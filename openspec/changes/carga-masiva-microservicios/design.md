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

### C13 — Gestión de secretos
No es una contradicción del enunciado (no lo menciona), pero es una pregunta legítima
de seguridad-por-diseño: ¿por qué `POSTGRES_PASSWORD`, `RABBITMQ_PASSWORD` y `JWT_KEY`
viajan como variables de entorno (`.env` → `environment:` en el compose) y no en un
gestor de secretos?

Se evaluaron tres escalones:

| Opción | Qué gana | Costo |
|---|---|---|
| **Variables de entorno** (elegida) | Cero infraestructura extra | Visible en `docker inspect` y en procesos hijos |
| **Docker secrets** (archivo montado en `/run/secrets/`, `AddKeyPerFile()` en .NET) | No aparece en `docker inspect`; sin Swarm, sigue siendo un archivo plano en disco | Bloques `secrets:` por servicio + cambiar el proveedor de configuración en cada uno |
| **Vault** (secretos dinámicos, rotación, políticas, auditoría) | La respuesta correcta en producción | Contenedor propio, unseal, políticas — para un `docker compose up` que un evaluador corre una vez |

**Resolución: variables de entorno**, con `.env` en `.gitignore` y `.env.example` sin
valores reales. El salto de "env var" a "Docker secrets" es marginal en este contexto —
el secreto sigue siendo un archivo en texto plano en la máquina de quien evalúa, sea cual
sea el mecanismo — y el salto a Vault es infraestructura desproporcionada para una entrega
que se levanta una sola vez, en una sola máquina, sin superficie de red expuesta más allá
de `localhost`. Trade-off consciente, no descuido.

### C14 — Endurecimiento adicional (seguridad por diseño)

Tampoco pedido por el enunciado, pero auditado a pedido explícito con criterio DevSecOps.
Seis controles de alto valor y bajo costo, aplicados; dos descartados con su razón.

**Aplicados:**

1. **Contenedores no-root.** Verificado con `docker compose exec gateway id` → daba
   `uid=0(root)`. Las imágenes oficiales `mcr.microsoft.com/dotnet/aspnet` traen un
   usuario no-root desde .NET 8, pero hay que activarlo — se agrega `USER $APP_UID`
   a los 5 `Dockerfile` (fuente: `learn.microsoft.com/dotnet/core/whats-new/dotnet-8/containers`,
   consultada en vivo). Costo: una línea por Dockerfile, cero regresión.
2. **Puertos de infraestructura acotados a loopback.** `docker compose port postgres 5432`
   devolvía `0.0.0.0:5432` — publicado en *todas* las interfaces, alcanzable desde
   cualquier equipo en la misma red que la del evaluador, con credenciales de ejemplo
   en `.env`. Se cambia a `127.0.0.1:5432:5432` (y equivalente en rabbitmq, seaweedfs,
   mailpit, gateway). No rompe nada: tests, `psql`/`dotnet ef` y el video de demo usan
   `localhost`, no la IP de red.
3. **Validación de contenido, no solo de extensión.** `ValidarArchivo` (§2.4b) solo
   miraba el nombre — un archivo renombrado a `.xlsx` lo pasaba igual. Se agrega
   `ValidarFirmaAsync`: los primeros 4 bytes deben ser la firma ZIP local-file-header
   (`PK\x03\x04`), porque todo `.xlsx` (OOXML) es por dentro un zip. Evita gastar
   SeaweedFS + una cola + un ciclo de CargaMasiva en un archivo que iba a fallar de
   todos modos al intentar leerse como Excel.
4. **`X-Content-Type-Options: nosniff`**, en `ServiceDefaults` (un solo lugar para los
   5 servicios).
5. **CORS con origen explícito.** Reevaluado al construirse el cliente React (O.1) —
   ver siguiente punto: dejó de ser un "no aplica" para ser un prerequisito técnico.
   `AddCors`/`UseCors` **solo en Gateway** (única puerta pública), `WithOrigins`
   con el origen del cliente por config (`Cors:OrigenesPermitidos`), nunca
   `AllowAnyOrigin`. Sin `AllowCredentials`: el JWT viaja en `Authorization`, no en
   cookies, así que no lo necesita — esto también es lo que hace que CSRF clásico
   (basado en que el navegador adjunta cookies solo) no aplique a esta API.
