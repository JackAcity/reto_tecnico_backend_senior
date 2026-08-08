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

## Qué queda fuera de este alcance

El pico de memoria (~1.4-2.4 GiB dependiendo de la corrida) viene de que
`ManejadorCarga`/`ProcesadorLote` siguen materializando el archivo completo
en listas antes de insertar — el chunking resuelve el timeout de Postgres
(el bug que de verdad rompía la carga), no convierte el pipeline en memoria
verdaderamente O(1). Para eso haría falta procesar el Excel en lotes de punta
a punta (leer N filas → resolver periodos → insertar → siguiente N), no solo
en el insert final. No se implementó — el bug real (`Fallida` a los 2M) ya
está resuelto y verificado; ir más allá es optimización sin un problema
medido que la justifique todavía.

## Cómo reproducir

```bash
python scripts/generar_masivo.py 2000000 samples/carga_masiva_2M.xlsx
# .env: CARGA_TAMANO_MAXIMO_MB=250 (temporal)
docker compose up -d --wait
# login, POST /cargas con el archivo, poll GET /cargas/{id}
# .env: CARGA_TAMANO_MAXIMO_MB=25 (revertir)
docker compose up -d --wait
```
