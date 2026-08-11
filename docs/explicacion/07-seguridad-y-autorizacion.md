# Seguridad: autenticación, permisos y borde público

**Pregunta que responde:** ¿cómo se autentica a una persona, qué diferencia hay entre consultar y cargar, y por qué no basta con confiar en una cabecera interna?

## Inicio de sesión y token

```mermaid
sequenceDiagram
    autonumber
    participant Cliente
    participant Gateway
    participant Auth
    participant DB as PostgreSQL

    Cliente->>Gateway: POST /auth/login
    Gateway->>Gateway: límite de login + cuerpo máximo
    Gateway->>Auth: reenvía la solicitud
    Auth->>DB: busca usuario activo por email
    alt usuario inexistente
        Auth->>Auth: verifica contraseña simulada
        Auth-->>Gateway: credenciales inválidas
    else usuario existente
        Auth->>Auth: verifica hash de contraseña
        Auth->>DB: guarda rehash si es necesario
        Auth-->>Gateway: access token + refresh token
    end
    Gateway-->>Cliente: respuesta de autenticación
```

Auth no distingue al cliente si falló el email o la contraseña. Esa decisión reduce la señal disponible para enumerar usuarios. Los tokens de refresco se almacenan y rotan; el acceso posterior se realiza con el token firmado, no con una identidad enviada libremente por el cliente.

## Autorización de rutas

```mermaid
flowchart LR
    Cliente["Cliente con Bearer JWT"] --> Gateway["Gateway"]
    Gateway --> Validar["Valida firma, expiración y claims"]
    Validar --> Subida{"POST /cargas"}
    Validar --> Consulta{"GET /cargas*"}
    Subida -->|permiso carga:masiva| Control["Control"]
    Subida -->|sin permiso| P403["403 Forbidden"]
    Consulta -->|usuario autenticado| Control
    Consulta -->|sin token válido| P401["401 Unauthorized"]

    classDef allowed fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    classDef denied fill:#ffebee,stroke:#c62828,color:#b71c1c
    class Control allowed
    class P401,P403 denied
```

Subir y consultar no comparten la misma política:

| Ruta | Requisito | Cuota | Motivo |
|---|---|---|---|
| `POST /cargas` | Token válido y permiso `carga:masiva` | Límite de carga | Es una operación de escritura y transporte de archivo. |
| `GET /cargas...` | Usuario autenticado | Límite general por usuario | Permite polling e historial sin entregar privilegio de carga. |
| `POST /auth/login` y `/auth/refresh` | No requiere Bearer previo | Límite de login | El cuerpo se limita de forma independiente del archivo. |

## Defensa en profundidad

```mermaid
flowchart TB
    Internet["Cliente externo"] --> GW["Gateway<br/>único borde HTTP"]
    GW --> JWT["JWT + policy + rate limit"]
    JWT --> Control["Control<br/>vuelve a validar identidad"]
    Control --> Audit["usuario y correlation id<br/>persistidos para auditoría"]

    classDef boundary fill:#e3f2fd,stroke:#1565c0,color:#0d47a1
    classDef audit fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    class GW,JWT boundary
    class Audit audit
```

El Gateway centraliza el borde, pero Control vuelve a validar el contexto que utilizará para auditar. La identidad auditada proviene de claims validados, no de una cabecera que un servicio interno pudiera fabricar.

Las rutas y las políticas se definen en [Gateway/Program.cs](../../src/Gateway/Program.cs); el caso de autenticación está en [ServicioAutenticacion.cs](../../src/Services/Auth/Auth.Api/Application/ServicioAutenticacion.cs).
