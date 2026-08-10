# Progreso — preparación entrevista/video

Estado de la sesión de práctica con el skill `preparar-entrevista`. Al retomar,
leer esto primero para no repetir lo ya firme y priorizar lo pendiente.

## Cubierto — quedó firme

- **C10 (base compartida, no database-per-service)** — la respuesta correcta es
  "el diagrama del enunciado la fuerza" (design.md §C10), no "es chico". El
  mecanismo que reemplaza el aislamiento físico: propiedad de escritura
  explícita por tabla (`db-schema.md`). Dato duro: `sp_resolver_periodo`
  necesita lectura+escritura en la misma transacción (§C9/§C15) — separar en
  proyección de lectura rompería esa consistencia.
- **Orden de qué colapsa primero bajo concurrencia** (1000 uploads/seg,
  hipotético): rate limiter del Gateway (10/min, política, no infra) → tu
  propio consumidor `cargamasiva` (una sola instancia, prefetch=1) → RabbitMQ
  aguanta → Postgres primary de un solo escritor (el techo real, ahí se
  reabre C10). El advisory lock de §C9 es barato y no es el cuello de botella
  — habilita escalar réplicas, no las limita.
- **C18 — lost-update en rotación de refresh token** (hallazgo propio, no
  parte del enunciado original). Dos `POST /auth/refresh` simultáneos con el
  mismo token activo podían crear dos hijos. **Ya arreglado**: `ExecuteUpdateAsync`
  con `WHERE revocado_en IS NULL` (compare-and-swap). Documentado en
  `design.md` §C18, test determinista en `AutenticacionTests.cs`. Commit
  `fix(seguridad): cierra lost-update en la rotación del refresh token`.
- **C7 — dual write / Transactional Outbox.** Sabés explicar la diferencia
  entre "reintento de mensaje ya en cola" (RabbitMQ/x-death, sí lo tenés) y
  "reintento de un publish que nunca llegó a existir" (no lo tenés — si el
  proceso muere entre el commit y el `publish()`, la carga queda `Pendiente`
  para siempre, nadie reintenta, ni siquiera un reinicio del contenedor).
  **Decisión: NO implementarlo para esta entrega** — el enunciado no lo pide
  (verificado directo contra `docs/RETO-ORIGINAL.md`, cero menciones de
  "outbox"/"saga"), y el gap no se puede disparar en una demo de
  `docker compose up` corrida una vez. Vale más saber explicarlo que
  implementarlo acá.
- **C5 — clave compuesta `(Periodo, CodigoProducto)`.** Argumento fuerte: si
  el enunciado pide "no recargar un periodo ya cargado" (Regla A, a nivel
  carga) Y "no repetir un código" (Regla B, a nivel fila) — la Regla A no
  tendría sentido con clave global, así que el enunciado mismo prueba que los
  datos están particionados por periodo. Número a citar de memoria: **154/46**
  (clave compuesta) vs **116/84** (clave global) — es el test de aceptación
  del sistema. Framing senior: `data_procesada` es una fact table con grano
  `(Periodo, CodigoProducto)` — el código identifica el producto, el precio es
  un hecho por periodo, como un tipo de cambio por fecha. El PAR sigue siendo
  único (`ux_data_procesada_periodo_codigo`); lo que no es único es el código
  solo, cruzando periodos, y es correcto que no lo sea.

## Diagramas — estado

- Los 8 `.drawio` (02–08) + `01-arquitectura.html` revisados contra el código
  real y corregidos. `01` ahora es HTML autocontenido (no drawio) — decisión
  del usuario, más portátil para pantalla compartida, sin depender de la app
  draw.io.

## Cubierto en esta ronda (modo 2, "¿por qué X y no Y?") — 2026-08-10

Todos con feedback completo dado y corrección/cita exacta señalada — repasar
antes de la entrevista real, no solo la resolución sino el número/línea
citado:

- **C1** — falta memorizar el hash del commit (`abba4ad`) + fechas EOL
  (.NET 8/9 mueren 10-nov-2026).
- **C2** — falta la mitad más filosa: implementado literal, CargaMasiva se
  autobloquea; el fix real es `IdCarga <> @idCargaActual` en el SQL, no solo
  "Control no parsea Excel".
