# Spec — Cliente web React (O.1, opcional §2.1/§4.2 del enunciado)

## Requisito

El enunciado marca el frontend como **opcional pero valorado** (§2.1): si no se
implementa, la entrega debe apoyarse solo en la colección Postman (§6.4, ya
entregada — `postman/reto-carga-masiva.postman_collection.json`). El backend
completo (Bloques 1–7) ya está funcional y verificado; este spec cubre el cliente
que demuestra el flujo completo end-to-end para el video (Bloque 9), sin tocar
ningún servicio .NET.

El cliente DEBE consumir el Gateway (`http://localhost:8080`, único punto público)
y no debe hablar directo con ningún microservicio.

## Pantallas (§4.2 del enunciado, las 4 exigidas si se opta por frontend)

| # | Pantalla | Endpoint(s) |
|---|---|---|
| 0 | Login | `POST /auth/login` |
| 1 | Subida de Excel | `POST /cargas` (multipart, campo `archivo`) |
| 2 | Historial de cargas (tabla) | `GET /cargas?limite=` |
| 3 | Detalle de una carga | `GET /cargas/{id}`, `GET /cargas/{id}/contenido` |

## Contrato consumido (verbatim de los `Program.cs` de Auth y Control — no se
inventa forma de respuesta)

```
POST /auth/login    { email, password } → 200 { accessToken, expiraEn, refreshToken } | 401
POST /auth/refresh  { refreshToken }     → 200 { accessToken, expiraEn, refreshToken } | 401

POST /cargas (multipart: archivo)  → 201 { idCarga, estado, error? } | 400 ValidationProblem | 502 { idCarga, estado, error }
GET  /cargas?limite=50             → 200 [ { idCarga, nombreArchivo, usuario, fechaRegistro, estado,
                                              totalFilas, filasInsertadas, filasRechazadas, fechaFin } ]
GET  /cargas/{id}?limiteErrores=100→ 200 { carga: ResumenCarga, rutaArchivo, mensajeError, correlationId,
                                            periodos: [{periodo,estado,filasInsertadas}],
                                            errores: [{numeroFila,periodo,codigoProducto,columna,motivo,valorCrudo}],
                                            totalErrores } | 404
GET  /cargas/{id}/contenido        → 200 binario .xlsx (requiere Authorization: Bearer, no es un <a href> directo)
```

Estados posibles (`maquina-estados.md`, ya implementado en backend): `Pendiente`,
`EnProceso`, `Cargado`, `Finalizado`, `Notificado`, `Rechazada`, `Bloqueada`,
`Fallida` — los tres últimos son terminales y no los pide el enunciado
explícitamente, pero el cliente DEBE poder mostrarlos (aparecen de verdad).

## Decisiones (mismo criterio que el resto del repo: evidencia, no gusto)

- **Sin librería de estado global.** Un `AuthContext` con `useState` alcanza para
  un token; Redux/Zustand serían abstracción sin necesidad (ponytail).
- **Sin UI kit.** CSS propio mínimo — el 20% de evaluación pide "UX básica pero
  consistente", no un design system.
- **Polling, no WebSockets.** El enunciado dice literal *"mediante pooling"*
  (§3, 5️⃣). Se refresca Historial/Detalle cada 3 s mientras exista al menos una
  fila en estado no terminal.
- **Refresh transparente.** Un 401 en cualquier llamada autenticada dispara
  `POST /auth/refresh` una vez; si también falla, logout y redirect a `/login`.
  Evita que el usuario pierda el flujo por expirar el access token (60 min) a
  mitad de una demo.
- **Descarga de `/contenido` vía blob.** No puede ser `<a href>` plano porque el
  endpoint exige `Authorization: Bearer`; se hace `fetch` + `URL.createObjectURL`.
- **CORS en el Gateway — único cambio de backend que exige este spec.** `README.md`
  declaró CORS fuera de alcance (§C13/§C14) porque el cliente previsto era Postman,
  que no está sujeto a same-origin policy. Un cliente **browser** en
  `http://localhost:5173` (Vite dev) sí lo está: sin `Access-Control-Allow-Origin`
  el navegador bloquea toda respuesta del Gateway antes de que el JS la vea. Se
  agrega `AddCors`/`UseCors` **solo en Gateway** (única puerta pública), con
  origen(es) explícitos por configuración (`Cors:OrigenesPermitidos`, default
  `http://localhost:5173`) — nunca `AllowAnyOrigin`. Sin `AllowCredentials`: el
  cliente manda el JWT en `Authorization`, no en cookies, así que no lo necesita.
