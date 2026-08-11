# Dependencias por capa — DIP verificable

**Pregunta que responde:** ¿qué puede conocer cada capa y qué impide que el dominio termine acoplado a PostgreSQL, RabbitMQ o HTTP?

Este diagrama representa referencias de proyecto permitidas; no el recorrido de una solicitud en ejecución.

```mermaid
flowchart RL
    Host["ServiceHost<br/>composición / arranque"]
    Api["API<br/>HTTP, consumidores, endpoints"]
    Infra["Infrastructure<br/>adaptadores de Postgres, RabbitMQ y SeaweedFS"]
    App["Application<br/>casos de uso y puertos"]
    Domain["Domain<br/>reglas, entidades y estados"]
    Shared["Shared<br/>adaptadores técnicos reutilizables"]
    Blocks["BuildingBlocks<br/>Resultado y contratos simples"]

    Host --> Api
    Host --> Infra
    Api --> App
    Api --> Infra
    Infra --> App
    Infra --> Shared
    App --> Domain
    App --> Blocks

    Domain -. "prohibido: no conoce detalles técnicos" .-> Infra
    Shared -. "prohibido: no referencia BuildingBlocks" .-> Blocks
    Blocks -. "prohibido: no referencia Shared ni hosts" .-> Shared

    classDef core fill:#e8f5e9,stroke:#388e3c,color:#1b5e20
    classDef adapter fill:#fff3e0,stroke:#ef6c00,color:#e65100
    classDef boundary fill:#e3f2fd,stroke:#1565c0,color:#0d47a1
    class Domain,App,Blocks core
    class Infra,Shared adapter
    class Host,Api boundary
```

Las líneas punteadas rojas conceptuales no son dependencias existentes: declaran relaciones que el diseño prohíbe. La dirección relevante de una flecha sólida es la de la referencia de compilación o de la composición, no la de una llamada HTTP.

## Regla práctica

| Elemento | Puede depender de | No debe depender de |
|---|---|---|
| Domain | Sus propias reglas y tipos de negocio | Infrastructure, API, EF Core, RabbitMQ, SeaweedFS |
| Application | Domain y contratos mínimos | Implementaciones concretas de adaptadores |
| Infrastructure | Application, Shared y librerías técnicas | Reglas de negocio del host |
| API | Application e Infrastructure para composición | Lógica de negocio duplicada |
| ServiceHost | APIs e infraestructura para registrar dependencias | Reglas de negocio |
| Shared | Dependencias técnicas propias | BuildingBlocks y capas de un servicio |
| BuildingBlocks | BCL y contratos simples | Shared, hosts, frameworks y servicios |

## Cómo se aplica al flujo real

```mermaid
sequenceDiagram
    participant Endpoint as API / Consumidor
    participant Caso as Application
    participant Puerto as Puerto definido por Application
    participant Adaptador as Infrastructure / Shared
    participant Recurso as Postgres, RabbitMQ o SeaweedFS

    Endpoint->>Caso: ejecuta caso de uso
    Caso->>Puerto: solicita operación abstracta
    Puerto->>Adaptador: implementación registrada por el host
    Adaptador->>Recurso: usa protocolo técnico
    Recurso-->>Adaptador: respuesta técnica
    Adaptador-->>Caso: resultado del puerto
    Caso-->>Endpoint: resultado de aplicación
```

El caso de uso no construye un cliente de RabbitMQ ni usa `RetoDbContext` directamente; declara lo que necesita mediante un puerto. El host decide qué adaptador concreto se registra.

## Qué evita una regresión

La guardia de arquitectura comprueba, entre otros límites, que:

- `Shared/Mensajeria` no tenga una referencia a `BuildingBlocks`.
- `BuildingBlocks` no declare `FrameworkReference` ni `PackageReference`.
- El núcleo de los servicios no incorpore frameworks técnicos.

La prueba está en [GuardiaArquitecturaTests.cs](../../tests/Reto.Tests/GuardiaArquitecturaTests.cs). El razonamiento completo y los puertos extraídos están en el [diseño hexagonal transversal](../../openspec/changes/arquitectura-hexagonal-transversal/design.md).

> DIP no significa una interfaz por cada clase. Aquí se introducen puertos en los límites donde existe una variación técnica real: persistencia, almacenamiento, publicación y autenticación.