6. **`X-Frame-Options: DENY`, en el dev server de Vite del cliente React.** Mismo
   motivo que el punto anterior: con un cliente browser real, un sitio malicioso
   podría enmarcarlo en un `<iframe>` invisible y usar clickjacking contra una acción
   autenticada simple (ej. "Cerrar sesión" — subir un archivo resiste esto mejor,
   exige un file picker nativo que no se puede automatizar a ciegas). `frame-ancestors`
   no es soportado vía `<meta>`, tiene que ser un header real de servidor; el dev
   server de Vite es el único servidor HTTP que este frontend usa hoy, así que es
   donde se aplica (`vite.config.ts`, `server.headers` + `preview.headers`).

**Descartados, con razón:**

| Control | Por qué no |
|---|---|
| **CSP completa** (más allá de `frame-ancestors`, ya cubierto arriba vía `X-Frame-Options`) | El *host* real que necesitaría una CSP estricta (`script-src`, etc.) es un servidor de producción sirviendo el build estático — no existe en este repo: el frontend es deliberadamente dev-server-only (`vite dev`), igual que el resto del alcance no contempla contenerizarlo (el enunciado no lo exige). Forzar una CSP estricta sobre el dev server de Vite arriesga romper HMR (WebSocket + estilos inline del propio tooling) por un artefacto que nunca es lo que se despliega. Se reevalúa si el frontend se conteneriza para producción. |
| **TLS/HTTPS local** | El modelo de amenaza real es "la propia máquina del evaluador", no un atacante en la red — y con el punto 2 ya resuelto, ni siquiera hay superficie de red más allá de `localhost` (el cliente React corre en la misma máquina, contra el mismo `localhost:8080`). Añadir certificados de desarrollo a 5 Dockerfiles + el gateway + el dev server de Vite es costo real por un riesgo que no existe en este alcance. |
| **Rate limiter con sliding window / token bucket** | El nativo (`FixedWindowRateLimiter`, Bloque 4) admite ráfaga doble en el borde de la ventana — límite conocido. Sliding window/token bucket exige código propio o un paquete de pago; el nativo ya cumple el requisito obligatorio (§4.3) sin esa brecha siendo explotable de forma práctica en este alcance. |
| **Revocación de access token** | JWT stateless por diseño: un token robado sigue siendo válido hasta su expiración (60 min). Mitigarlo exige una lista de revocación consultada en cada request, lo que reintroduce el estado que el JWT stateless evita — trade-off estándar y documentado de cualquier sistema JWT sin sesión server-side; el refresh token sí rota y sí se revoca (Bloque 3). |

### C15 — CQRS: ¿una tabla o dos?

`ConsultaCargas`/`ServicioCargas` ya separan comando de consulta en código (CQRS-lite,
§3). La pregunta que queda es de datos: ¿el lado de lectura necesita su propia tabla
(proyección/read-model), como en un CQRS completo?

**Resolución: una sola tabla.** Dos tablas se justifica cuando lectura y escritura
escalan distinto, la forma de la lectura difiere mucho de la de escritura, o la
consistencia eventual es aceptable. Ninguna aplica acá — y la tercera está
directamente **prohibida** por el propio diseño: `sp_resolver_periodo` (§C2/§C9)
exige que la lectura vea la escritura dentro de la misma transacción; con una
proyección desincronizada, dos cargas del mismo periodo podrían pasar ambas.

**Preparados para migrar si hiciera falta:** sí, a bajo costo, porque el corte ya
está en el lugar correcto. `ConsultaCargas` es la única clase que sabe de dónde
lee — cambiar su fuente a una proyección no toca el dominio, `ServicioCargas` ni
los endpoints. Y el disparador para poblar esa proyección ya existe: cada cambio
de estado ya publica en RabbitMQ; un proyector sería un consumidor más de la
misma cola, no una reescritura.