- **`X-Frame-Options: DENY` en el dev server de Vite.** `design.md` §C14 también
  había descartado CSP/X-Frame-Options "no hay página que clickjackear" — cierto
  para las 5 APIs JSON, ya no para este cliente. Con un navegador real, un sitio
  malicioso podría enmarcarlo en un `<iframe>` invisible y usar clickjacking
  contra una acción autenticada simple (ej. "Cerrar sesión"). `frame-ancestors`
  no es soportado vía `<meta>`; se agrega como header real en
  `vite.config.ts` (`server.headers` + `preview.headers`), el único servidor
  HTTP que este frontend usa hoy.

## Cambio de backend requerido (fuera del alcance de "solo frontend")

`src/Gateway/Program.cs` — `AddCors` + `UseCors("cliente-web")` antes de
`UseAuthentication`. Sin esto el spec no es implementable: es un prerequisito
técnico, no un cambio de alcance funcional (ningún microservicio, ruta, política
ni contrato cambia).

## Bug preexistente encontrado al verificar (no introducido por este spec)

La ruta única `/cargas/{**resto}` del Gateway exigía `PoliticaCargaMasiva`
(claim `permiso=carga:masiva`) para **cualquier** método — Control, en cambio,
ya exige solo `PoliticaAutenticado` en sus `GET /cargas` y `GET /cargas/{id}`
(`Control.Api/Program.cs`). Resultado: el rol `consulta` (pensado para *ver* sin
subir — así lo prueba README con el 403 en la subida) no podía ni listar su
propio historial. Invisible mientras el único cliente probado era Postman con
casos puntuales; lo expuso el primer flujo real de navegador con ese usuario.
Se separó en dos `RouteConfig` (`cargas-subida` con `Methods: [POST]`,
`cargas-consulta` con `Methods: [GET]`), cada una con la policy y el rate limit
que le corresponde — `cargas-consulta` pasa de 10/min (pensado para subidas
caras) a 60/min, porque el polling de Historial/Detalle cada 3 s superaba la
cuota vieja. Ver `tasks.md` §O.1.5 para el detalle de la verificación.

## Escenarios

### Escenario: login válido lleva al historial
- **DADO** las credenciales sembradas `admin@reto.local` / `Reto2026!`
- **CUANDO** el usuario envía el formulario de login
- **ENTONCES** se guarda `accessToken`/`refreshToken` y navega a `/historial`

### Escenario: login inválido no navega
- **DADO** una contraseña incorrecta
- **CUANDO** el usuario envía el formulario
- **ENTONCES** se muestra el mensaje de error del 401
- **Y** la ruta permanece en `/login`, sin token guardado

### Escenario: usuario sin permiso ve 403 al subir
- **DADO** el usuario sembrado `consulta@reto.local` (rol sin `carga:masiva`)
- **CUANDO** intenta `POST /cargas`
- **ENTONCES** el gateway responde 403 y la pantalla de Upload lo muestra sin crashear

### Escenario: historial refleja el flujo completo por polling
- **DADO** una carga recién subida en `Pendiente`
- **CUANDO** el backend la procesa de forma asíncrona
- **ENTONCES** la fila en `/historial` transiciona sola (sin recargar la página)
  hasta `Notificado`, visible dentro de la ventana de polling

## Test determinista

`Login.test.tsx` (Vitest + Testing Library, `fetch` mockeado — no depende del
stack real, corre en CI):
- credenciales válidas → `login()` del contexto se llama una vez con el token
  del mock y el componente navega
- credenciales inválidas (mock 401) → se renderiza el texto de error y `login()`
  NO se llama

La verificación end-to-end contra el stack real (los 4 escenarios de arriba con
Docker levantado) se hace manualmente antes de grabar el video — no es parte del
test determinista porque depende de estado async multi-servicio (igual criterio
que los tests de `CargaMasiva.Tests`, que sí corren contra contenedores reales
porque ahí el async importa probarlo real; acá el frontend no tiene lógica async
propia que valga la pena, solo consume una API ya probada).
