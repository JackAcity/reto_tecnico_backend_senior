# Arquitectura — diagrama de componentes

Quién le habla a quién, y por qué protocolo. Flechas sólidas = HTTP síncrono;
flechas punteadas = mensaje asíncrono por RabbitMQ.

```mermaid
flowchart TB
    Cliente["Cliente / Postman"]

    subgraph borde[" "]
        GW["Gateway (YARP)<br/>JWT + rate limit + routing<br/>único puerto público: 8080"]
    end

    subgraph servicios["Microservicios (sin puerto publicado — solo red interna)"]
        Auth["Auth<br/>/auth/login, /auth/refresh"]
        Control["Control<br/>POST /cargas, GET /cargas*"]
        CM["CargaMasiva<br/>consumidor + publicador"]
        Notif["Notificaciones<br/>consumidor"]
    end

    subgraph infra["Infraestructura"]
        PG[("PostgreSQL<br/>una base, un esquema")]
        MQ{{"RabbitMQ<br/>exchange topic 'cargas'"}}
        SW["SeaweedFS<br/>filer HTTP"]
        Mail["Mailpit (SMTP demo)"]
    end

    Cliente -->|"HTTPS + Bearer JWT"| GW
    GW -->|"valida credenciales"| Auth
    GW -->|"POST /cargas, GET /cargas*"| Control

    Control -->|"sube el .xlsx"| SW
    Control -->|"INSERT Pendiente"| PG
    Control -. "publica MensajeCarga<br/>rk: carga.masiva" .-> MQ

    MQ -. "consume<br/>cola: carga_masiva" .-> CM
    CM -->|"descarga el .xlsx"| SW
    CM -->|"sp_resolver_periodo<br/>sp_insertar_data_procesada"| PG
    CM -. "publica MensajeNotificacion<br/>rk: carga.notificacion" .-> MQ

    MQ -. "consume<br/>cola: notificaciones" .-> Notif
    Notif -->|"SMTP"| Mail
    Notif -->|"UPDATE Notificado"| PG

    Auth -->|"siembra/valida usuario"| PG

    style GW fill:#dae8fc,stroke:#6c8ebf
    style CM fill:#ffe6cc,stroke:#d79b00
```

## Por qué el Gateway es el único punto público

Los 4 microservicios de negocio **no publican puerto** en `docker-compose.yml` —
solo son alcanzables dentro de la red de Docker. Si alguien intentara pegarle
directo a Control saltándose el Gateway, no podría: no hay puerto que tocar
desde el host. El Gateway valida el JWT una vez; Control lo vuelve a validar
igual (§3.2a), porque el usuario auditado sale del token, no de una cabecera
que cualquiera en la red interna podría inventar.

## Por qué Control y CargaMasiva no se llaman directo

No hay ninguna llamada HTTP entre Control y CargaMasiva. Todo el acoplamiento
es a través de RabbitMQ — Control no sabe si CargaMasiva está vivo, ocupado, o
caído en ese momento; solo publica y sigue. Es lo que hace que la subida
(`POST /cargas`) responda en milisegundos aunque el archivo tarde segundos en
procesarse: el procesamiento es asíncrono por diseño, no por optimización.

## Preguntas que esto responde

- *"¿Por qué no hay una llamada REST de Control a CargaMasiva?"* — porque el
  desacople es el punto: si CargaMasiva está caído, la carga queda en
  `Pendiente` esperando en la cola, no falla la subida.
- *"¿Cómo se protege Postgres de que cualquier servicio escriba cualquier
  cosa?"* — no hay aislamiento físico (una sola base, por el diagrama del
  enunciado, C10), pero sí propiedad de escritura declarada por tabla
  (`db-schema.md`) y respetada por disciplina de código.
- *"¿Qué pasa si el Gateway se cae?"* — todo el sistema queda inalcanzable
  desde afuera; es el único punto de falla del borde, a cambio de centralizar
  JWT y rate limiting en un solo lugar en vez de duplicarlo en 4 servicios.
