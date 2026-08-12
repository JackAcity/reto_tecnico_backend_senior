# Dependencias por capa — DIP verificable

**Pregunta que responde:** ¿qué puede conocer cada capa y qué impide que el dominio termine acoplado a PostgreSQL, RabbitMQ o HTTP?

Este diagrama representa referencias de proyecto permitidas; no el recorrido de una solicitud en ejecución.

```mermaid
flowchart RL
    Host["ServiceHost<br/>composición / arranque"]
    Api["API<br/>HTTP, consumidores, endpoints"]
    Infra["Infrastructure local<br/>adaptadores de Postgres, RabbitMQ y SeaweedFS"]
    App["Application<br/>casos de uso y puertos"]
    Domain["Domain<br/>reglas, entidades y estados"]
    Blocks["BuildingBlocks<br/>Resultado y contratos interservicio puros"]

    Host --> Api
    Host --> Infra
    Api --> App
    Api --> Infra
    Infra --> App
    Infra --> Blocks
    App --> Domain
    App --> Blocks

    Domain -. "prohibido: no conoce detalles técnicos" .-> Infra
    Blocks -. "prohibido: no referencia hosts ni frameworks" .-> Api

    classDef core fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    classDef adapter fill:#fff3e0,stroke:#ef6c00,color:#e65100
    classDef boundary fill:#e3f2fd,stroke:#1565c0,color:#0d47a1
    class Domain,App,Blocks core
    class Infra adapter
    class Host,Api boundary
```

Las líneas punteadas rojas conceptuales no son dependencias existentes: declaran relaciones que el diseño prohíbe. La dirección relevante de una flecha sólida es la de la referencia de compilación o de la composición, no la de una llamada HTTP.

## Regla práctica

| Elemento | Puede depender de | No debe depender de |
|---|---|---|
| Domain | Sus propias reglas y tipos de negocio | Infrastructure, API, EF Core, RabbitMQ, SeaweedFS |
| Application | Domain y contratos mínimos | Implementaciones concretas de adaptadores |
| Infrastructure local | Application, BuildingBlocks y librerías técnicas | Reglas de negocio del host o infraestructura de otro servicio |
| API | Application e Infrastructure local para composición | Lógica de negocio duplicada |
| ServiceHost | APIs e infraestructura para registrar dependencias | Reglas de negocio |
| BuildingBlocks | BCL y contratos simples | Hosts, frameworks, adaptadores concretos y servicios |

## Cómo se aplica al flujo real

```mermaid
sequenceDiagram
    participant Endpoint as API / Consumidor
    participant Caso as Application
    participant Puerto as Puerto definido por Application
    participant Adaptador as Adaptador local del servicio
    participant Recurso as Postgres, RabbitMQ o SeaweedFS

    Endpoint->>Caso: ejecuta caso de uso
    Caso->>Puerto: solicita operación abstracta
    Puerto->>Adaptador: implementación registrada por el host
    Adaptador->>Recurso: usa protocolo técnico
    Recurso-->>Adaptador: respuesta técnica
    Adaptador-->>Caso: resultado del puerto
    Caso-->>Endpoint: resultado de aplicación
```

El caso de uso no construye un cliente de RabbitMQ ni usa un `DbContext` directamente; declara lo que necesita mediante un puerto. El host decide qué adaptador concreto local se registra. Los contratos compartidos (`MensajeCarga`, `MensajeNotificacion` y `TopologiaMensajeria`) no instancian infraestructura ni introducen paquetes técnicos.

## Qué evita una regresión

La guardia de arquitectura comprueba, entre otros límites, que:

- `Shared/Persistencia` y `Shared/Mensajeria` no formen parte de la solución.
- `BuildingBlocks` no declare `FrameworkReference` ni `PackageReference`.
- El núcleo de los servicios no incorpore frameworks técnicos ni referencias a dominios de otro servicio.

La prueba está en [GuardiaArquitecturaTests.cs](../../tests/Reto.Tests/GuardiaArquitecturaTests.cs). El razonamiento completo y los puertos extraídos están en el [diseño hexagonal transversal](../../openspec/changes/arquitectura-hexagonal-transversal/design.md).

> DIP no significa una interfaz por cada clase. Aquí se introducen puertos en los límites donde existe una variación técnica real: persistencia, almacenamiento, publicación y autenticación.