- **C3** — falta la trampa "¿por qué no el primer periodo?": violaría el
  mandato de auditoría del enunciado.
- **C4** — falta el número (42%, 84/116 códigos duplicados, 35 intra-periodo
  vs 36 cruzando periodos) — es lo que prueba que no es un edge case teórico.
- **C6** — bien la distinción Bloqueada/Rechazada; falta el desempate mixto
  (un periodo YaCargado + otro Bloqueado → gana Bloqueada, más accionable).
- **C8** — falta la capa doble (índice compuesto + chequeo de estado antes
  de procesar) y por qué vive en el motor, no en C# (TOCTOU).
- **C9 directa** — bien el lock; falta el paso 2 del SP (libera reservas de
  cargas muertas, si no el periodo queda secuestrado para siempre).

**Hallazgo propio nuevo, no en el enunciado original:** **C19** — el usuario
señaló en vivo que `ManejadorCarga` vivía en Infrastructure con
`new ProcesadorLote(reglas)` adentro (violación de DIP + capa equivocada).
**Ya arreglado**: movido a `CargaMasiva.Application`, puertos nuevos
`ILectorExcel`/`IInsertadorMasivo`, `ProcesadorLote` inyectado. Documentado
completo en `design.md` §C19, incluido el límite honesto (Application ahora
referencia Persistencia/Almacenamiento/Mensajeria transitivamente — no es
pureza 100%, y se explica por qué no se fue más lejos). Verificado: build
14 proyectos sin error, 100/100 tests, carga real corrida contra el
contenedor reconstruido.

## Pendiente — no cubierto todavía

C11 (migraciones, un solo dueño del esquema — pregunta ya hecha, quedó
pendiente la respuesta cuando surgió C19), C12 (tres techos de tamaño de
archivo), C13 (secretos, tres escalones), C14 (hardening — no-root, firma
binaria, nosniff), C15 (CQRS una tabla), C16 (Postgres en contenedor), C17
(RabbitMQ vs Kafka — ya cubierto en profundidad fuera del modo 2, con
cuenta de costos y el ángulo de auditoría/ROI de IA vs reglas — repasar esa
conversación en vez de repetirla).

Modos del skill sin usar todavía: **1** (recorrido guiado completo),
**4** (simulacro rápido mezclando todo), **5** (ensayo del guion del video,
`docs/guion-video.md`).

## Entrega — GitHub

- Repo nuevo, propio: **https://github.com/JackAcity/reto_tecnico_backend_senior**
  (el original, `desarrollo-acity/...`, es de la empresa — sin permiso de
  escritura, se dejó como remoto `origin` de solo lectura; el nuevo es el
  remoto `jackacity`).
- **`main`** = lo que revisa el evaluador. Pusheado, fast-forward desde
  `develop`, solo código + docs del entregable (README, `openspec/changes/...`,
  `docs/explicacion/*`, `docs/guion-video.md`, `docs/RETO-ORIGINAL.md`). Sin
  `.claude/` (tooling de Claude Code) ni `progreso-entrevista.md` (este mismo
  archivo) ni `docs/propuesta-jack/` (borrador viejo) — esos quedan SOLO en
  `develop`.
- **`develop`** = todo, incluido este archivo y el modo entrevistador.
- Checklist de entregables (§7 del enunciado, `docs/RETO-ORIGINAL.md:354-368`):
  código en GitHub ✅, README ✅, instrucciones de despliegue ✅, scripts SQL
  ✅, Postman ✅ (cubre el 20% de "Frontend o Postman" del rubro, React no
  suma nota ahí), **video ✅ — enlazado en README, checklist completo**.
- Rubro de evaluación (`docs/RETO-ORIGINAL.md:325-350`): Arquitectura 25% +
  Funcionalidad 35% + Docker/DevOps 20% + Frontend-o-Postman 20% = 100%.
  CI/CD no aparece en ningún bloque — cero impacto en nota, no vale la pena
  meterle tiempo ahora.
- **Video grabado y enlazado** en `README.md` sección "Video". Bloque 9
  completo (9.1/9.2/9.3), Bloque 8 completo (8.2/8.3 recontados: ya estaban
  hechos, checkboxes desactualizados).

## Para retomar

Decir "seguimos con el modo entrevistador" o `/preparar-entrevista` — el
skill ya sabe leer este archivo primero.