### C16 — Postgres en un contenedor: ¿riesgo de perder la base?

Objeción planteada (de fuera, discutida con el usuario): *"está mal poner una BD en
un contenedor porque si se borra se borra toda la base — se debería contratar como
servicio administrado"*. La premisa mezcla dos cosas distintas y hay que separarlas.

**El contenedor no es la causa del riesgo.** Un contenedor de Postgres no pierde
datos al reiniciarse, pararse o reconstruir la imagen. Los pierde si (a) no tiene
volumen persistente — el disco vive solo dentro del contenedor, efímero —, o (b)
alguien borra el volumen a propósito, o (c) no hay backup y el disco físico falla.
Ninguna de las tres es "por ser contenedor": pasan igual con Postgres instalado
directo en un VM sin volumen redundante ni backup. Acá `pgdata:` (`docker-compose.yml`)
ya es un volumen nombrado — `stop`/`start`/`restart`/reconstruir no toca los datos.
Solo `docker compose down -v` explícito los borra, usado en esta sesión **a propósito**
para probar el determinismo 154/46 desde cero, no por accidente.

**Lo que "contratarla como servicio" (RDS, Cloud SQL, Azure Database) compra de
verdad** no es "no perder datos" — eso lo da un volumen bien puesto. Es backup
automático con point-in-time recovery, failover automático, parches sin operación
manual, réplicas de lectura. Preocupaciones de **operación en producción**, no de
dónde corre el proceso.

**¿Es exclusivo de nube?** El producto específico (RDS/Cloud SQL) sí. El principio
—que alguien o algo automatice backup + HA— no: Patroni (failover automático de
Postgres on-premise), el operador CloudNativePG sobre Kubernetes propio, pgBackRest/
Barman para backup y PITR sin nube, o vendors enterprise que venden "DBaaS" para
correr en hardware propio. La pregunta correcta no es "¿contenedor o servicio
administrado?", es "¿quién es dueño de backup y HA: un vendor de nube, un operador
de Kubernetes, o un humano con un cron de `pg_basebackup`?" — cualquiera de las tres
es válida; la que no lo es, es "nadie".

**Resolución para este reto: no aplica.** Se evalúa con `docker compose up` una vez,
en una máquina, sin datos de producción reales — mismo argumento que C13 (secretos)
y C14 (TLS local): meter un servicio administrado ataría el proyecto a una cuenta de
nube y facturación para que el evaluador ni siquiera pueda levantarlo, rompiendo el
requisito real de `docker-compose` autocontenido. El volumen nombrado ya resuelve el
riesgo real dentro de este alcance.

### C17 — RabbitMQ vs. Kafka a escala extrema

Pregunta planteada (discutida con el usuario): *"¿Kafka para mensajes masivos de
alta concurrencia — quince millones en menos de un minuto — o hay que usar
RabbitMQ igual?"*

**La diferencia real es quién hace el trabajo por mensaje.** RabbitMQ es *broker
inteligente, consumidor tonto*: el broker decide el ruteo (exchange → bindings →
cola) y rastrea qué mensajes están sin confirmar por cada consumidor — trabajo
por mensaje, en memoria. Kafka es *broker tonto, consumidor inteligente*: el
broker solo anexa al final de un log inmutable particionado (I/O secuencial a
disco, más `sendfile()` zero-copy) y cada consumidor lleva su propio offset — casi
cero decisión por mensaje del lado del broker.

**A ~250 000 msg/seg sostenidos, el diseño de Kafka es la respuesta correcta, no
una preferencia.** Particionar y agregar consumidores en paralelo escala
linealmente. RabbitMQ puede empujarse a ese volumen (colas quorum, muchas colas,
o específicamente RabbitMQ Streams, la feature construida para competir con Kafka
en throughput) pero ahí se está reconstruyendo a mano lo que Kafka da nativo.
RabbitMQ rinde mejor en volumen medio con ruteo rico (topic exchanges, prioridad,
TTL, dead-letter por mensaje); Kafka rinde mejor en volumen bruto sostenido.

