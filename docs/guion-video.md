# Guion del video (máx. 5 min) — §7.6, obligatorio

## Antes de grabar — checklist

1. **Reset completo**, para que el camino feliz dé el resultado determinista (nadie ya reclamó los periodos 2025-01/02/03):
   ```bash
   docker compose down -v
   docker compose up -d --wait
   ```
   Esperar a que los 9 contenedores queden `healthy` (`docker compose ps`).
2. Abrir de antemano, en pestañas separadas: Postman (con la colección + environment de `postman/` importados), RabbitMQ management (http://localhost:15672, usuario/password de `.env`), Mailpit (http://localhost:8025).
3. Tener a mano `samples/carga_masiva_productos.xlsx`.
4. Micrófono probado. Cerrar notificaciones/Slack antes de grabar.

## Guion cronometrado

**0:00–0:25 — Intro**
"Sistema de carga masiva distribuida: Gateway, Auth, Control, CargaMasiva y
Notificaciones, comunicados por RabbitMQ, con SeaweedFS para el archivo y
Postgres compartido." Mostrar `docker compose ps` — los 9 contenedores healthy.

**0:25–0:55 — Login**
Postman → "Auth → Login (admin — carga:masiva)". Mostrar la respuesta 200 con
el `accessToken`. Mencionar: rotación de refresh token, policy `carga:masiva`
por claim.

**0:55–1:20 — Subida (primera vez, periodos libres)**
"Cargas → Subir archivo (.xlsx real)", con `samples/carga_masiva_productos.xlsx`
adjunto. Mostrar 201, estado `Pendiente`, guardar el `idCarga`.

**1:20–2:15 — Polling de estados**
"Cargas → Detalle" 2-3 veces seguidas (Postman: botón Send repetido). Narrar
mientras avanza: `Pendiente` → `EnProceso` → `Finalizado` → `Notificado`. En el
último Send, señalar en el JSON: `filasInsertadas: 154`, `filasRechazadas: 46`
— "es el resultado determinista de la clave compuesta (Periodo, CodigoProducto),
documentado en design.md §C5."

**2:15–2:45 — Correo real**
Pestaña Mailpit → abrir el correo "Carga #N finalizada" → mostrar el cuerpo con
el resumen 154/46. "MailKit real, SMTP configurable por variables de entorno."

**2:45–3:05 — Colas (obligatorio mostrar la consola)**
Pestaña RabbitMQ management → pestaña Queues → señalar `carga_masiva` y
`notificaciones`, con sus colas de reintento y de muertos al lado.

**3:05–4:00 — El caso rechazado (mismo periodo dos veces)**
Volver a Postman → "Subir archivo (.xlsx real)" **con el mismo archivo otra
vez**. 201 igual (Control no sabe todavía que va a rechazarse — eso lo decide
CargaMasiva). Polling de nuevo 1-2 veces hasta `Rechazada`. Abrir el detalle
completo: `filasInsertadas: 0`, `filasRechazadas: 200`, y el arreglo `errores`
— señalar el motivo `PeriodoYaCargado` repetido. "Los tres periodos del
archivo ya estaban `Finalizado` por la carga anterior — se rechaza entera, sin
trabajo útil, tal como lo definimos en `maquina-estados.md`."

**4:00–4:35 — Bonus si alcanza: permiso**
Postman → "Login (consulta)" → "Subir archivo — sin permiso (403 esperado)".
"Usuario autenticado, pero sin el claim `carga:masiva` — la policy del gateway
lo rechaza antes de llegar a Control."

**4:35–5:00 — Cierre**
"95 tests automatizados, corridos contra contenedores reales. Colección
Postman completa en `postman/`, verificada con `newman`. README con la
arquitectura completa y las decisiones documentadas." Cortar.

## Después de grabar

1. Subir el video (YouTube no listado, Drive, o el medio que prefieras).
2. Enlazar en `README.md`, sección "Video" (al final, reemplaza el placeholder).
3. Marcar 9.3 en `tasks.md`.
