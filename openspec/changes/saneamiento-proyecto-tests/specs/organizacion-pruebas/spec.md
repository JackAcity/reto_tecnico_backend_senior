## ADDED Requirements

### Requirement: Nombre del proyecto de test refleja su alcance real
El proyecto de test del repo SHALL tener un nombre que refleje que cubre los 5
servicios (Auth, Control, Notificaciones, Gateway, CargaMasiva) y
BuildingBlocks compartido, no solo uno de ellos.

#### Scenario: Proyecto de test único para todo el repo
- **WHEN** se lista la solución (`reto_tecnico_backend_senior.sln`)
- **THEN** el proyecto de test se llama `Reto.Tests`, no `CargaMasiva.Tests`

#### Scenario: Namespace consistente con el nombre del proyecto
- **WHEN** se inspecciona cualquier clase de test en `tests/Reto.Tests/`
- **THEN** su namespace es `Reto.Tests`

### Requirement: Sin tests placeholder sin valor
El proyecto de test SHALL NOT contener archivos de test generados por scaffold
que no ejerciten ningún comportamiento real.

#### Scenario: Placeholder del scaffold eliminado
- **WHEN** se lista el contenido de `tests/Reto.Tests/`
- **THEN** no existe ningún archivo `UnitTest1.cs` ni una clase de test con un
  método vacío sin aserciones

### Requirement: El rename no cambia comportamiento de los tests existentes
Renombrar el proyecto y su namespace SHALL preservar exactamente los mismos
casos de prueba y aserciones que existían antes del cambio.

#### Scenario: Misma cantidad de tests, todos en verde
- **WHEN** se corre `dotnet test` sobre `tests/Reto.Tests` después del rename
- **THEN** el número de tests ejecutados es igual al número de tests que
  existían en `tests/CargaMasiva.Tests` menos el placeholder eliminado, y
  todos pasan
