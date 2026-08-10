## ADDED Requirements

### Requirement: Application y Domain acceden a datos solo vía puertos propios
Ningún tipo dentro de `Application` o `Domain` de ningún servicio SHALL
referenciar un tipo concreto de infraestructura de datos (`DbContext` de EF,
`SqlClient`, `Npgsql`) directamente. Todo acceso a datos pasa por una interfaz
definida en `Application` o `Domain` e implementada en `Infrastructure`.

#### Scenario: ManejadorCarga usa un puerto, no RetoDbContext
- **WHEN** se inspecciona el constructor de `CargaMasiva.Application.ManejadorCarga`
- **THEN** ninguno de sus parámetros es `RetoDbContext` ni ningún otro tipo de
  `Microsoft.EntityFrameworkCore`; el acceso a datos pasa por `IRepositorioCargas`

#### Scenario: El puerto es angosto (ISP)
- **WHEN** se inspecciona `IRepositorioCargas`
- **THEN** solo expone las operaciones que `ManejadorCarga` efectivamente usa
  (obtener carga, obtener periodos, agregar errores, guardar cambios) — no un
  repositorio genérico con todos los métodos posibles sobre `CargaArchivo`

### Requirement: El adaptador EF vive en Infrastructure
La implementación concreta de cada puerto de acceso a datos SHALL vivir en la
capa `Infrastructure` del servicio correspondiente, nunca en `Application` ni
en el proyecto/carpeta de entrada (`Api`/`Endpoints`).

#### Scenario: Implementación de IRepositorioCargas en CargaMasiva.Infrastructure
- **WHEN** se busca la clase que implementa `IRepositorioCargas`
- **THEN** está en el proyecto `CargaMasiva.Infrastructure`, usa `RetoDbContext`
  internamente, y no es referenciada por ningún tipo de `CargaMasiva.Application`