Dos diferencias adicionales que pesan a esa escala: **replay** (Kafka retiene el
log, varios *consumer groups* independientes releen el mismo stream a su propio
ritmo; RabbitMQ clásico borra al confirmar) y **exactly-once nativo** (productor
idempotente + API transaccional en Kafka; RabbitMQ es at-least-once por diseño,
exige idempotencia del lado del consumidor — exactamente lo que este proyecto ya
resuelve con el índice único + `ON CONFLICT`, §C8).

**Resolución: no aplica a este reto, y no es contradicción con lo anterior.** El
volumen acá es un mensaje por archivo subido — la decisión nunca fue throughput.
El enunciado permite "RabbitMQ/Kafka/otro" explícitamente, el DLX + reintento +
routing-key de RabbitMQ calza directo con "mínimo 2 colas, intercambio directo o
topic", y meter Kafka (Zookeeper/KRaft, gestión de particiones, cluster de
brokers) sería sobrecarga operativa pura para un docker-compose de demo — mismo
argumento de costo/beneficio que C13/C14/C16.

### C18 — Rotación de refresh token: lost-update entre dos refresh simultáneos

Hallazgo posterior a la entrega original (repaso de arquitectura antes del video),
no parte del enunciado: `RefrescarAsync` leía el token, decidía en C# si estaba
activo, y recién después escribía (`ServicioAutenticacion.cs`, versión previa a
este cambio). Dos requests `POST /auth/refresh` con el **mismo** refresh token,
todavía activo, llegando al mismo tiempo (doble pestaña, retry sin esperar la
respuesta anterior) es el mismo patrón TOCTOU que §C9, pero en una tabla sin
ninguna guarda: `refresh_token` solo tiene `Token` único (trivial, es random) —
nada de `[ConcurrencyCheck]`, `xmin` ni un `WHERE` condicional en el `UPDATE`
que EF emite por PK. Resultado: los dos requests pueden leer el mismo estado
"activo", y los dos terminan creando su propio hijo — un *lost update* clásico,
el segundo `UPDATE` pisa el `ReemplazadoPor` del primero sin que nadie lo
rechace. Distinto del caso ya cubierto por el comentario `ponytail:` de la
misma clase (reuso *secuencial* de un token ya revocado, que sí devuelve 401);
acá ninguno de los dos actuó mal — la carrera está en la escritura, no en el uso.

**Resolución:** igual que §C9, un TOCTOU se cierra con lock o con escritura
atómica condicional — acá no hace falta un advisory lock (es una sola fila, un
solo `UPDATE`), alcanza con condición en el propio `WHERE`, estilo
compare-and-swap: `ExecuteUpdateAsync` sobre `RefreshTokens.Where(t => t.Id ==
anterior.Id && t.RevocadoEn == null)`. Si afecta 0 filas, alguien más ya rotó
ese token primero → se trata como token inválido (mismo 401 que ya existía),
en vez de crear un segundo hijo válido. Mismo espíritu que `ON CONFLICT DO
NOTHING` de `sp_insertar_data_procesada` (§C8): el motor decide atómicamente
quién ganó, no una lectura previa en código de aplicación.

**Severidad:** baja — exige dos requests literalmente simultáneos con el mismo
token válido; el impacto sin el fix es "dos sesiones hijas válidas en vez de
una", no un bypass de autenticación. Se corrige de todos modos porque el
mecanismo (lock vs. escritura atómica condicional) es el mismo criterio que ya
se aplicó en §C9, y dejarlo asimétrico —protegido en `carga_periodo`, sin
protección en `refresh_token`— es la clase de inconsistencia que un evaluador
de criterio encuentra.

**Test determinista:** `Refresh_RotacionConcurrente_SoloUnIntentoGanaLaCarrera`
— simula la interleaving ejecutando dos intentos de rotación atómica contra el
mismo token todavía activo: el primero afecta 1 fila, el segundo 0, y
`ReemplazadoPor` queda con el valor del primero, nunca pisado por el segundo.

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
