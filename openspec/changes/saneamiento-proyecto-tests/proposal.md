## Why

La auditoría `dotnet-audit` sobre todo el repo encontró que el único proyecto de
test (`tests/CargaMasiva.Tests`) ya no refleja su alcance real: contiene pruebas
de los 5 servicios (`AutenticacionTests` = Auth, `GatewayTests` = Gateway,
`CorrelationIdTests`/`GlobalExceptionHandlerTests` = BuildingBlocks compartido,
además de las de CargaMasiva), pero su nombre sugiere que solo cubre CargaMasiva.
Además tiene un archivo placeholder del scaffold de xUnit (`UnitTest1.cs`, método
`Test1()` vacío) sin ningún valor. Se resuelve ahora, antes de que
`arquitectura-hexagonal-transversal` agregue tests nuevos (guardia de
arquitectura, `Result<T>`, puerto de datos) a este mismo proyecto — si el rename
pasa después, esos archivos nuevos nacen con el nombre/namespace viejo y hay que
moverlos.

## What Changes

- Eliminar `tests/CargaMasiva.Tests/UnitTest1.cs`.
- Renombrar el proyecto `tests/CargaMasiva.Tests` → `tests/Reto.Tests`: carpeta,
  `.csproj`, namespace de todas las clases existentes, referencia en el `.sln`.
- Sin cambios de comportamiento: mismos tests, mismas aserciones, solo cambia
  dónde viven y cómo se llaman su proyecto/namespace.

## Capabilities

### New Capabilities
- `organizacion-pruebas`: el proyecto de test del repo tiene un nombre que
  refleja su alcance real (todos los servicios, no solo uno) y no contiene
  archivos placeholder sin valor.

### Modified Capabilities
(ninguna — no cambia ningún requisito de negocio ya especificado en
`carga-masiva-microservicios`, solo la organización del proyecto de test)

## Impact

- **Código**: `tests/CargaMasiva.Tests/*` → `tests/Reto.Tests/*` (rename de
  carpeta, `.csproj` y namespace), `reto_tecnico_backend_senior.sln` (referencia
  al proyecto actualizada).
- **Sin impacto** en `src/` (cero cambio en código de producción), contratos
  HTTP, esquema Postgres, topología RabbitMQ, ni en los changes
  `carga-masiva-microservicios` o `arquitectura-hexagonal-transversal` (que
  todavía no tiene `tasks.md`, sin conflicto de archivos en progreso).
