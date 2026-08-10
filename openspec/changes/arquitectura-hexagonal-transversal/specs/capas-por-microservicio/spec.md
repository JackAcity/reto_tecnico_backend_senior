## ADDED Requirements

### Requirement: Auth, Control y Notificaciones separan Application de Infrastructure
Cada uno de los servicios `Auth.Api`, `Control.Api` y `Notificaciones.Api`
SHALL organizar su código de negocio en una carpeta `Application/` y su
acceso a infraestructura en una carpeta `Infrastructure/`, dentro del mismo
proyecto (sin partir en `.csproj` separados).

#### Scenario: ServicioAutenticacion vive en Application
- **WHEN** se busca `ServicioAutenticacion` en el árbol de archivos
- **THEN** está en `src/Services/Auth/Auth.Api/Application/`

#### Scenario: ServicioCargas y ConsultaCargas viven en Application
- **WHEN** se busca `ServicioCargas` y `ConsultaCargas` en el árbol de archivos
- **THEN** ambos están en `src/Services/Control/Control.Api/Application/`

#### Scenario: ManejadorNotificacion vive en Application
- **WHEN** se busca `ManejadorNotificacion` en el árbol de archivos
- **THEN** está en `src/Services/Notificaciones/Notificaciones.Api/Application/`

### Requirement: Los 3 servicios usan puertos propios de acceso a datos
Cada uno de los 3 servicios SHALL tener su propio puerto de acceso a datos en
`Application/`, implementado por un adaptador en `Infrastructure/`, en vez de
recibir `RetoDbContext` directo en sus casos de uso.

#### Scenario: Auth usa IRepositorioUsuarios
- **WHEN** se inspecciona el constructor de `ServicioAutenticacion`
- **THEN** depende de `IRepositorioUsuarios`, no de `RetoDbContext`

#### Scenario: Control usa IRepositorioCargas e IConsultaCargas
- **WHEN** se inspeccionan los constructores de `ServicioCargas` y `ConsultaCargas`
- **THEN** `ServicioCargas` depende de `IRepositorioCargas` (comando) y
  `ConsultaCargas` depende de `IConsultaCargas` (lectura) — ninguno de
  `RetoDbContext` directo

#### Scenario: Notificaciones usa IRepositorioNotificaciones
- **WHEN** se inspecciona el constructor de `ManejadorNotificacion`
- **THEN** depende de `IRepositorioNotificaciones`, no de `RetoDbContext`

### Requirement: El cambio de capas no altera comportamiento observable
Reorganizar en carpetas y detrás de puertos SHALL preservar exactamente el
comportamiento HTTP y de mensajería existente de los 3 servicios.

#### Scenario: Specs de comportamiento existentes siguen pasando
- **WHEN** se corren los tests de comportamiento ya existentes para Auth,
  Control y Notificaciones (login, rotación, subida de carga, notificación)
- **THEN** todos pasan sin modificar sus aserciones
