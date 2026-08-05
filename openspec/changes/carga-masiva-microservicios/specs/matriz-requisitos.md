# Matriz de requisitos — lectura literal del enunciado

> Extracción verbatim de `docs/RETO-ORIGINAL.md`. Regla del evaluador:
> *"sigue las indicaciones, el stack y el alcance técnico funcional. **Ni más ni menos**."*
>
> `O` = obligatorio (sin marca) · `V` = "opcional pero valorado" · `X` = opcional puro
> Esta tabla se recicla como sección de trazabilidad del `README.md` de entrega.

## §2 — Componentes de la arquitectura

| # | Requisito (verbatim) | Tipo | Dónde |
|---|---|---|---|
| 2.1 | Cliente Web React | V | — |
| 2.1a | *"En caso no se realice la interfaz con React se deben enviar las colecciones postman con cada petición"* | **O** | `postman/` |
| 2.1b | Iniciar Sesión | O | `POST /auth/login` |
| 2.1c | Subir un archivo Excel (.xlsx) | O | `POST /cargas` |
| 2.1d | Consultar el historial de cargas | O | `GET /cargas` |
| 2.1e | ⚠️ ***"Consultar el contenido del archivo excel subido"*** | **O** | `GET /cargas/{id}/contenido` |
| 2.1f | Ver el estado: Pendiente / En proceso / Cargado / Finalizado / Notificado | O | `GET /cargas/{id}` |
| 2.2a | Gateway recibe **todas** las solicitudes del cliente | O | YARP |
| 2.2b | Gateway **valida JWT** | O | Gateway |
| 2.2c | Gateway reenvía al microservicio correspondiente | O | YARP routes |
| 2.3a | `/auth/login` | O | Auth |
| 2.3b | Valida credenciales contra fuente de identidad (BD / IdP / servicio interno) | O | Auth + tabla `Usuario` |
| 2.3c | Genera y retorna **JWT Bearer con claims del usuario** | O | Auth |
| 2.3d | `/auth/refresh` | V | Auth |
| 2.4a | Control recibe del Gateway la solicitud de carga | O | Control |
| 2.4b | *"Validar que archivo tenga no exceda el tamaño maximo **configurado**"* | O | `appsettings` + env |
| 2.4c | Guarda trazabilidad con estado inicial **Pendiente** | O | `CargaArchivo` |
| 2.4d | Publica mensaje en la cola | O | Control |
| 2.4e | Envía el archivo a **SeaweedFS** | O | Control |
| 2.5a | CargaMasiva escucha la cola | O | Consumer |
| 2.5b | Descarga el archivo desde SeaweedFS | O | CargaMasiva |
| 2.5c | Realiza **validaciones y limpieza** de datos del Excel | O | CargaMasiva |
| 2.5d | Inserta la información en PostgreSQL | O | `DataProcesada` |
| 2.5e | ⚠️ Marca los **tres** estados: **En proceso, Cargado, Finalizado** | O | CargaMasiva |
| 2.5f | Publica notificación en una **segunda** cola | O | CargaMasiva |
| 2.6a | Notificaciones escucha la cola de notificaciones | O | Consumer |
| 2.6b | Envía correo al usuario indicando que la carga finalizó | O | Notificaciones |
| 2.6c | **Usa MailKit** | O | Notificaciones |
| 2.6d | Actualiza el estado final a **Notificado** | O | Notificaciones |
| 2.7a | ⚠️ Cola 1 llamada exactamente **`carga_masiva`** | O | RabbitMQ |
| 2.7b | ⚠️ Cola 2 llamada exactamente **`notificaciones`** | O | RabbitMQ |
| 2.9 | SeaweedFS para almacenar los Excel | O | compose |

## §3 — Flujo y reglas de negocio

| # | Requisito (verbatim) | Tipo | Dónde |
|---|---|---|---|
| 3.2a | *"Valida que el usuario tenga **permiso** para ejecutar cargas masivas"* | O | policy `carga:masiva` |
| 3.2b | Valida tamaño permitido **y extensión correcta** | O | Control |
| 3.2c | *"Registra **auditoría de quién** subió el archivo **y cuándo**"* | O | `CargaArchivo` |
| 3.2d | ⚠️ **Formato exacto del mensaje 1** | O | contrato |
| 3.3a | Rechazar si el periodo ya tiene carga `Cargado`/`Finalizado`/`Notificado` | O | `sp_resolver_periodo` |
| 3.3b | Bloquear si el periodo tiene carga `Pendiente`/`En proceso` | O | `sp_resolver_periodo` |
| 3.3c | *"almacenar los fallidos en una tabla de **auditoria y trazabilidad**"* | O | `DetalleCargaError` |
| 3.3d | Duplicado por `CodigoProducto` → reportar **`Existente`** | O | procesador |
| 3.3e | *"En el caso de que una columna esté vacía debe guardar un **valor por defecto**"* | O | procesador |
| 3.3f | *"Si hay **filas vacías** en el archivo **no se deben registrar**"* | O | procesador |
| 3.3g | ⚠️ **Formato exacto del mensaje 2** | O | contrato |
| 3.5 | *"Permite ver estados en tiempo real (mediante **pooling**)"* | O | `GET /cargas` polling |

