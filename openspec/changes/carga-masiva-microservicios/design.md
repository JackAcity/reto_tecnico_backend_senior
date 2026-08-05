# Diseño técnico

> Este documento se recicla como la sección de arquitectura del `README.md` de entrega.

## 1. Evidencia: qué contiene realmente el archivo de muestra

`samples/carga_masiva_productos.xlsx` — 200 filas, columnas
`Periodo | CodigoProducto | NombreProducto | Precio`.

| Hecho medido | Valor |
|---|---|
| Periodos distintos **en un solo archivo** | 3 → `2025-01` (47), `2025-02` (105), `2025-03` (48) |
| `CodigoProducto` distintos | 116 → **84 filas son duplicados intra-archivo (42%)** |
| Códigos repetidos dentro del *mismo* periodo | 35 |
| Códigos repetidos *cruzando* periodos | 36 |
| Duplicados con `Precio` distinto | 59 de 59 → conflicto real, no fila clonada |
| Celdas vacías / filas vacías / precios inválidos | 0 / 0 / 0 |

Las dos últimas filas importan: el archivo entregado es el **camino feliz**, pero el
enunciado exige manejar *"columna vacía → valor por defecto"* y *"filas vacías → no
registrar"*. Se generan fixtures sucios propios para demostrar ambas reglas.

## 2. Contradicciones del enunciado y su resolución

### C1 — Versión de .NET
El encabezado dice `.NET 10` (×5). La sección *Requerimientos **Obligatorios*** y las
cinco cajas del diagrama dicen `.NET 8/9`.

**Resolución: .NET 10.** El commit más reciente del repositorio origen (`abba4ad`,
30-jul-2026) es literalmente *"Update .NET version references in README"* — se
actualizó el encabezado y no se re-renderizó la imagen ni se tocó la sección 4.
Además, **.NET 8 y .NET 9 alcanzan End of Support el 10-nov-2026**; .NET 10 es LTS
hasta nov-2028. Al estar todo dockerizado, la versión del SDK del evaluador es
irrelevante. Migrar a 9 es cambiar `<TargetFramework>`.

### C2 — La validación de periodo está en el microservicio equivocado
El enunciado §2️⃣ dice que **Control** crea la fila `Pendiente` *antes* de publicar.
El §3️⃣ dice que **CargaMasiva** debe validar *"si existe una carga previa Pendiente o
En proceso → bloquear"*.

Implementado literal, CargaMasiva **encuentra su propia fila** y se auto-bloquea.
Pero Control tampoco puede validar: el `Periodo` vive **dentro** del Excel, y Control
no parsea Excel (el diagrama lo confirma: Control solo guarda en SeaweedFS y publica).

**Resolución:** la validación vive en CargaMasiva y **excluye su propio `IdCarga`**:

```sql
WHERE Periodo = @periodo AND IdCarga <> @idCargaActual
```

### C3 — "El periodo" no existe: son tres
La regla asume `1 archivo = 1 periodo`. El archivo que el propio enunciado entrega lo
desmiente.

**Resolución:** se modela `CargaPeriodo` (1 `CargaArchivo` → N periodos) y el
procesamiento es **parcial por periodo**: las filas de periodos libres se insertan;
las de periodos en conflicto van a la tabla de auditoría con el motivo. Es la única
lectura consistente con el mandato del enunciado de *"almacenar los fallidos en una
tabla de auditoría y trazabilidad"*.

### C4 — Duplicados intra-archivo: no especificados, y son el 42%
El enunciado dice *"se consulta la base de datos"*. Con la base vacía, las 200 filas
pasan el filtro y el `INSERT` masivo viola la restricción única.

**Resolución:** la deduplicación ocurre **también dentro del lote**. Primera
ocurrencia gana; el resto se reporta `Existente` y se audita.

### C5 — Clave de unicidad: las dos reglas del enunciado son incompatibles

El enunciado pide dos validaciones distintas:

- **Regla A** (nivel carga): no recargar un periodo que ya fue cargado.
- **Regla B** (nivel fila): *"Si existe un elemento con el mismo Codigo no se debe
  registrar y se debe reportar como **Existente**"* — sin mencionar el periodo.

No pueden coexistir. **Si `CodigoProducto` fuera único global, la Regla A no tendría
razón de existir**: en un catálogo de productos con código único no hay nada que
controlar por periodo. La sola presencia de la Regla A demuestra que los datos están
particionados por periodo, y por lo tanto la clave natural es `(Periodo, CodigoProducto)`.
La Regla B está escrita omitiendo el periodo.

La evidencia empírica lo confirma. En el archivo entregado:

```
P0060 → 2025-02 : 229.02
P0060 → 2025-03 : 263.82     ← mismo código, otro periodo, otro precio
```

36 códigos se comportan así, y los 59 duplicados tienen precio distinto. Con clave
global, la carga de marzo se rechazaría entera: **un sistema de carga masiva mensual
que solo sirve el primer mes.**

**Resolución: la clave es `(Periodo, CodigoProducto)`.**

La Regla B no se abandona, se escopa correctamente: 35 códigos se repiten *dentro del
mismo periodo* y siguen reportándose `Existente`.

| Clave | Insertados | Rechazados `Existente` |
|---|---|---|
| `CodigoProducto` global (lectura literal) | 116 | 84 |
| **`(Periodo, CodigoProducto)`** ← elegida | **154** | **46** |

Ese par `154 / 46` es el test de aceptación del sistema. Se documentan ambos
escenarios para que el criterio sea auditable.

