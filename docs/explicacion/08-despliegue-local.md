# Despliegue local con Docker Compose

**Pregunta que responde:** ¿qué se expone al host, qué queda dentro de la red Docker y cómo se comprueba que el entorno está listo?

```mermaid
flowchart TB
    Dev["Navegador, Postman o curl<br/>host local"]

    subgraph Host["Host de desarrollo"]
        GPort["127.0.0.1:8080<br/>Gateway"]
        Ops["127.0.0.1<br/>puertos de infraestructura<br/>solo desarrollo"]
    end

    subgraph Network["Red interna de Docker Compose"]
        Gateway["Gateway"]
        Auth["Auth"]
        Control["Control"]
        Carga["CargaMasiva"]
        Notif["Notificaciones"]
        PG[("PostgreSQL")]
        MQ{{"RabbitMQ"}}
        SW["SeaweedFS"]
        Mail["Mailpit"]
    end

    Dev --> GPort --> Gateway
    Gateway --> Auth
    Gateway --> Control
    Gateway --> Carga
    Gateway --> Notif
    Control --> PG
    Control --> MQ
    Control --> SW
    Carga --> PG
    Carga --> MQ
    Carga --> SW
    Notif --> PG
    Notif --> MQ
    Notif --> Mail
    Ops --- PG
    Ops --- MQ
    Ops --- SW
    Ops --- Mail

    classDef public fill:#e3f2fd,stroke:#1565c0,color:#0d47a1
    classDef internal fill:#f5f5f5,stroke:#616161,color:#212121
    classDef infra fill:#fff3e0,stroke:#ef6c00,color:#e65100
    class GPort,Gateway public
    class Auth,Control,Carga,Notif internal
    class PG,MQ,SW,Mail,Ops infra
```

Los microservicios de negocio no publican un puerto propio hacia el host. El Gateway es el borde HTTP de aplicación; los puertos de infraestructura se enlazan a `127.0.0.1` para facilitar diagnóstico local sin exponerlos a la red externa.

## Secuencia de arranque

```mermaid
flowchart LR
    Env[".env local"] --> Compose["docker compose up -d --build --wait"]
    Compose --> Infra["PostgreSQL · RabbitMQ · SeaweedFS · Mailpit"]
    Infra --> Servicios["Gateway · Auth · Control · CargaMasiva · Notificaciones"]
    Servicios --> Health{"health checks correctos?"}
    Health -->|sí| Listo["Stack listo para pruebas"]
    Health -->|no| Diagnostico["docker compose ps<br/>docker compose logs -f servicio"]

    classDef ready fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    classDef warn fill:#fff3e0,stroke:#ef6c00,color:#e65100
    class Listo ready
    class Diagnostico warn
```

## Puertos de interés

| Recurso | Dirección local | Propósito |
|---|---|---|
| Gateway | http://localhost:8080 | API y `/health`. |
| RabbitMQ Management | http://localhost:15672 | Topología, mensajes y DLQ. |
| Mailpit | http://localhost:8025 | Ver los correos enviados. |
| PostgreSQL | `localhost:5432` | Diagnóstico local. |
| RabbitMQ AMQP | `localhost:5672` | Diagnóstico de mensajería local. |
| SeaweedFS | `localhost:8333`, `8888`, `9333` | Diagnóstico del almacenamiento local. |

El compose define health checks para los nueve contenedores. La comprobación reproducible es:

```cmd
docker compose up -d --build --wait
docker compose ps
for %S in (gateway auth control cargamasiva notificaciones) do @echo %S && docker compose exec -T %S curl -fsS http://localhost:8080/health
```

La definición ejecutable está en [docker-compose.yml](../../docker-compose.yml). Para borrar datos locales de manera intencional use `docker compose down -v`; esa operación elimina volúmenes.
