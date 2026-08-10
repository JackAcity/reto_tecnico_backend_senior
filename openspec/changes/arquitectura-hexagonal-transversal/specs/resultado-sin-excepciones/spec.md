## ADDED Requirements

### Requirement: Fallos de negocio esperados devuelven Resultado, no excepción
Cuando un caso de uso puede terminar en un fallo de negocio esperado (no un
error de infraestructura ni un bug), SHALL comunicarlo devolviendo
`Resultado`/`Resultado<T>` en vez de lanzar una excepción o transicionar
estado sin señal explícita de retorno.

#### Scenario: Publicación fallida en RegistrarAsync ya no depende de catch genérico
- **WHEN** `IPublicador.PublicarAsync` retorna `Resultado.Fallo(...)` durante
  `ServicioCargas.RegistrarAsync`
- **THEN** la carga queda en estado `Fallida` y se devuelve `ResultadoRegistro`
  con `Error` poblado, sin que el código pase por un bloque `catch`

#### Scenario: Un bug real en el publicador ya no se confunde con un fallo esperado
- **WHEN** `IPublicador.PublicarAsync` lanza una excepción no relacionada con
  el fallo de publicación esperado (por ejemplo, un `NullReferenceException`
  por un bug)
- **THEN** la excepción se propaga sin ser capturada por `ServicioCargas`
  (ya no hay `catch (Exception ex)` genérico alrededor de la llamada)

#### Scenario: ManejadorCarga comunica su resultado terminal sin releer la base
- **WHEN** `ManejadorCarga.ProcesarAsync` termina el procesamiento de una carga
- **THEN** retorna `Resultado<EstadoCarga>` con el estado terminal alcanzado
  (`Rechazada`, `Bloqueada` o `Finalizado`), consultable sin una query aparte
  contra `carga_archivo`

### Requirement: Resultado<T> vive como tipo compartido
El tipo `Resultado<T>` (y su variante sin valor `Resultado`) SHALL vivir en
`BuildingBlocks`, disponible para los 5 servicios sin duplicación.

#### Scenario: Un solo tipo Resultado en todo el repo
- **WHEN** se busca una definición de `Resultado<T>` en el código
- **THEN** existe una única definición, en `src/BuildingBlocks`