### C6 — Máquina de estados incompleta
El enunciado enumera cinco estados pero exige comportamientos (*rechazada*,
*bloqueada*, *fallidos*) que no tienen estado, y nunca define `Cargado` vs `Finalizado`.

**Resolución:** ver `specs/maquina-estados.md`.

### C7 — Dual write en Control y en CargaMasiva
`INSERT` en base + `publish` en RabbitMQ son dos sistemas sin transacción común. Si el
publish falla tras el commit, la carga queda huérfana en `Pendiente` para siempre.

El patrón correcto es **Transactional Outbox** (Richardson). Queda **fuera de alcance
por tiempo**; mitigación: publicación inmediatamente posterior al commit, y si falla,
transición a estado terminal `Fallida` con el error auditado. Trade-off declarado.

### C8 — Entrega at-least-once → el consumidor debe ser idempotente
RabbitMQ reentrega. Un lote insertado a medias más un crash duplica registros al
reprocesar.

**Resolución:** la restricción única sobre `CodigoProducto` actúa como llave natural
de idempotencia (**Idempotent Consumer**, Richardson), reforzada por la comprobación
de estado de la carga antes de procesar.

### C9 — Carrera de concurrencia en el periodo
Dos cargas simultáneas del mismo periodo: `SELECT` y luego `INSERT` es un TOCTOU. Un
`SELECT` no alcanza.

**Resolución:** `pg_advisory_xact_lock(hashtext(periodo))` dentro del procedimiento
almacenado que resuelve el periodo, más índice único parcial sobre periodos en estado
activo.

### C10 — Base de datos compartida
El diagrama entregado muestra **una sola** caja de base de datos con tres servicios
accediéndola. Microsoft y Richardson recomiendan *database-per-service*.

**Resolución:** se respeta el diagrama — una base, un esquema, con **propiedad de
escritura explícita**: `Control` crea, `CargaMasiva` transiciona, `Notificaciones`
marca `Notificado`. El trade-off se declara en el README. Nombrarlo suma; ignorarlo
resta.

### C11 — Migraciones automáticas con N servicios arrancando a la vez
Todos ejecutando `Migrate()` al boot es una carrera.

**Resolución:** un único servicio es dueño del esquema (`Control`); el resto espera
vía health check.

### C12 — Límite de tamaño de archivo
Kestrel (30 MB por defecto), los límites de formulario y YARP son **tres** techos
distintos. Sin configurar los tres, aparece un `413` crudo en el gateway que parece
un bug. La validación de negocio debe responder un error claro **antes** de ese techo.

## 3. Decisiones de librerías (y por qué)

| Necesidad | Elección | Razón |
|---|---|---|
| Mediator / CQRS | **Handlers propios** (~40 líneas) | MediatR v13+ es comercial. Fowler advierte contra CQRS completo: *"you should be very cautious about using CQRS"*. Se aplica CQRS-lite: separación Command/Query sin infraestructura de lectura aparte |
| Broker client | **`RabbitMQ.Client`** | MassTransit v9 es comercial. Además el enunciado evalúa la topología (*"intercambio directo o topic, mínimo 2 colas"*) — MassTransit la autogenera y la vuelve invisible |
| Excel | **`ExcelDataReader`** (MIT) | Lectura forward-only en streaming, memoria constante. El reto se llama *carga masiva* |
| Resiliencia | **`Microsoft.Extensions.Http.Resilience`** | `AddStandardResilienceHandler()` = una línea → retry + circuit breaker + timeout + jitter. Cubre dos ítems "valorados" del enunciado |
| Rate limiting | **`AddRateLimiter()` nativo** + `RouteConfig.RateLimiterPolicy` de YARP | Requisito obligatorio, cero dependencias |
| Excepciones | **`IExceptionHandler` nativo** | Requisito obligatorio, cero dependencias |
| Logging | **Serilog** estructurado + `CorrelationId` propagado HTTP → cola → consumidor | *Trazabilidad* está en el título del reto |
| SMTP para demo | **MailHog** en compose | El correo se ve en el video sin credenciales reales |

## 4. Arquitectura limpia — cómo se aplica

Regla de dependencia de Uncle Bob: **el código fuente solo apunta hacia adentro.**

```
Domain      ← entidades, reglas de negocio, sin dependencias externas
Application ← casos de uso (handlers), define interfaces (puertos)
Infrastructure ← EF Core, RabbitMQ, SeaweedFS, MailKit (implementan los puertos)
Api         ← endpoints, DI, configuración
```

*"The Web is a detail. The database is a detail."* — por eso los procedimientos
almacenados (obligatorios) viven en `Infrastructure`, detrás de una interfaz que
`Application` define. Se usan donde **realmente** ganan:

1. Inserción masiva set-based de `DataProcesada` (un round trip en vez de N).
2. Resolución atómica del periodo, con advisory lock.

Sin procedimientos de adorno.

## 5. Referencias

- Robert C. Martin — [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- Martin Fowler — [CQRS](https://martinfowler.com/bliki/CQRS.html) · [Microservice Prerequisites](https://martinfowler.com/bliki/MicroservicePrerequisites.html)
- Chris Richardson — [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) · [Idempotent Consumer](https://microservices.io/patterns/communication-style/idempotent-consumer.html)
- Microsoft — [.NET Microservices Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/) · [YARP Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/rate-limiting?view=aspnetcore-10.0)
- RabbitMQ — [Dead Letter Exchanges](https://www.rabbitmq.com/docs/dlx)
