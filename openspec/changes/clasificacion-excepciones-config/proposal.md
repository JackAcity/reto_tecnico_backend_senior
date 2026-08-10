## Why

`dotnet-audit` sobre el estado actual (post `arquitectura-hexagonal-transversal`)
encontró que `GlobalExceptionHandler` (`src/BuildingBlocks/ServiceDefaults.cs`)
clasifica **cualquier** `InvalidOperationException` como 400 con `ex.Message`
expuesto al cliente. Ese mismo tipo de excepción se usa hoy para dos cosas muy
distintas: `TransicionInvalidaException` (regla de negocio, mensaje pensado para
el cliente — la excepción que motivó la regla original) y validación de
configuración faltante al arrancar/primer uso (`Topologia.Validar`,
`ServicioAutenticacion.LlaveDeFirma`, `OpcionesSmtp.Validar`,
`AlmacenSeaweedFs`, `Auth.Api/Program.cs`, `CargaMasiva.Api/Program.cs`,
`Gateway/Program.cs`). Un `RabbitMq:Password` faltante en producción hoy le
devuelve al cliente que subió un archivo un `400 "Falta RabbitMq:Password."` —
un bug de configuración del servidor reportado como error del cliente, con el
nombre de la variable de entorno interna filtrado en el body.

## What Changes

- Nueva excepción `ExcepcionDeConfiguracion` (`BuildingBlocks`) para
  configuración requerida faltante en un punto alcanzable durante un request
  HTTP en curso. `GlobalExceptionHandler` la matchea antes que el case
  combinado de `InvalidOperationException` y responde 500 sin exponer
  `ex.Message` — igual que hoy ya ocurre para cualquier excepción no
  clasificada.
- Los dos únicos sitios de config faltante alcanzables por HTTP —
  `OpcionesRabbit.Validar` (`Mensajeria/Topologia.cs`, llamado desde
  `PublicadorRabbit.CanalAsync` dentro de `POST /cargas`) y
  `ServicioAutenticacion.LlaveDeFirma` (`Auth.Api`, dentro de
  `POST /auth/login`) — pasan de lanzar `InvalidOperationException` a lanzar
  `ExcepcionDeConfiguracion`. Los demás sitios de config faltante (arranque de
  proceso, `BackgroundService`) no se tocan: no pasan por el middleware HTTP
  (ver design.md D2).
- `TransicionInvalidaException` y el resto de excepciones ya cubiertas
  (`ArgumentException`, `UnauthorizedAccessException`, `KeyNotFoundException`,
  y cualquier otra `InvalidOperationException` no marcada) mantienen su
  contrato actual sin cambios.
- Se agregan tests determinísticos en `GlobalExceptionHandlerTests.cs` que
  reproducen el caso real (config faltante → 500 sin fuga del nombre de la
  clave) y confirman que `TransicionInvalidaException` real sigue en 400.

## Capabilities

### New Capabilities
- `clasificacion-excepciones-globales`: el manejador de excepciones global
  distingue excepciones de negocio (pensadas para el cliente) de excepciones
  de configuración/infraestructura (no pensadas para el cliente), y solo las
  primeras exponen `ex.Message` con un status distinto de 500.

### Modified Capabilities
(ninguna — no hay spec archivada de manejo de excepciones todavía; el
comportamiento previo de `GlobalExceptionHandler` no estaba formalizado en
`openspec/specs/`)

## Impact

- **Código**: `src/BuildingBlocks/ExcepcionDeConfiguracion.cs` (nuevo),
  `src/BuildingBlocks/ServiceDefaults.cs` (clasificación en
  `GlobalExceptionHandler`), `src/Shared/Mensajeria/Topologia.cs`
  (`OpcionesRabbit.Validar`), `src/Services/Auth/Auth.Api/Application/
  ServicioAutenticacion.cs` (`LlaveDeFirma`).
- **Tests**: `tests/Reto.Tests/GlobalExceptionHandlerTests.cs` — 2 casos
  nuevos (`ExcepcionDeConfiguracion_Da500_SinFiltrarElMensaje`,
  `TransicionInvalidaException_Real_Da400_ConSuMensaje`), sin romper los
  existentes.
- **Sin impacto** en contratos HTTP externos ya cubiertos por
  `ArgumentException`, `UnauthorizedAccessException`, `KeyNotFoundException` o
  `TransicionInvalidaException` — ninguno de esos casos cambia de status ni de
  exposición de mensaje. `CargaMasiva.Domain` queda sin cambios (alternativa
  descartada, ver design.md D1).
