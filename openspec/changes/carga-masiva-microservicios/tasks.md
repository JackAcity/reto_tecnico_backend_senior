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

- [x] 3.1 `POST /auth/login` → JWT Bearer con claims (`sub`, `email`, `role`, `jti`, `permiso`)
- [x] 3.2 Hash de contraseña con `PasswordHasher<T>` (sin ASP.NET Identity completo)
- [x] 3.3 `POST /auth/refresh` con rotación de refresh token persistido (revoca + encadena)
- [x] 3.4 Claim `permiso=carga:masiva` emitido por rol; la **policy** que lo exige se
      aplica en el gateway (tarea 4.2)
- [x] 3.5 **Verificación: login 200 con claims, credencial mala 401, refresh 200 y
      reuso del mismo refresh 401 — contra el contenedor** ✅ 05-ago 22:35

## Bloque 4 — Gateway (h 6–7)

- [x] 4.1 YARP: rutas a los 4 servicios (`/auth/*`, `/cargas/*`, `/servicios/{cargamasiva,notificaciones}/*`)
- [x] 4.2 Validación de JWT en el gateway + policy `cargaMasiva` que exige el claim `permiso`
- [x] 4.3 `AddRateLimiter` + `RateLimiterPolicy` por ruta, particionado por `sub` **(obligatorio)**
      — 60/min general, 10/min carga, 10/min login particionado por IP
- [x] 4.4 Límites de body en los **tres** niveles: Kestrel, form options, YARP (C12), de un
      único cálculo en código; falta ejercitar el 413 real cuando exista el endpoint de subida
- [x] 4.5 **Verificación: 401 sin token, 401 con token falso, 200 atravesando con permiso,
      429 por ráfaga — 6 tests contra el gateway en contenedor** ✅ 05-ago 22:55

## Bloque 5 — Control / Publicador (h 7–9)

- [x] 5.1 `POST /cargas` multipart: valida extensión `.xlsx` y tamaño máximo configurable
- [x] 5.2 Subida a SeaweedFS (filer HTTP API) + auditoría de quién y cuándo
- [x] 5.3 `INSERT CargaArchivo` estado `Pendiente`
- [x] 5.4 Publica en exchange topic → cola `carga_masiva`; si falla → `Fallida` (C7)
      — con publisher confirms, si no no habría forma de saber que falló
- [x] 5.5 `GET /cargas` (historial) y `GET /cargas/{id}` (detalle + periodos + errores auditados)
- [x] 5.6 **Verificación: subida real por el gateway → 201 `Pendiente`, archivo íntegro en
      SeaweedFS (10 006 bytes = original), 1 mensaje en `carga_masiva`, `.csv` → 400** ✅ 06-ago 08:10

## Bloque 6 — CargaMasiva ⭐ EL NÚCLEO (h 9–14)

- [x] 6.1 Consumidor con prefetch(1), ack manual, DLX + cola de reintento — tope de 3
      intentos leyendo `x-death`, después va a `carga_masiva.muertos` y la carga a `Fallida`
- [x] 6.2 Estado → `EnProceso`; descarga desde SeaweedFS
- [x] 6.3 Lectura streaming del Excel (`ExcelDataReader`), normalización y defaults
      (bug real encontrado y corregido: el stream de red no es seekable y ExcelDataReader
      lo necesita — `AlmacenSeaweedFs.DescargarAsync` ahora bufferiza antes de devolver)
- [x] 6.4 Filas vacías descartadas; columnas vacías → valor por defecto
- [x] 6.5 Validación de periodo **excluyendo el propio `IdCarga`** (C2), parcial por periodo (C3)
- [x] 6.6 Deduplicación **intra-lote** + contra base (C4), primera ocurrencia gana
- [x] 6.7 Inserción masiva vía SP; estados `Cargado` → `Finalizado` (o `Rechazada`/`Bloqueada`
      si ningún periodo quedó libre — desempate documentado en maquina-estados.md)
- [x] 6.8 Fallidos a `DetalleCargaError` con fila, columna, regla y valor crudo
- [x] 6.9 Publica en cola `notificaciones`, con el mismo `correlationId` de origen
- [x] 6.10 **TEST DETERMINISTA: el archivo de muestra produce exactamente 154 insertados / 46 `Existente`**
      — verificado dos veces contra el consumidor REAL (no solo lógica pura) en una base
      recién levantada (`docker compose down -v && up`): `data_procesada` con 154 filas,
      `detalle_carga_error` con 46 `Existente`, `carga_archivo.estado = Finalizado`.
      (clave `(Periodo, CodigoProducto)`. El escenario de clave global — 116/84 — queda
      documentado como alternativa descartada, no se implementa.) ✅ 06-ago 22:45

## Bloque 7 — Notificaciones (h 14–15)

- [x] 7.1 Consumidor de `notificaciones` — mismo patrón que CargaMasiva (prefetch 1, ack
      manual, tope de 3 intentos vía `x-death`); al agotarse NO marca `Fallida` (no existe
      esa transición desde `Finalizado` en la máquina de estados) — solo audita fuerte en
      el log y deja el mensaje en `notificaciones.muertos`
- [x] 7.2 Correo con MailKit → Mailpit, con resumen (insertados / rechazados) — los números
      se leen de `carga_archivo`, no viajan en el mensaje (misma razón que el correlationId:
      una sola fuente de verdad)
