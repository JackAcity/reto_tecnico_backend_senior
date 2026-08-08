# Prueba de escala — 2 millones de filas

El enunciado llama "carga masiva" al reto; el archivo de muestra
(`samples/carga_masiva_productos.xlsx`) trae 200 filas. Esto documenta qué
pasa con un archivo de 2 millones — el orden de magnitud que se suele
entender por "masivo" — corrido de verdad contra el stack, no estimado.

## Generación del archivo

`scripts/generar_masivo.py` — streaming real (`openpyxl.Workbook(write_only=True)`),
memoria constante en la generación:

```bash
python scripts/generar_masivo.py 2000000 samples/carga_masiva_2M.xlsx
```

Resultado: **2,000,000 filas, 49.7 MB**, ~7m48s en generarse. No se commitea
(`.gitignore`) — se regenera con el comando de arriba. Periodos `2030-01`
a `2030-12` (fuera del rango que usan los demás fixtures), código de producto
único por fila para que el test mida inserción real y no duplicados.

## Corrida 1 — reveló un bug real, no confirmó el diseño

Subido con `Carga:TamanoMaximoMb` elevado a 250 (solo para esta prueba, no
en el compose de la entrega — el límite de 25 MB en `design.md §C12` sigue
vigente para el path que evalúa el enunciado).

**Resultado: `Fallida`**, no `Finalizado`.

```
mensajeError: "Agotados los 3 intentos: Exception while reading from stream"
```

Log de `cargamasiva`:

```
Npgsql.NpgsqlException: Exception while reading from stream
 ---> System.TimeoutException: Timeout during reading attempt
   at ... InsertadorMasivo.InsertarAsync(...) in InsertadorMasivo.cs:line 33
```

**Causa:** `InsertadorMasivo` mandaba las ~2M filas aceptadas en un único
`unnest`-insert (un solo round trip). El `CommandTimeout` de Npgsql
(default 30s) se agotaba antes de que Postgres terminara de resolver un
`INSERT` de ese tamaño. El consumidor reintenta 3 veces
(`ConsumidorCargaMasiva`), pero el timeout no era transitorio — reintentar
el mismo round trip gigante fallaba igual las 3 veces.

Memoria pico durante el intento (medida con `docker stats` cada 2s):
**2.37 GiB**. Consistente con lo que ya se había leído en el código antes de
correr la prueba: `ManejadorCarga.cs:44` materializa el archivo entero con
`.ToList()`, y `ProcesadorLote` arma varias copias completas en
`List<FilaProducto>` — nada de eso es memoria constante para 2M filas, pese
al comentario de `LectorExcel.cs` (que sí es forward-only; el problema está
en cómo se consume, no en el lector).

## Fix — chunking en `InsertadorMasivo`

`InsertadorMasivo.InsertarAsync` ahora inserta en lotes de 20,000 filas
(parámetro `tamanoLote`, default en el constructor) en vez de un único
`unnest` para todo el archivo — mismo patrón set-based, N round trips en vez
de 1 gigante. Ver `src/Services/CargaMasiva/CargaMasiva.Infrastructure/InsertadorMasivo.cs`.

## Corrida 2 — con el fix

Mismo archivo (`carga_masiva_2M.xlsx`), mismo stack reconstruido con el fix.

```json
{
  "estado": "Finalizado",
  "totalFilas": 2000000,
  "filasInsertadas": 2000000,
  "filasRechazadas": 0
}
```

- **Tiempo punta a punta:** 3m43s (`fechaRegistro` → `fechaFin`).
- **Memoria:** pico medido de al menos 1.37 GiB (muestreo parcial — el
  sampler de `docker stats` se cortó a los 74s de un proceso de 224s por una
  limitación del entorno de shell, no alcanzó a cubrir todo el rango; el
  número real puede ser mayor). Sigue sin ser memoria constante — el
  chunking arregla el insert, no las copias completas que arma
  `ProcesadorLote` antes de llegar ahí.
- **100/100 tests automatizados** siguen en verde después del cambio
  (`dotnet test tests/CargaMasiva.Tests`).

## Corrida 3 — 5M filas: no falla limpio, entra en OOM-loop

