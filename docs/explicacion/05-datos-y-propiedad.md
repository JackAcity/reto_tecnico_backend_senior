# Modelo de datos y responsabilidad de escritura

**Pregunta que responde:** si se usa una sola base para el reto, ¿qué datos existen, quién los modifica y cómo se protege la consistencia?

```mermaid
erDiagram
    USUARIO ||--o{ REFRESH_TOKEN : posee
    CARGA_ARCHIVO ||--o{ CARGA_PERIODO : registra
    CARGA_ARCHIVO ||--o{ DATA_PROCESADA : origina
    CARGA_ARCHIVO ||--o{ DETALLE_CARGA_ERROR : audita

    USUARIO {
        int id PK
        string email UK
        string rol
        bool activo
    }
    REFRESH_TOKEN {
        int id PK
        int usuario_id FK
        string token UK
        string reemplazado_por
    }
    CARGA_ARCHIVO {
        int id PK
        string nombre_archivo
        string ruta_archivo
        string usuario_auditado
        string estado
        string correlation_id
        int total_filas
        int filas_insertadas
        int filas_rechazadas
    }
    CARGA_PERIODO {
        int id PK
        int carga_archivo_id FK
        string periodo
        string estado
        int filas_insertadas
    }
    DATA_PROCESADA {
        int id PK
        int carga_archivo_id FK
        string periodo
        string codigo_producto
        decimal precio
    }
    DETALLE_CARGA_ERROR {
        int id PK
        int carga_archivo_id FK
        int numero_fila
        string motivo
    }
```

## Propiedad por ciclo de vida

| Datos | Autoridad de escritura | Uso |
|---|---|---|
| `usuario` y `refresh_token` | Auth | Login, emisión y rotación de credenciales. |
| `carga_archivo` al crearla | Control | Registra la intención, la ruta del archivo y el usuario auditado. |
| `carga_archivo` durante el proceso | CargaMasiva | Actualiza estados, contadores y errores de procesamiento. |
| `carga_archivo` al notificar | Notificaciones | Cierra `Finalizado → Notificado` después del correo. |
| `carga_periodo`, `data_procesada` y `detalle_carga_error` | CargaMasiva | Reserva períodos, persiste datos aceptados y deja auditoría por fila. |

`carga_archivo.usuario` es una instantánea de auditoría y no una clave foránea hacia `usuario`. Por eso no hay una línea entre esas dos entidades en el diagrama: el historial debe conservar quién inició la carga aunque el usuario cambie después.

## Consistencia que importa

```mermaid
flowchart LR
    Archivo["Archivo Excel"] --> Periodos["Períodos del archivo"]
    Periodos --> Resolver["sp_resolver_periodo<br/>advisory lock"]
    Resolver -->|Libre| Reserva["carga_periodo Aceptado"]
    Resolver -->|Ya cargado| Error["detalle_carga_error"]
    Resolver -->|En proceso| Bloqueo["estado Bloqueada"]
    Reserva --> Insertar["sp_insertar_data_procesada<br/>lotes unnest"]
    Insertar --> Unico["Índice único<br/>(periodo, codigo_producto)"]
    Unico --> Datos["data_procesada"]

    classDef good fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    classDef warn fill:#fff3e0,stroke:#ef6c00,color:#e65100
    class Reserva,Insertar,Unico,Datos good
    class Error,Bloqueo warn
```

- El `advisory lock` evita la carrera entre dos cargas que intentan reservar el mismo período.
- El índice único `(periodo, codigo_producto)` es también la llave de idempotencia: una reentrega no duplica datos ya insertados.
- El conteo de errores exacto puede ser costoso para millones de filas; el historial resumido es el endpoint apropiado para polling.

Cada servicio materializa sólo su vista de persistencia: [AuthDbContext](../../src/Services/Auth/Auth.Api/Infrastructure/AuthDbContext.cs), [ControlDbContext](../../src/Services/Control/Control.Api/Infrastructure/ControlDbContext.cs), [CargaMasivaDbContext](../../src/Services/CargaMasiva/CargaMasiva.Infrastructure/CargaMasivaDbContext.cs) y [NotificacionesDbContext](../../src/Services/Notificaciones/Notificaciones.Api/Infrastructure/NotificacionesDbContext.cs). El esquema compartido lo aplica una vez [DatabaseMigrator](../../src/DatabaseMigrator/Program.cs); ningún servicio de negocio migra la base ni conoce el contexto de otro.
