# Cómo estudiar esto para la entrevista

No es documentación del proyecto — es material de estudio. Objetivo: que
puedas **discutir criterio**, no recitar código. El evaluador va a intentar
romperlo (buscar el hueco que no consideraste), va a chequear que entendiste
el enunciado (no que lo copiaste) y te va a hacer preguntas trampa (dos
caminos válidos, ¿por qué elegiste este?). Cada diagrama de acá está armado
para esa conversación, no para explicar "qué hace el código".

## Galería visual renderizable en GitHub

Los diagramas siguientes son la vista que se renderiza directamente al abrir el
repositorio. Cuando corresponde, el enlace a Draw.io conserva la fuente
editable para una presentación o una modificación posterior.

| # | Diagrama | Pregunta que responde | Fuente editable |
|---:|---|---|---|
| 1 | [Arquitectura](01-arquitectura.md) | ¿Qué componentes existen y cómo se comunican? | [HTML](01-arquitectura.html) |
| 2 | [Flujo de carga](02-flujo-carga.md) | ¿Cómo pasa una carga de HTTP a correo? | [Draw.io](02-flujo-feliz.drawio) |
| 3 | [Máquina de estados](03-maquina-estados.md) | ¿Qué transiciones son válidas y cuáles son terminales? | [Draw.io](04-maquina-estados.drawio) |
| 4 | [Dependencias DIP](04-dependencias-dip.md) | ¿Qué referencias están permitidas y cuáles están prohibidas? | — |
| 5 | [Datos y propiedad](05-datos-y-propiedad.md) | ¿Quién escribe cada dato y cómo se conserva la consistencia? | [Draw.io](05-modelo-datos.drawio) |
| 6 | [Mensajería y reintentos](06-mensajeria-y-reintentos.md) | ¿Qué pasa con un mensaje que falla? | [Draw.io](06-mensajeria.drawio) |
| 7 | [Seguridad](07-seguridad-y-autorizacion.md) | ¿Cómo se autentica y autoriza cada operación? | [Draw.io](07-seguridad-jwt.drawio) |
| 8 | [Despliegue local](08-despliegue-local.md) | ¿Qué queda expuesto y qué queda en la red interna? | [Draw.io](08-despliegue-docker.drawio) |
| 9 | [Escala y observabilidad](09-escala-y-observabilidad.md) | ¿Qué rendimiento se comprobó y cuál es el límite actual? | — |

## Orden de lectura (general → detalle)

| # | Diagrama | Responde | Si te preguntan... |
|---:|---|---|---|
| 1 | [Arquitectura](01-arquitectura.md) | ¿Qué componentes hay y cómo se hablan? | "Dibujame la arquitectura" — punto de partida de casi cualquier entrevista. |
| 2 | [Mapa de caminos](00-mapa-de-caminos.md) | ¿Qué escenarios existen y dónde se prueban? | "¿Qué pasa si...?" — la respuesta tiene escenario y test asociado. |
| 3 | [Flujo de carga](02-flujo-carga.md) | ¿Cómo recorre una solicitud el sistema hasta el correo? | "Caminá conmigo un request de punta a punta". |
| 4 | [Máquina de estados](03-maquina-estados.md) | ¿Qué transiciones están cerradas y cuáles son terminales? | "¿Por qué no simplificaste a menos estados?". |
| 5 | [Dependencias DIP](04-dependencias-dip.md) | ¿Dónde se corta el acoplamiento técnico? | "¿Cómo impides Domain → Infrastructure?". |
| 6 | [Datos y propiedad](05-datos-y-propiedad.md) | ¿Qué guarda cada tabla y quién la modifica? | "¿No debería ser database-per-service?" (C10). |
| 7 | [Mensajería](06-mensajeria-y-reintentos.md) | ¿Cómo operan retry, TTL y DLQ? | "¿Qué pasa si un mensaje falla repetidamente?". |
| 8 | [Seguridad](07-seguridad-y-autorizacion.md) | ¿Cómo se emiten tokens y se autorizan rutas? | "¿Cómo evitas escalar privilegios?". |
| 9 | [Despliegue](08-despliegue-local.md) | ¿Qué puertos son públicos y cómo arranca el stack? | "¿Cómo lo operarías en local?". |
| 10 | [Escala](09-escala-y-observabilidad.md) | ¿Qué volumen se midió y cuál es el límite? | "¿Qué pasa a 5 millones de filas?". |

Los archivos Markdown son la vista renderizable en GitHub. Los enlaces Draw.io
de la galería anterior siguen siendo las fuentes editables para pizarra o
presentación; `01-arquitectura.html` es una alternativa autocontenida para
abrir directamente en un navegador.

## Las 17 decisiones documentadas (C1–C17)

No están acá — viven en
[`openspec/changes/carga-masiva-microservicios/design.md`](../../openspec/changes/carga-masiva-microservicios/design.md),
con la cita exacta del enunciado que genera cada contradicción y la evidencia
que sostiene la resolución elegida. **Es la fuente más probable de preguntas
trampa** porque cada una es un punto donde el enunciado es ambiguo o
contradictorio a propósito — el evaluador sabe que están ahí. El
[README raíz](../../README.md#decisiones-que-importan) resume las decisiones
operativas; el diseño conserva el detalle completo.

Las que más se prestan a "¿y si en vez de X hubieras hecho Y?":

- **C5** (clave compuesta vs. global) — tenés el número exacto: 154/46.
- **C10** (una base vs. database-per-service) — el diagrama del enunciado la fuerza; decilo así, no como limitación tuya.
- **C16** (Postgres en contenedor) — separá "contenedor" de "sin volumen/backup"; son dos cosas distintas.
- **C17** (RabbitMQ vs. Kafka) — sabé nombrar el eje real (broker inteligente/consumidor tonto vs. al revés), no solo "Kafka es para más escala".

## Si te preguntan algo que no está acá

Es más probable que sea del código real que de un caso no contemplado — este
sistema tiene 95 tests corridos contra contenedores reales, no mocks, y cada
fila de `00-mapa-de-caminos.md` señala el test exacto. Si dudás en vivo:
nombrá el mecanismo (idempotencia por índice único, advisory lock, DLX con
`x-death`) antes que el nombre de la clase — es lo que un evaluador de
criterio realmente está escuchando.
