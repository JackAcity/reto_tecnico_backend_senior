# Propuesta — Sistema de Carga Masiva Distribuida

## Por qué

El enunciado (`docs/RETO-ORIGINAL.md`) pide un sistema de microservicios donde un
usuario sube un Excel, este se procesa de forma asíncrona vía cola, y se notifica
por correo al finalizar. Todo dockerizado.

La rúbrica reparte: **Arquitectura 25% · Funcionalidad 35% · Docker/DevOps 20% ·
Postman-o-Frontend 20%**.

El 35% de funcionalidad **no se gana con el camino feliz**. Se gana resolviendo las
contradicciones que el propio enunciado y su archivo de muestra plantean (ver
`design.md` §Contradicciones). El archivo `samples/carga_masiva_productos.xlsx`
contiene evidencia dura de que la lectura ingenua del enunciado produce un sistema
incorrecto.

## Qué cambia

Se construye desde cero, en un único repositorio (el enunciado §7 pide
*"un repositorio"*):

| Componente | Rol | Puerto |
|---|---|---|
| `Gateway` (YARP) | JWT + rate limiting + enrutamiento | 8080 |
| `Auth` | `/auth/login`, `/auth/refresh` → JWT Bearer | interno |
| `Control` | Upload, validación de tamaño, SeaweedFS, estado `Pendiente`, publica | interno |
| `CargaMasiva` | Consume, descarga, procesa Excel, persiste, publica notificación | interno |
| `Notificaciones` | Consume, envía correo (MailKit), estado `Notificado` | interno |

Infraestructura en `docker-compose.yml`: PostgreSQL, RabbitMQ, SeaweedFS, MailHog.

## Qué NO cambia (no-goals)

Fuera de alcance por no estar en el enunciado ni en el diagrama entregado:

- **CI/CD** — cero menciones en el enunciado. La rúbrica DevOps 20% es literalmente
  *"docker-compose funcional"* + *"servicios se levantan sin errores"*.
- **Database-per-service** — el diagrama entregado muestra **una sola** caja de base
  de datos, con tres servicios accediéndola. Se respeta el diagrama.
- **Transactional Outbox** — mitigado con publicación post-commit y estado terminal
  `Fallida`. Trade-off documentado en el README.
- **Redis, service discovery, observabilidad distribuida, retención de archivos.**
- **Cliente React** — el enunciado lo marca *"opcional pero valorado"* y acepta
  colecciones Postman por el mismo 20%. Postman es el entregable comprometido;
  React solo si el backend cierra con holgura.

## Riesgo principal y su mitigación

El 20% de Docker/DevOps se pierde entero si `docker compose up` falla la noche
antes de entregar. **Mitigación: la infraestructura se levanta primero**, con los
cinco servicios respondiendo `/health` antes de escribir una sola regla de negocio.
El orden de trabajo invierte el riesgo en vez de acumularlo.
