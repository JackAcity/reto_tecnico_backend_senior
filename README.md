# Sistema de Carga Masiva Distribuida

Reto técnico backend senior — microservicios en .NET 10, mensajería con RabbitMQ,
almacenamiento en SeaweedFS, persistencia en PostgreSQL, todo dockerizado.

El enunciado original está en [`docs/RETO-ORIGINAL.md`](docs/RETO-ORIGINAL.md). Este
documento es la entrega: arquitectura, decisiones, cómo levantar y cómo probar.

## Cómo levantar

```bash
cp .env.example .env
docker compose up -d --wait
```

Espera a que los 9 contenedores estén `healthy` (el flag `--wait` bloquea hasta que
lo estén, o falla si alguno no llega). Primer arranque: Control migra el esquema y
Auth siembra dos usuarios de demostración.

| Servicio | URL |
|---|---|
| API (gateway, único punto público) | http://localhost:8080 |
| RabbitMQ management | http://localhost:15672 |
| Mailpit (correo de la demo) | http://localhost:8025 |
| PostgreSQL | `localhost:5432` |

**Usuarios sembrados** (`.env`, cambiar antes de cualquier uso real):

| Usuario | Password | Rol | Permiso `carga:masiva` |
|---|---|---|---|
| `admin@reto.local` | `Reto2026!` | `administrador` | Sí |
| `consulta@reto.local` | `Consulta2026!` | `consulta` | No — demuestra el 403 de la policy |

Bajar todo (y borrar los datos): `docker compose down -v`.

## Cómo probar

**Postman** — `postman/reto-carga-masiva.postman_collection.json` +
`postman/reto-local.postman_environment.json`. Importar los dos, seleccionar el
environment, correr "Auth → Login (admin)" primero (guarda el token para el resto de
la colección). Incluye los casos negativos (401/403/404/400) además del camino feliz.
Verificada con `newman` contra el stack real: 15/15 requests, 12/12 asserts.

**curl**, si preferís no abrir Postman:

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@reto.local","password":"Reto2026!"}' | jq -r .accessToken)

curl -X POST http://localhost:8080/cargas \
  -H "Authorization: Bearer $TOKEN" \
  -F "archivo=@samples/carga_masiva_productos.xlsx"

curl http://localhost:8080/cargas -H "Authorization: Bearer $TOKEN"
```

**Tests automatizados** (95, corren contra contenedores reales — no hay dobles de
prueba para Postgres/RabbitMQ/SeaweedFS, solo para llamadas HTTP salientes puntuales):

```bash
dotnet test tests/CargaMasiva.Tests/CargaMasiva.Tests.csproj
```

## Arquitectura

```
Cliente/Postman
      │
      ▼
  Gateway (YARP) ── JWT + rate limiting + enrutamiento ── único punto público
      │
   ┌──┼───────────────┬──────────────────┐
   ▼  ▼               ▼                  ▼
 Auth Control ───► RabbitMQ ───► CargaMasiva ───► RabbitMQ ───► Notificaciones
      │  │          (carga_masiva)   │  │           (notificaciones)    │
      │  ▼                           │  ▼                               ▼
      │ SeaweedFS ◄───────────────────┘ PostgreSQL ◄────────────────────┘
      ▼
 PostgreSQL (una sola base, un solo esquema — el diagrama del enunciado la prescribe)
