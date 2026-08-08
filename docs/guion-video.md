# Guion del video (máx. 5 min) — §7.6, obligatorio

## Antes de grabar — checklist

1. **Reset completo**, para que el camino feliz dé el resultado determinista (nadie ya reclamó los periodos 2025-01/02/03):
   ```bash
   docker compose down -v
   docker compose up -d --wait
   ```
   Esperar a que los 9 contenedores queden `healthy` (`docker compose ps`).
2. **Levantar el cliente web** (§4.2, opción mejor puntuada que Postman en §6.4):
   ```bash
   cd frontend && npm run dev
   ```
   Confirmar que abre en http://localhost:5173.
3. Abrir de antemano, en pestañas separadas: el cliente web
   (http://localhost:5173), RabbitMQ management (http://localhost:15672,
   usuario/password de `.env`), Mailpit (http://localhost:8025). Postman queda
   de respaldo (`postman/`, verificado con `newman`) — no hace falta abrirlo si
   el tiempo aprieta.
4. Tener a mano `samples/carga_masiva_productos.xlsx`.
5. Micrófono probado. Cerrar notificaciones/Slack antes de grabar.

**Credenciales** (de `.env` / `.env.example` — no rotadas, son las de demo):

| Servicio | Usuario | Password |
|---|---|---|
| Cliente web — admin (sube y consulta) | `admin@reto.local` | `Reto2026!` |
| Cliente web — consulta (solo consulta, bloque 3:40) | `consulta@reto.local` | `Consulta2026!` |
| RabbitMQ management | `reto` | `cambiar_en_local` |
| PostgreSQL (si hace falta inspeccionar la DB) | `reto` | `cambiar_en_local` |

Mailpit no pide credenciales. `docker compose ps` con los 9 `healthy` ya
confirma que todo levantó — no hace falta entrar a ningún servicio para
verificarlo, solo para los pasos que el guion explícitamente muestra en
pantalla (RabbitMQ).

## Guion cronometrado

**0:00–0:20 — Intro**
"Sistema de carga masiva distribuida: Gateway, Auth, Control, CargaMasiva y
Notificaciones, comunicados por RabbitMQ, con SeaweedFS para el archivo y
Postgres compartido." Mostrar `docker compose ps` — los 9 contenedores healthy.

**0:20–0:45 — Login (cliente web)**
Pestaña del cliente (localhost:5173) → login con `admin@reto.local`. Redirige a
Historial, vacío (reset recién hecho). "JWT con policy por claim —
`carga:masiva` distingue quién puede subir de quién solo consulta, lo vemos al
final."

**0:45–1:10 — Subida (primera vez, periodos libres)**
"Subir Excel" → adjuntar `samples/carga_masiva_productos.xlsx` → Subir.
Mostrar la tarjeta con `Carga #1 — Pendiente`. Click "Ver detalle".

**1:10–2:05 — Polling (ADVERTENCIA: el pipeline es MUY rápido, ~1-2 s de
punta a punta incluso en frío — verificado en vivo. NO va a alcanzar a
mostrarse "Pendiente" ni "EnProceso" en pantalla el tiempo suficiente para
narrarlo — probablemente ya esté en `Notificado` cuando termines de decir la
frase de abajo. No te quedes en silencio esperando que "avance": seguí
hablando y dejá que el estado cambie solo, de fondo, mientras explicás.**
Click "Ver detalle" y sin tocar nada narrar: "esto es polling real contra
`GET /cargas/{id}` cada 3 segundos, como pide el enunciado — no un reload
manual. Y miren qué rápido: publish, consume, procesa 200 filas, publica la
notificación, la consume Notificaciones, envía el correo — todo esto ya
terminó." Señalar en la tabla de periodos y en el resumen:
`filasInsertadas: 154`, `filasRechazadas: 46` — "resultado determinista de la
clave compuesta (Periodo, CodigoProducto), documentado en design.md §C5."

**2:05–2:30 — Correo real**
Pestaña Mailpit → abrir el correo "Carga #1 finalizada" → mostrar el cuerpo con
el resumen 154/46. "MailKit real, SMTP configurable por variables de entorno."

**2:30–2:50 — Colas (obligatorio mostrar la consola)**
Pestaña RabbitMQ management → pestaña Queues → señalar `carga_masiva` y
`notificaciones`, con sus colas de reintento y de muertos al lado.

**2:50–3:40 — El caso rechazado (mismo periodo dos veces)**
Volver al cliente → "Subir Excel" **con el mismo archivo otra vez**. Carga #2
en `Pendiente` (Control no sabe todavía que va a rechazarse — eso lo decide
CargaMasiva). Historial: la fila pasa sola a `Rechazada` (misma velocidad que
antes — seguir narrando, no esperar en silencio). Abrir el detalle:
`filasInsertadas: 0`, `filasRechazadas: 200`, tabla de errores con el motivo
`PeriodoYaCargado` repetido. "Los tres periodos ya estaban `Finalizado` por la
carga anterior — se rechaza entera, sin trabajo útil, tal como lo definimos en
`maquina-estados.md`."

**3:40–4:20 — Permiso: consultar sí, subir no**
Cerrar sesión → login con `consulta@reto.local`. Historial: **se ve completo**
— "autenticado, sin el claim `carga:masiva`, pero el rol se llama consulta por
algo: puede ver". Ir a "Subir Excel" e intentar subir el mismo archivo → 403
mostrado en pantalla, sin crashear. "La policy del gateway lo bloquea en el
borde, antes de llegar a Control — separar consulta de subida en dos rutas fue
un bug real que encontré probando el cliente contra el navegador, no algo que
vino así del enunciado."

**4:20–4:50 — Cierre**
"100 tests automatizados, corridos contra contenedores reales. Colección
Postman completa en `postman/`, verificada con `newman`, como alternativa si
no se usa el cliente web. README con la arquitectura completa, las decisiones
y los dos bugs de Gateway que expuso construir este cliente, documentados."
Cortar.

## Después de grabar

1. Subir el video (YouTube no listado, Drive, o el medio que prefieras).
2. Enlazar en `README.md`, sección "Video" (al final, reemplaza el placeholder).
3. Marcar 9.3 en `tasks.md`.
