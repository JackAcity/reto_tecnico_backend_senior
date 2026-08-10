## ADDED Requirements

### Requirement: Excepción de configuración marcada explícitamente no expone su mensaje
El manejador de excepciones global SHALL responder con status 500 y sin
exponer `ex.Message` cuando la excepción es `ExcepcionDeConfiguracion`
(`BuildingBlocks`), aunque su tipo base (`InvalidOperationException`)
coincida con el de una excepción de negocio que sí expone su mensaje.

#### Scenario: ExcepcionDeConfiguracion no expone el nombre de la config faltante
- **WHEN** el manejador recibe un `ExcepcionDeConfiguracion("Falta RabbitMq:Password.")`
- **THEN** la respuesta tiene status 500 y `detail` NO contiene `"RabbitMq"` ni
  `"Password"`

#### Scenario: InvalidOperationException de negocio (no marcada) sigue siendo 400
- **WHEN** el manejador recibe un `InvalidOperationException("mensaje pensado para el cliente")`
  que NO es `ExcepcionDeConfiguracion`
- **THEN** la respuesta tiene status 400 y `detail` igual a ese mensaje —
  el contrato previo para este caso no cambia

### Requirement: Sitios de configuración alcanzables por HTTP usan la marca
Toda validación de configuración requerida que se ejecute dentro de un
request HTTP en curso (no en el arranque del proceso ni dentro de un
`BackgroundService`) SHALL lanzar `ExcepcionDeConfiguracion` en vez de
`InvalidOperationException` genérica.

#### Scenario: RabbitMq mal configurado durante una subida de carga
- **WHEN** `POST /cargas` dispara `ServicioCargas.RegistrarAsync` →
  `PublicadorRabbit.CanalAsync` → `OpcionesRabbit.Validar()`, y `RabbitMq:Password`
  falta en la configuración
- **THEN** el cliente recibe 500 sin el nombre `RabbitMq:Password` en el body,
  no 400

#### Scenario: Jwt:Key corta durante un login
- **WHEN** `POST /auth/login` dispara `ServicioAutenticacion.LlaveDeFirma` con
  una clave de menos de 32 bytes
- **THEN** el cliente recibe 500 sin el detalle "Jwt:Key debe tener al menos
  32 caracteres" en el body, no 400
