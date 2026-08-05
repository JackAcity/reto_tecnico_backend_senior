# Tareas — sprint de ~30 h (miércoles 05-ago → jueves 06-ago tarde)

Fuente de verdad del progreso. Marcar `[x]` al completar.

## Bloque 1 — Infraestructura verde (h 0–2) ⚠️ PRIMERO, invierte el riesgo

- [x] 1.1 `.gitignore` (con `.env`, `bin/`, `obj/`) + `.env.example`
- [x] 1.2 `Reto.slnx` + 5 proyectos web mínimos (Gateway, Auth, Control, CargaMasiva, Notificaciones) + `BuildingBlocks`
- [x] 1.3 Endpoint `/health` en los 5 (`AddHealthChecks`)
- [x] 1.4 `Dockerfile` por servicio (multi-stage, `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`)
- [x] 1.5 `docker-compose.yml`: postgres, rabbitmq(-management), seaweedfs, mailpit + los 5 servicios, con `healthcheck` y `depends_on: condition: service_healthy`
- [x] 1.6 **Verificación: `docker compose up` → 9 contenedores healthy, 5 `/health` en 200** ✅ 05-ago 21:20

## Bloque 2 — Datos (h 2–4)

- [x] 2.1 Entidades de dominio + `DbContext` (EF Core 10 + Npgsql) — `src/Shared/Persistencia`
- [x] 2.2 Migración inicial; `Control` es dueño del esquema y migra al arrancar (C11)
- [x] 2.3 SP `sp_resolver_periodo` — advisory lock + verificación de duplicidad (C2, C3, C9)
- [x] 2.4 SP `sp_insertar_data_procesada` — inserción masiva set-based con `unnest`
- [x] 2.5 Índice único sobre `(Periodo, CodigoProducto)` (C5 — clave compuesta, decidida 05-ago) + índice único parcial de periodo activo
- [x] 2.6 Seed de usuario para login (lo siembra Auth); `scripts/sql/esquema.sql` exportado
- [x] 2.7 **Verificación: `down -v` + `up` desde cero → migraciones aplicadas por Control,
      usuario semilla creado por Auth, 2 SPs en el motor, 20/20 tests verdes** ✅ 05-ago 22:05

## Bloque 3 — Auth (h 4–6)

- [ ] 3.1 `POST /auth/login` → JWT Bearer con claims (`sub`, `email`, `role`)
- [ ] 3.2 Hash de contraseña con `PasswordHasher<T>` (sin ASP.NET Identity completo)
- [ ] 3.3 `POST /auth/refresh` con rotación de refresh token persistido
- [ ] 3.4 Claim/policy `carga:masiva` (el enunciado exige validar permiso de carga)

## Bloque 4 — Gateway (h 6–7)

- [ ] 4.1 YARP: rutas a los 4 servicios
- [ ] 4.2 Validación de JWT en el gateway
- [ ] 4.3 `AddRateLimiter` + `RateLimiterPolicy` por ruta, particionado por `sub` **(obligatorio)**
- [ ] 4.4 Límites de body en los **tres** niveles: Kestrel, form options, YARP (C12)

## Bloque 5 — Control / Publicador (h 7–9)

- [ ] 5.1 `POST /cargas` multipart: valida extensión `.xlsx` y tamaño máximo configurable
- [ ] 5.2 Subida a SeaweedFS (filer HTTP API) + auditoría de quién y cuándo
- [ ] 5.3 `INSERT CargaArchivo` estado `Pendiente`
- [ ] 5.4 Publica en exchange topic → cola `carga_masiva`; si falla → `Fallida` (C7)
- [ ] 5.5 `GET /cargas` (historial) y `GET /cargas/{id}` (detalle + errores auditados)

## Bloque 6 — CargaMasiva ⭐ EL NÚCLEO (h 9–14)

- [ ] 6.1 Consumidor con prefetch, ack manual, DLX + cola de reintento
- [ ] 6.2 Estado → `EnProceso`; descarga desde SeaweedFS
- [ ] 6.3 Lectura streaming del Excel (`ExcelDataReader`), normalización y defaults
- [ ] 6.4 Filas vacías descartadas; columnas vacías → valor por defecto
- [ ] 6.5 Validación de periodo **excluyendo el propio `IdCarga`** (C2), parcial por periodo (C3)
- [ ] 6.6 Deduplicación **intra-lote** + contra base (C4), primera ocurrencia gana
- [ ] 6.7 Inserción masiva vía SP; estados `Cargado` → `Finalizado`
- [ ] 6.8 Fallidos a `DetalleCargaError` con fila, columna, regla y valor crudo
- [ ] 6.9 Publica en cola `notificaciones`
- [ ] 6.10 **TEST DETERMINISTA: el archivo de muestra produce exactamente 154 insertados / 46 `Existente`**
      (clave `(Periodo, CodigoProducto)`. Medido sobre el `.xlsx` real el 05-ago: 200 filas,
      116 códigos distintos, 154 pares distintos, 35 códigos repetidos dentro del mismo periodo,
      36 cruzando periodos. El escenario de clave global — 116/84 — se documenta en el README
      como alternativa descartada, no se implementa.)

## Bloque 7 — Notificaciones (h 14–15)

- [ ] 7.1 Consumidor de `notificaciones`
- [ ] 7.2 Correo con MailKit → MailHog, plantilla con resumen (insertados / rechazados)
- [ ] 7.3 Estado → `Notificado`
- [ ] 7.4 Configuración SMTP por variables de entorno **(obligatorio)**

## Bloque 8 — Entregables (h 15–17)

- [ ] 8.1 Colección Postman de todos los endpoints, con variable de entorno para el JWT
- [ ] 8.2 `README.md` propio: arquitectura, decisiones, cómo levantar, trade-offs
- [ ] 8.3 Matriz de trazabilidad: cada requisito "obligatorio" → dónde está implementado
- [ ] 8.4 `scripts/sql/` con esquema y procedimientos
- [ ] 8.5 Fixtures sucios (`samples/`) demostrando defaults y filas vacías

## Bloque 9 — Video (h 17–18)

- [ ] 9.1 Guion de 5 min: login → upload → estados por polling → correo en MailHog
- [ ] 9.2 Mostrar **el caso rechazado** (mismo periodo dos veces) y la tabla de auditoría
- [ ] 9.3 Grabar, subir, enlazar en el README

## Opcional — solo si el bloque 8 cierra con holgura

- [ ] O.1 Cliente React (Vite): login, upload, historial, detalle
- [ ] O.2 Workflow de GitHub Actions (`dotnet build` + el test) — **solo si sale verde**