```

| Componente | Rol |
|---|---|
| `Gateway` (YARP) | JWT, rate limiting, enrutamiento — nada de negocio |
| `Auth` | `/auth/login`, `/auth/refresh` con rotación → JWT Bearer |
| `Control` | Recibe la subida, valida, sube a SeaweedFS, publica en `carga_masiva` |
| `CargaMasiva` | **El núcleo.** Consume, descarga, procesa el Excel, inserta, audita, publica en `notificaciones` |
| `Notificaciones` | Consume, envía correo (MailKit → Mailpit), marca `Notificado` |

Cada servicio de negocio sigue Domain → Application → Infrastructure → Api (Clean
Architecture, Uncle Bob): el dominio no sabe que Postgres, RabbitMQ o SeaweedFS
existen — son detalles detrás de interfaces que Application define.

## Decisiones de diseño

El enunciado tiene contradicciones reales — se resuelven con evidencia, no a ciegas.
El detalle completo, con la evidencia medida y las citas exactas del enunciado, está en
[`openspec/changes/carga-masiva-microservicios/design.md`](openspec/changes/carga-masiva-microservicios/design.md).
Resumen:

| # | Contradicción | Resolución |
|---|---|---|
| C1 | .NET 10 (encabezado) vs 8/9 (cuerpo) | .NET 10 — commit más reciente del repo origen actualizó solo el encabezado |
| C2 | CargaMasiva valida periodo y encontraría su propia fila | El SP excluye el propio `IdCarga` |
| C3 | "El periodo" asume 1 archivo = 1 periodo; el archivo de muestra trae 3 | Se modela `CargaPeriodo`, procesamiento parcial por periodo |
| C4 | Duplicados intra-archivo no especificados (42% del archivo de muestra) | Dedup también dentro del lote, primera ocurrencia gana |
| C5 | Clave de unicidad: global vs `(Periodo, CodigoProducto)` | **Clave compuesta** — con clave global, la carga del segundo mes rechazaría todo. Test determinista: **154 insertados / 46 `Existente`** |
| C6 | Máquina de estados incompleta (falta `Rechazada`/`Bloqueada`) | Ver [`specs/maquina-estados.md`](openspec/changes/carga-masiva-microservicios/specs/maquina-estados.md) |
| C7 | Dual write (INSERT + publish, sin transacción común) | Publish post-commit; si falla, `Fallida` con el error auditado. Outbox transaccional fuera de alcance, declarado |
| C8 | Entrega at-least-once → duplicados al reprocesar | Índice único como llave de idempotencia (`ON CONFLICT DO NOTHING`) |
| C9 | Carrera de concurrencia al resolver el periodo | `pg_advisory_xact_lock` dentro del SP |
| C10 | Diagrama muestra una sola base; database-per-service es lo "correcto" | Se respeta el diagrama — una base, propiedad de escritura explícita por tabla |
| C11 | N servicios migrando a la vez = carrera | Solo Control migra; el resto espera por health check |
| C12 | Límite de archivo en 3 techos distintos (Kestrel/form options/YARP) | Los tres calculados de un único valor de configuración |
| C13 | Gestión de secretos | Variables de entorno (`.env`, gitignored) — Docker secrets y Vault evaluados y descartados por costo/beneficio en este alcance |
| C14 | Endurecimiento adicional (a pedido, criterio DevSecOps) | Contenedores no-root, puertos en loopback, validación de firma de archivo (no solo extensión), `nosniff` |
| C15 | CQRS — ¿una tabla o dos? | Una — el modelo de lectura ya está separado en código (`ConsultaCargas`); dos tablas violaría la consistencia que `sp_resolver_periodo` necesita |
| C16 | Postgres en contenedor, ¿riesgo de perder la base? | No es el contenedor — es volumen persistente + backup, ortogonal a Docker. `pgdata:` ya es un volumen nombrado; un servicio administrado (RDS) ataría el proyecto a una cuenta de nube que el evaluador no tiene por qué tener |

## Matriz de trazabilidad

Extracción verbatim del enunciado, con dónde está implementado cada requisito
obligatorio. Completa en
[`specs/matriz-requisitos.md`](openspec/changes/carga-masiva-microservicios/specs/matriz-requisitos.md).
Puntos que exigieron una lectura menos literal:

- **§2.1e** — *"consultar el contenido del archivo excel subido"* → `GET /cargas/{id}/contenido`, reproxea el `.xlsx` original desde SeaweedFS.
- **§2.7a/b** — nombres de cola exactos: `carga_masiva` y `notificaciones`.
- **§3.2d/§3.3g** — contrato de mensaje copiado literal del enunciado (campos y `seaweed://` incluidos); el `correlationId` viaja como header AMQP, no en el body, para no alterar ese contrato.
- **D1** — Dockerfile/compose están marcados "opcional" en §4 pero valen 20% en §6.3 → tratados como obligatorios.

## Seguridad por diseño

Auditoría propia, más allá de lo pedido explícitamente por el enunciado (detalle en
`design.md` §C13/§C14):

- JWT HS256, refresh con rotación de un solo uso, policy por claim (`carga:masiva`)
- Timing side-channel corregido en login (verificación de hash siempre corre, exista o no el usuario)
- `IExceptionHandler` global no filtra `ex.Message` en errores no clasificados (500)
- Validación de firma binaria del archivo (ZIP), no solo la extensión
- `X-Correlation-Id` de entrada saneado antes de loguearlo (evita inyección de líneas de log)
- Contenedores no-root, puertos de infraestructura acotados a `127.0.0.1`
- Ninguna credencial de infraestructura tiene fallback en código — todo falla al arrancar si falta, en vez de adivinar un valor por defecto

## Trade-offs y fuera de alcance

Declarados a propósito, no descuidos — razón completa en `proposal.md`/`design.md`:

- **CI/CD** — cero menciones en el enunciado; el 20% de DevOps es literal "docker-compose funcional".
- **Database-per-service** — el diagrama entregado prescribe una sola base.
- **Transactional Outbox** — mitigado con publish post-commit + estado terminal `Fallida` (§C7).
- **TLS local, CORS, Vault** — evaluados y descartados por costo/beneficio para este alcance (§C13/§C14).
- **Notificación en cargas `Rechazada`/`Bloqueada`/`Fallida`** — el enunciado solo define `Finalizado → Notificado`; se respeta tal cual, aunque implica que un rechazo no se comunica por correo.

## Scripts de base de datos

`scripts/sql/esquema.sql` — exportado con `dotnet ef migrations script --idempotent`,
incluye las tablas y los dos procedimientos almacenados (`sp_resolver_periodo`,
`sp_insertar_data_procesada`). Se aplica solo mediante Control al arrancar (§C11); este
script es para inspección o para levantar el esquema sin Docker.

## Fixtures de prueba

- `samples/carga_masiva_productos.xlsx` — el archivo real del enunciado. Camino feliz: 200 filas, 3 periodos, resultado determinista **154 insertados / 46 `Existente`**.
- `samples/fixture-sucio.xlsx` — genera las 8 reglas de limpieza/validación en una sola carga (fila vacía descartada, columnas vacías con default, precio inválido, periodo inválido/ausente, código ausente, duplicado intra-lote). Verificado en vivo: `10 filas → 5 insertadas / 5 rechazadas`, exactamente los 8 motivos esperados.

## Video

_Pendiente — se enlaza acá al grabarlo (Bloque 9)._