- [x] 7.3 Estado → `Notificado`
- [x] 7.4 Configuración SMTP por variables de entorno **(obligatorio)** — falla al arrancar
      si falta `Smtp:Host/Puerto/Desde` (Usuario/Password opcionales, Mailpit no los exige)
- [x] 7.5 **Verificación: subida real → `Pendiente → EnProceso → Finalizado → Notificado` →
      correo recibido en Mailpit con "Filas insertadas: 154 / Filas rechazadas: 46"** ✅ 06-ago 23:35
      (bug real encontrado y corregido en el camino: la cola `notificaciones` quedó
      declarada con argumentos distintos entre Control —imagen vieja— y CargaMasiva/
      Notificaciones —imagen nueva— tras generalizar el circuito de reintento a las dos
      colas; RabbitMQ exige argumentos idénticos y rechazó la declaración con
      `PRECONDITION_FAILED`. No era un bug de diseño: había que reconstruir los 5 servicios,
      no solo los 2 tocados — cualquier cambio a `src/Shared/Mensajeria` obliga a
      reconstruir TODO lo que declara topología, no solo el servicio editado.)

## Bloque 8 — Entregables (h 15–17)

- [x] 8.1 Colección Postman de todos los endpoints (`postman/`), con environment para el JWT
      — corrida real vía `newman` contra el stack en vivo: 15/15 requests, 12/12 asserts ✅ 06-ago
- [x] 8.2 `README.md` propio: arquitectura, decisiones, cómo levantar, trade-offs
- [x] 8.3 Matriz de trazabilidad: cada requisito "obligatorio" → dónde está implementado
      (`matriz-requisitos.md` enlazada en README §Matriz de trazabilidad)
- [x] 8.4 `scripts/sql/` con esquema y procedimientos — verificado vigente, sin migraciones
      nuevas desde Bloque 2
- [x] 8.5 Fixtures sucios (`samples/fixture-sucio.xlsx`) demostrando defaults y filas vacías
      — subida real verificada: 10 filas contadas (2 vacías descartadas sin auditar), 5
      insertadas, 5 rechazadas, los 8 motivos exactos (`PrecioInvalido`, `PeriodoRequerido`,
      `PeriodoFormatoInvalido`, `CodigoRequerido`, `Existente` ×1, `ValorPorDefectoAplicado` ×3)
      coincidiendo con lo calculado antes de correrlo ✅ 07-ago 00:48

## Bloque 9 — Video (h 17–18)

- [x] 9.1 Guion de 5 min: login → upload → estados por polling → correo en MailHog
- [x] 9.2 Mostrar **el caso rechazado** (mismo periodo dos veces) y la tabla de auditoría
- [x] 9.3 Grabar, subir, enlazar en el README

## Opcional — solo si el bloque 8 cierra con holgura

- [x] O.1 Cliente React (Vite) — spec: `specs/frontend-cliente-react.md`
  - [x] O.1.1 Scaffold Vite+React+TS, react-router-dom, vitest+testing-library
  - [x] O.1.2 API client (fetch a gateway) + AuthContext con refresh transparente
  - [x] O.1.3 Pantallas: Login, Upload, Historial (polling), Detalle (+ descarga blob)
  - [x] O.1.4 Test determinista `Login.test.tsx` verde (2/2), `tsc -b` sin errores
  - [x] O.1.5 Verificación manual contra stack real (Chrome real, no solo curl):
        login admin → historial con datos reales → detalle con periodos/errores →
        descarga del .xlsx original vía blob → subida real de `fixture-sucio.xlsx`
        (Carga #61, procesada end-to-end hasta `Notificado` en ~1 s, 5/5 igual que
        el fixture documentado) → login inválido muestra error sin navegar → usuario
        `consulta` sube y recibe 403 manejado sin crashear.
        **2 bugs reales de backend encontrados y corregidos en el camino** (ninguno
        existía en el enunciado, ambos preexistentes al frontend, expuestos por ser
        el primer cliente real en un navegador):
        1. Faltaba CORS en Gateway — declarado fuera de alcance en README/design.md
           asumiendo Postman como único cliente; con un cliente browser real pasa a
           ser prerequisito técnico. `AddCors`/`UseCors` agregado solo en Gateway,
           origen explícito por config, sin `AllowAnyOrigin` ni `AllowCredentials`.
        2. La ruta `/cargas/{**resto}` del Gateway exigía el permiso `carga:masiva`
           para TODO método, incluyendo `GET` — el rol `consulta` (autenticado, sin
           ese permiso) recibía 403 al pedir su propio historial, aunque Control ya
           exige solo "autenticado" en sus GET. Además esa ruta usaba el rate limit
           de subida (10/min), insuficiente para el polling cada 3 s. Separada en
           `cargas-subida` (POST, `PoliticaCargaMasiva`, 10/min) y `cargas-consulta`
           (GET, `PoliticaAutenticado`, 60/min).
        `dotnet test` completo re-corrido tras el cambio de Gateway (ver resultado
        en README) para confirmar que el split de rutas no rompió nada.
- [ ] O.2 Workflow de GitHub Actions (`dotnet build` + el test) — **solo si sale verde**