Mismo procedimiento, `carga_masiva_5M.xlsx` (5,000,000 filas, 125 MB,
generado en 8m01s). Bump temporal de `Carga:TamanoMaximoMb` a 250, stack
reconstruido con el fix de chunking (ya validado a 2M en la Corrida 2).

**No llegó a `Fallida`.** Después de ~20 minutos sin salir de `EnProceso`,
`docker inspect` mostró **2 restarts** del contenedor `cargamasiva` durante
esa única carga, y memoria trepando otra vez hacia **9.03 GiB de un VM de
Docker con 11.53 GiB totales** — compartido con Postgres, RabbitMQ,
SeaweedFS y el resto del stack.

Logs revisados (`docker logs --since`, grep de la ventana completa): **cero**
líneas de `"Fallo procesando"` (el catch que ya conocíamos de la Corrida 1),
cero `"shutting down"` — es decir, el proceso no murió por una excepción
manejada ni un shutdown ordenado. Consistente con **OOM-kill del kernel**
dentro del contenedor: lo mata en seco, sin ack, RabbitMQ redelivera el
mismo mensaje al reiniciar el consumidor, y el ciclo completo (descargar,
reparsear, reprocesar desde cero) arranca de nuevo — sin tope. El circuito
de reintento de la app (3 intentos, `x-death`) nunca se activa porque nunca
llega a esa rama de código: el proceso no sobrevive lo suficiente para
contar el intento.

**Intervención manual necesaria** para cortarlo: `docker compose stop
cargamasiva` (si no, el loop no tiene techo propio) + purgar el mensaje
atascado de la cola vía la API de management de RabbitMQ. Verificado
después: stack completo healthy, **100/100 tests siguen en verde** — el
incidente no dejó nada roto, solo agotó tiempo y memoria.

## Dónde está el techo real (con este equipo)

- **2M filas**: bien. 3m43s, memoria acotada (~1.4-2.4 GiB), termina
  `Finalizado`.
- **5M filas**: mal. Nunca termina — OOM-kill en loop, hay que intervenir
  a mano para pararlo.
- El techo está en algún punto entre esos dos, en **este** equipo (VM de
  Docker de 11.53 GiB). En un servidor con más RAM el número sube, pero la
  causa no cambia: el chunking de la Corrida 2 arregló el *timeout de
  Postgres*, no convirtió el pipeline en memoria O(1). `ManejadorCarga`
  (`.ToList()` del archivo completo) y `ProcesadorLote` (varias copias
  completas en `List<FilaProducto>`) siguen materializando el archivo
  entero antes de insertar nada — a 2M cabe en el presupuesto de memoria
  del contenedor, a 5M no.

Esto **no** es ya "optimización sin problema medido" — es un techo medido,
concreto, con un incidente real (loop de OOM que hubo que cortar a mano) que
lo prueba. Documentado como limitación conocida en vez de arreglado en este
alcance: arreglar el bug de la Corrida 1 (el timeout) era necesario para que
la carga masiva funcionara en absoluto; hacer el pipeline streaming de
punta a punta es un cambio más grande (tocar `ManejadorCarga` y
`ProcesadorLote`, no solo `InsertadorMasivo`) que no se justifica sin que el
enunciado o un caso de uso real pida archivos de ese tamaño.

## Cómo reproducir

```bash
python scripts/generar_masivo.py 2000000 samples/carga_masiva_2M.xlsx
# .env: CARGA_TAMANO_MAXIMO_MB=250 (temporal)
docker compose up -d --wait
# login, POST /cargas con el archivo, poll GET /cargas/{id}
# .env: CARGA_TAMANO_MAXIMO_MB=25 (revertir)
docker compose up -d --wait
```

Para reproducir la Corrida 3 (5M, cuidado — entra en el OOM-loop descrito
arriba, tener `docker compose stop cargamasiva` a mano): mismo procedimiento
con `python scripts/generar_masivo.py 5000000 samples/carga_masiva_5M.xlsx`.
Si el estado no sale de `EnProceso` en varios minutos y `docker inspect
reto-carga-masiva-cargamasiva-1 --format '{{.RestartCount}}'` sube, es el
mismo cuadro — parar el consumidor y purgar la cola (`DELETE
/api/queues/%2F/carga_masiva/contents` contra la management API de
RabbitMQ) en vez de esperar a que resuelva solo.
