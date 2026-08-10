# Sistema de Carga Masiva Distribuida

Reto técnico backend senior — microservicios en .NET 10, mensajería con RabbitMQ,
almacenamiento en SeaweedFS, persistencia en PostgreSQL, todo dockerizado.

El enunciado original está en [`docs/RETO-ORIGINAL.md`](docs/RETO-ORIGINAL.md). Este
documento es la entrega: arquitectura, decisiones, cómo levantar y cómo probar.

## Video de la demo

**[▶ Ver demo completa (~5 min)](https://drive.google.com/file/d/1VGuuXTcuEjBK-6rf-VLUXAvVc3XzjMx4/view?usp=sharing)**
— login → subida real → estados por polling cada 3 s → correo (Mailpit) → colas
(RabbitMQ) → caso rechazado (mismo periodo dos veces) → permisos por rol (403).

## En 30 segundos

- **Gateway → Auth/Control → RabbitMQ → CargaMasiva → RabbitMQ → Notificaciones**,
  una Postgres compartida, SeaweedFS para el archivo. 5 microservicios, Clean
  Architecture en cada uno.
- Excel leído en **streaming** (memoria acotada, no se carga el libro entero),
  inserción **set-based** (`unnest`, un round trip) en vez de fila por fila.
- Test de aceptación determinista: `samples/carga_masiva_productos.xlsx` →
  **154 filas insertadas / 46 rechazadas**, reproducible con un comando.
- **100 tests automatizados**, corridos contra contenedores reales — sin dobles
  de prueba para Postgres/RabbitMQ/SeaweedFS.
- `docker compose up -d --wait` levanta los 9 contenedores y confirma que
  están sanos antes de devolver el control.

## Rúbrica de evaluación

Dónde está la evidencia de cada criterio del enunciado (§6), para no tener que
buscarla:

| Criterio | Peso | Evidencia |
|---|---|---|
| **Arquitectura** — microservicios independientes, limpieza, manejo de colas y estados | 25% | [Arquitectura](#arquitectura) · [Decisiones de diseño](#decisiones-de-diseño) · Clean Architecture por servicio (Domain → Application → Infrastructure → Api) |
| **Funcionalidad** — flujo completo operativo, procesamiento real del Excel, persistencia correcta | 35% | [Video de la demo](#video-de-la-demo) · [Cómo probar](#cómo-probar) · test determinista 154/46 (`specs/procesamiento-excel.md`) |
| **Docker / DevOps** — compose funcional, servicios se levantan sin errores | 20% | [Cómo levantar](#cómo-levantar) — 9 contenedores, `--wait` lo verifica en el propio comando |
| **Frontend o Colecciones Postman** | 20% | [Cómo probar](#cómo-probar) — Postman verificado con `newman` (15/15) **y**, además, cliente React opcional |

## Índice

- [Cómo levantar](#cómo-levantar)
- [Cómo probar](#cómo-probar)
- [Arquitectura](#arquitectura)
- [Decisiones de diseño](#decisiones-de-diseño)
- [Matriz de trazabilidad](#matriz-de-trazabilidad)
- [Seguridad por diseño](#seguridad-por-diseño)
- [Trade-offs y fuera de alcance](#trade-offs-y-fuera-de-alcance)
- [Scripts de base de datos](#scripts-de-base-de-datos)
- [Fixtures de prueba](#fixtures-de-prueba)

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

**Tests automatizados** (100, corren contra contenedores reales — no hay dobles de
prueba para Postgres/RabbitMQ/SeaweedFS, solo para llamadas HTTP salientes puntuales):

```bash
dotnet test tests/Reto.Tests/Reto.Tests.csproj
```

**Cliente web (React, opcional — §2.1 del enunciado)** — las 4 pantallas exigidas
(login, subida, historial, detalle), consumiendo el Gateway igual que Postman/curl,
sin lógica de negocio propia. Spec completo, contrato consumido y decisiones en
[`specs/frontend-cliente-react.md`](openspec/changes/carga-masiva-microservicios/specs/frontend-cliente-react.md).

```bash
cd frontend
cp .env.example .env    # VITE_API_URL=http://localhost:8080
npm install
npm run dev              # http://localhost:5173
npm test                 # 2/2 — vitest + testing-library, sin backend
```

Verificado en vivo (Chrome real, no solo curl): login → historial con polling cada
3 s → detalle con periodos/errores auditados → descarga del `.xlsx` original →
subida real hasta `Notificado`. Esa verificación expuso 2 bugs preexistentes del
backend (ninguno introducido por el frontend, ambos invisibles mientras el único
cliente probado era Postman) — detalle completo en
[`tasks.md` §O.1.5](openspec/changes/carga-masiva-microservicios/tasks.md):

1. **Gateway sin CORS** — necesario en cuanto el cliente deja de ser Postman y pasa
   a ser un navegador real en otro origen (`localhost:5173`). `AddCors`/`UseCors`
   agregado solo en Gateway, origen explícito por config, sin `AllowAnyOrigin`.
2. **La ruta `/cargas/{**resto}` del Gateway exigía el permiso `carga:masiva` para
   TODO método**, incluido `GET` — el rol `consulta` no podía ni ver su propio
   historial, aunque Control ya solo exige "autenticado" en sus `GET`. Separada en
   `cargas-subida` (POST) y `cargas-consulta` (GET, con el rate limit general de
   60/min en vez del de subida de 10/min — el polling lo necesitaba).

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

**Set completo de diagramas** (`.drawio`, editable en draw.io/diagrams.net) para
estudiar y discutir toda la solución antes de una entrevista —arquitectura, flujo
feliz, flujo de rechazo, máquina de estados, modelo de datos, mensajería, JWT,
despliegue— con orden de lectura y "qué te pueden preguntar" por cada uno:
[`docs/explicacion/README.md`](docs/explicacion/README.md).

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
| C17 | RabbitMQ vs. Kafka a escala extrema (~250K msg/seg) | Kafka es la respuesta correcta a esa escala (log particionado, I/O secuencial), pero acá el volumen es un mensaje por archivo — la decisión nunca fue throughput. RabbitMQ calza directo con "mínimo 2 colas" del enunciado |

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
- CORS en Gateway con origen explícito (nunca `AllowAnyOrigin`, sin `AllowCredentials` — el JWT viaja en `Authorization`, no en cookies) y `X-Frame-Options: DENY` en el dev server del cliente React, contra clickjacking sobre acciones autenticadas — ambos reevaluados y aplicados al construirse el frontend (§C14)

## Trade-offs y fuera de alcance

Declarados a propósito, no descuidos — razón completa en `proposal.md`/`design.md`:

- **CI/CD** — cero menciones en el enunciado; el 20% de DevOps es literal "docker-compose funcional".
- **Database-per-service** — el diagrama entregado prescribe una sola base.
- **Transactional Outbox** — mitigado con publish post-commit + estado terminal `Fallida` (§C7).
- **TLS local, CSP completa, Vault** — evaluados y descartados por costo/beneficio para este alcance (§C13/§C14). CORS y `X-Frame-Options` sí se aplicaron — ver "Seguridad por diseño" arriba.
- **Pipeline no es memoria O(1) de punta a punta** — el insert ya es por lotes (ver abajo), pero `ManejadorCarga`/`ProcesadorLote` siguen materializando el archivo completo en listas antes de insertar. Techo real medido, no hipotético: 2M filas funciona (3m43s, ~2 GiB), 5M entra en loop de OOM-kill del contenedor. Detalle completo, incluido cómo se cortó el incidente: [`docs/pruebas-de-escala.md`](docs/pruebas-de-escala.md).
- **Notificación en cargas `Rechazada`/`Bloqueada`/`Fallida`** — el enunciado solo define `Finalizado → Notificado`; se respeta tal cual, aunque implica que un rechazo no se comunica por correo.

## Scripts de base de datos

`scripts/sql/esquema.sql` — exportado con `dotnet ef migrations script --idempotent`,
incluye las tablas y los dos procedimientos almacenados (`sp_resolver_periodo`,
`sp_insertar_data_procesada`). Se aplica solo mediante Control al arrancar (§C11); este
script es para inspección o para levantar el esquema sin Docker.

## Fixtures de prueba

- `samples/carga_masiva_productos.xlsx` — el archivo real del enunciado. Camino feliz: 200 filas, 3 periodos, resultado determinista **154 insertados / 46 `Existente`**.
- `samples/fixture-sucio.xlsx` — genera las 8 reglas de limpieza/validación en una sola carga (fila vacía descartada, columnas vacías con default, precio inválido, periodo inválido/ausente, código ausente, duplicado intra-lote). Verificado en vivo: `10 filas → 5 insertadas / 5 rechazadas`, exactamente los 8 motivos esperados.
- **Escala (2M filas)** — no versionado (se regenera con `scripts/generar_masivo.py`). Corrida real completa, incluyendo un bug encontrado y arreglado en el proceso: [`docs/pruebas-de-escala.md`](docs/pruebas-de-escala.md).
