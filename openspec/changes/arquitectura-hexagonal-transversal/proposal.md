## Why

Comparamos el reto contra 4 fuentes (repo `.net-api-hexagonal-skeleton`, MS Learn
*arquitecturas de aplicaciones web comunes*, netmentor.es *arquitectura hexagonal* y
netmentor.es *core-driven-architecture*) y contra el propio estándar ya documentado
del usuario (skill `dotnet-clean-style`). Aparecen 3 desviaciones reales, no
cosméticas: `ManejadorCarga` (Application) depende del `DbContext` concreto en vez de
un puerto (viola DIP, el único caso de acceso a datos sin puerto en el reto); no hay
`Result<T>` para errores de negocio esperados (todo pasa por excepción o por
transición de estado silenciosa); y 3 de los 4 microservicios (Auth, Control,
Notificaciones) no tienen separación de capas ni siquiera en carpetas, a diferencia de
CargaMasiva. El usuario va a defender este proyecto en entrevista y no quiere que
ninguna de estas desviaciones quede como algo que tenga que justificar sobre la
marcha — se resuelven ahora, no se documentan como excepción aceptada.

## What Changes

- Reorganizar Auth.Api, Control.Api y Notificaciones.Api en carpetas por capa
  (`Domain/`, `Application/`, `Infrastructure/`, `Endpoints/`) donde el alcance actual
  ya lo justifique según el criterio de `dotnet-clean-style` §1; si algún servicio es
  genuinamente demasiado chico para que la separación en carpetas aporte claridad, se
  documenta esa decisión igual que las decisiones `C1..C17` de `carga-masiva-microservicios`
  — no se fuerza estructura por uniformidad.
- Cerrar el gap de DIP en `CargaMasiva.Application`: reemplazar la dependencia directa
  de `ManejadorCarga` sobre `RetoDbContext` por un puerto angosto (ISP) con las
  operaciones que realmente usa (leer carga por id, transicionar estado, guardar
  `CargaPeriodo`, agregar `DetalleCargaError`), implementado en `CargaMasiva.Infrastructure`.
- Introducir `Result<T>` (nombre técnico, no de dominio — decisión final de naming en
  design.md) para los casos de uso donde un fallo es un resultado de negocio esperado
  (ej. carga rechazada/bloqueada), reservando excepciones para lo verdaderamente
  excepcional — consistente con el `IExceptionHandler` global que ya existe.
- Documentar en `design.md`, con un ejemplo concreto, cómo se agrega un nuevo tipo de
  archivo de entrada, un nuevo tipo de reporte o un nuevo motor de base de datos
  destino usando el mecanismo de puertos que ya existe (`ILectorExcel`,
  `IInsertadorMasivo`) — sin construir un framework de plugins nuevo (YAGNI): la
  extensibilidad es el mismo patrón de puertos aplicado consistentemente, no una
  capa adicional.
- Agregar una prueba determinística de guarda arquitectónica (reflection sobre los
  assemblies ya referenciados en `tests/Reto.Tests`, sin dependencia nueva)
  que falla si `Application`/`Domain` de cualquier servicio referencia un tipo
  concreto de infraestructura (EF Core, Npgsql, RabbitMQ.Client, etc.) — es el "test
  determinístico" que exige el flujo de OpenSpec y a la vez la garantía permanente de
  que la extensibilidad por puertos no se puede romper sin que el build lo avise.

## Capabilities

### New Capabilities
- `capas-por-microservicio`: separación física (carpetas o proyectos, según criterio
  de tamaño) de Domain/Application/Infrastructure/Endpoints en Auth, Control y
  Notificaciones.
- `puertos-acceso-datos`: todo acceso a datos desde Application/Domain pasa por un
  puerto definido en esa misma capa; ningún tipo concreto de infraestructura se
  referencia desde Application/Domain en ningún servicio.
- `resultado-sin-excepciones`: los casos de uso con fallos de negocio esperados
  devuelven `Result<T>` en vez de lanzar excepción o transicionar estado en silencio.
- `guardia-arquitectura-dip`: prueba automatizada que verifica en cada build que
  Application/Domain de los 4 servicios no referencian ensamblados de infraestructura
  concreta.

### Modified Capabilities
(ninguna — este change no modifica requisitos de negocio ya especificados en
`carga-masiva-microservicios`, solo la forma en que el código los implementa)

## Impact

- **Código**: `src/Services/Auth/Auth.Api/*`, `src/Services/Control/Control.Api/*`,
  `src/Services/Notificaciones/Notificaciones.Api/*` (reorganización en `Application/`
  e `Infrastructure/`, puertos nuevos),
  `src/Services/CargaMasiva/CargaMasiva.Application/ManejadorCarga.cs` (nuevo puerto,
  retorno `Resultado<EstadoCarga>`),
  `src/Services/CargaMasiva/CargaMasiva.Infrastructure/*` (nuevo adaptador),
  `src/BuildingBlocks/Resultado.cs` (tipo `Resultado`/`Resultado<T>` compartido — nombre
  final decidido en design.md §D3, no `Result<T>` como decía la versión original de este
  proposal), `src/Shared/Mensajeria/Publicador.cs` (`IPublicador.PublicarAsync` retorna
  `Task<Resultado>` en vez de lanzar — no listado originalmente, es consecuencia directa
  de §D4).
- **Tests**: `tests/Reto.Tests/*` (nuevos casos para los puertos de datos de los 4
  servicios, para `Resultado`/`Resultado<T>`, y la guardia de arquitectura —
  `GuardiaArquitecturaTests.cs`); ajustes en `RegistroDeCargaTests.cs`,
  `AutenticacionTests.cs` y `PublicadorRabbitTests.cs` para la nueva forma de
  construir los casos de uso y el nuevo contrato de `IPublicador` (sin cambiar
  aserciones de comportamiento salvo donde el propio contrato cambió).
- **Sin impacto** en contratos HTTP externos, esquema Postgres, topología RabbitMQ ni
  en `openspec/changes/carga-masiva-microservicios` (ese change no se toca).
