# Cómo estudiar esto para la entrevista

No es documentación del proyecto — es material de estudio. Objetivo: que
puedas **discutir criterio**, no recitar código. El evaluador va a intentar
romperlo (buscar el hueco que no consideraste), va a chequear que entendiste
el enunciado (no que lo copiaste) y te va a hacer preguntas trampa (dos
caminos válidos, ¿por qué elegiste este?). Cada diagrama de acá está armado
para esa conversación, no para explicar "qué hace el código".

## Orden de lectura (general → detalle)

| # | Diagrama | Responde | Si te preguntan... |
|---|---|---|---|
| 1 | [`01-arquitectura.drawio`](01-arquitectura.drawio) | ¿Qué componentes hay y cómo se hablan? | "Dibujame la arquitectura" — punto de partida de casi cualquier entrevista |
| 2 | [`00-mapa-de-caminos.md`](00-mapa-de-caminos.md) | Lista completa de ~35 escenarios, con dónde está probado cada uno | "¿Qué pasa si...?" — la pregunta trampa más común, ya la tenés tabulada |
| 3 | [`02-flujo-feliz.drawio`](02-flujo-feliz.drawio) | Secuencia completa, request → correo, con SPs y colas exactas | "Caminá conmigo un request de punta a punta" |
| 4 | [`03-flujo-rechazo.drawio`](03-flujo-rechazo.drawio) | Qué cambia (y qué NO) cuando el mismo periodo se sube dos veces | "¿Y si subo el mismo archivo dos veces?" — la comparación, no una lista separada |
| 5 | [`04-maquina-estados.drawio`](04-maquina-estados.drawio) | 8 estados, transiciones cerradas, por qué 3 terminales sin `Notificado` | "¿Por qué no simplificaste a menos estados?" |
| 6 | [`05-modelo-datos.drawio`](05-modelo-datos.drawio) | Las 6 tablas, FKs, y quién tiene **propiedad de escritura** de cada una | "¿No debería ser database-per-service?" (C10) |
| 7 | [`06-mensajeria.drawio`](06-mensajeria.drawio) | Topología real de RabbitMQ: 2 exchanges, 6 colas, TTL de reintento, DLX | "¿Qué pasa si un mensaje falla 10 veces?" / "¿por qué no Kafka?" (C17) |
| 8 | [`07-seguridad-jwt.drawio`](07-seguridad-jwt.drawio) | Login → claims → validación en 2 capas → rotación de refresh | "Mostrame cómo evitás que alguien escale privilegios" |
| 9 | [`08-despliegue-docker.drawio`](08-despliegue-docker.drawio) | Qué puerto es público, qué es solo red interna, non-root, volúmenes | "¿Cómo lo asegurarías en producción?" |

`01`, `02`, `03` ya tenían versión Mermaid (`.md` con el mismo número) —
quedan como respaldo de texto; los `.drawio` son la versión para pantalla
compartida o pizarra.

## Las 17 decisiones documentadas (C1–C17)

No están acá — viven en
[`openspec/changes/carga-masiva-microservicios/design.md`](../../openspec/changes/carga-masiva-microservicios/design.md),
con la cita exacta del enunciado que genera cada contradicción y la evidencia
que sostiene la resolución elegida. **Es la fuente más probable de preguntas
trampa** porque cada una es un punto donde el enunciado es ambiguo o
contradictorio a propósito — el evaluador sabe que están ahí. Resumen en la
tabla del [`README.md`](../../README.md#decisiones-de-diseño) raíz.

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
