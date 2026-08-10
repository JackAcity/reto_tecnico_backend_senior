## ADDED Requirements

### Requirement: El build falla si Application/Domain referencia infraestructura concreta
Una prueba automatizada SHALL verificar, en cada ejecución de la suite de
tests, que ningún tipo en `Application` o `Domain` de ninguno de los 5
servicios referencia `Microsoft.EntityFrameworkCore`, `Npgsql` o
`RabbitMQ.Client` directamente.

#### Scenario: CargaMasiva se verifica por reflection de ensamblado
- **WHEN** corre la guardia de arquitectura sobre `CargaMasiva.Application.dll`
  y `CargaMasiva.Domain.dll`
- **THEN** falla si `Assembly.GetReferencedAssemblies()` incluye
  `Microsoft.EntityFrameworkCore`, `Npgsql.dll` o `RabbitMQ.Client`

#### Scenario: Auth, Control y Notificaciones se verifican por escaneo de código fuente
- **WHEN** corre la guardia de arquitectura sobre los archivos `.cs` bajo
  `*/Application/` de Auth.Api, Control.Api y Notificaciones.Api
- **THEN** falla si algún archivo contiene `using Microsoft.EntityFrameworkCore`,
  `using Npgsql` o `using RabbitMQ.Client`

### Requirement: La guardia no depende de una librería nueva
La guardia de arquitectura SHALL implementarse con `System.Reflection` y
`System.IO` ya disponibles, sin agregar ArchUnitNET ni ninguna otra
dependencia de análisis estático.

#### Scenario: Sin nueva referencia de paquete
- **WHEN** se inspecciona el `.csproj` del proyecto de test tras agregar la
  guardia
- **THEN** no aparece ninguna referencia de paquete nueva relacionada con
  análisis de arquitectura