**Contratos de mensaje — copiados textualmente del enunciado, se respetan tal cual:**

```json
// cola: carga_masiva
{ "idCarga": 123, "rutaArchivo": "seaweed://.../archivo.xlsx", "usuario": "user@example.com" }

// cola: notificaciones
{ "idCarga": 123, "usuario": "user@example.com", "fechaFin": "2025-02-10T10:20:00" }
```

camelCase, esos nombres de campo, y `rutaArchivo` con esquema `seaweed://`.

## §4 — Requerimientos técnicos

| # | Requisito (verbatim) | Tipo | Dónde |
|---|---|---|---|
| 4.1 | Lenguaje .NET | O | .NET 10 (ver `design.md` C1) |
| 4.2 | **Arquitectura limpia** | O | Domain/Application/Infrastructure/Api |
| 4.3 | *"**CQRS ó** Inversión de dependencias"* — es un **O** inclusivo | O | CQRS-lite + DI |
| 4.4 | **SOLID** | O | transversal |
| 4.5 | JWT | O | Auth + Gateway |
| 4.6 | **Manejo de excepciones global** | O | `IExceptionHandler` |
| 4.7 | **Logging estructurado** | O | Serilog + CorrelationId |
| 4.8 | ⚠️ Dockerfile por microservicio — *"(Opcional pero valorado)"* | V | pero vale 20% |
| 4.9 | ⚠️ docker-compose general — *"(Opcional pero valorado)"* | V | pero vale 20% |
| 4.10 | **Implementar Patrón Rate Limiting** — sin marca de opcional | **O** | Gateway |
| 4.11 | *"Uso de **Dapper ó EntityFramework**"* | O | EF Core 10 |
| 4.12 | Circuit Breaker | V | `AddStandardResilienceHandler` |
| 4.13 | Patrones de Reintentos | V | idem + DLX |
| 4.14 | BD: *"**Debe incluir** migraciones automáticas"* | **O** | EF migrations al boot |
| 4.15 | BD: *"**Uso de procedimientos almacenados**"* | **O** | 2 SPs |
| 4.16 | Mensajería: *"Intercambio **directo o topic**"* | O | exchange topic |
| 4.17 | Mensajería: *"**Mínimo 2 colas**"* | O | 2 + DLQ |
| 4.18 | SeaweedFS **dockerizado** | O | compose |
| 4.19 | Correo *"**Configurable por variables de entorno**"* | O | `.env` |

## §7 — Entregables

| # | Requisito | Tipo |
|---|---|---|
| 7.1 | Código fuente completo en repositorio **GITHUB** (singular: **un** repositorio) | O |
| 7.2 | **Documentación en README** | O |
| 7.3 | Instrucciones de despliegue | V |
| 7.4 | **Scripts de base de datos** | O |
| 7.5 | Postman collection | O (por 2.1a) |
| 7.6 | **Video corto (máximo 5 minutos)** mostrando flujo completo funcionando | O |

## Contradicciones detectadas en esta pasada

**D1 — Dockerfile y compose están marcados "opcional" pero valen el 20% de la nota.**
§4 los marca *"(Opcional pero valorado)"*; §6.3 asigna **20%** a *"docker-compose funcional"*.
→ Se tratan como obligatorios. La rúbrica manda sobre la marca.

**D2 — El estado `Cargado` aparece y desaparece.**
§2.5 exige marcar *"En proceso, **Cargado**, Finalizado"*. El flujo §3.3 solo describe
*"Actualiza el estado → En proceso ... Estado → Finalizado"*, sin `Cargado`.
→ Se implementan los tres (§2.5 es la lista explícita de responsabilidades).

**D3 — §6.3 dice *"Servicios se levantan sin errores (opcional)"***
dentro de un criterio que vale 20%. Marca ignorada por la misma razón que D1.

## Fuera de alcance — verificado por ausencia en el texto

Ninguna mención de: CI/CD, pipeline, GitHub Actions, Kubernetes, Redis, caché,
service discovery, observabilidad/tracing distribuido, database-per-service,
outbox, saga, retención o borrado de archivos, paginación, multi-tenant.
