# Mapa de caminos — el feliz y todos los que no lo son

Cada fila es un camino distinto que el sistema puede tomar, con lo que lo
dispara, a dónde llega, y dónde está probado (no declarado — corrido de
verdad, en test o en vivo contra contenedores reales). Es la lista que
responde *"¿qué pasa si...?"* para cualquier pregunta de entrevista sobre el
comportamiento del sistema.

## 1 — En la subida (`POST /cargas`, antes de tocar la cola)

| # | Escenario | Dispara con | Llega a | Verificado en |
|---|---|---|---|---|
| 1.1 | **Camino feliz** | Archivo `.xlsx` válido, con permiso | `201`, `Pendiente`, mensaje publicado | `Subir_XlsxValido_QuedaRegistradaEnPendienteYConRutaDeSeaweed` |
| 1.2 | Sin token | Header `Authorization` ausente | `401` en el Gateway, ni siquiera llega a Control | `Cargas_SinToken_Da401` |
| 1.3 | Token falsificado/inválido | JWT mal firmado o corrupto | `401` en el Gateway | `Cargas_ConTokenFalsificado_Da401` |
| 1.4 | Autenticado sin permiso | Usuario con rol `consulta` (sin claim `carga:masiva`) | `403` en el Gateway — ni llega a Control | `Cargas_ConUsuarioSinPermiso_Da403` |
| 1.5 | Extensión inválida | Archivo que no termina en `.xlsx` | `400`, nada se guarda | `Subir_ConExtensionInvalida_Da400` |
| 1.6 | Firma binaria inválida | `.txt`/`.csv` renombrado a `.xlsx` (pasa 1.5, falla acá) | `400`, nada se guarda — ni siquiera se sube a SeaweedFS | `Subir_TextoPlanoRenombradoAXlsx_Da400PorFirma` |
| 1.7 | Excede el tamaño máximo | Archivo > `CARGA_TAMANO_MAXIMO_MB` | `400` de negocio, **antes** del techo de Kestrel/YARP (evita el 413 crudo) | `ElMaximoEsConfigurable` (§C12) |
| 1.8 | Archivo vacío | 0 bytes | `400` | `ArchivoVacio_SeRechaza` |
| 1.9 | Cuerpo del POST demasiado grande en `/auth/login` | Body > 4 KB en login/refresh | Corta antes de llegar a Auth (techo por ruta, no el general de `/cargas`) | `Login_ConCuerpoDemasiadoGrande_NoDa401` |
| 1.10 | **Falla la publicación** (dual write, §C7) | Sube y guarda OK, pero RabbitMQ no responde | `502`, carga registrada como **`Fallida`**, error auditado — no queda huérfana en `Pendiente` | `PublicacionFallida_DejaLaCargaEnFallidaConElError` |
| 1.11 | Ráfaga sobre ruta anónima | > 10 requests/min a `/auth/login` o `/auth/refresh` desde la misma IP | `429` | `Rafaga_SobreRutaAnonima_Termina429` |

## 2 — Dentro de una fila del Excel (CargaMasiva, `NormalizadorFila`)

| # | Escenario | Dispara con | Resultado de la fila | Verificado en |
|---|---|---|---|---|
| 2.1 | Fila completamente vacía | Las 4 columnas vacías/blancas | **Descartada en silencio** — ni se cuenta en `totalFilas` ni se audita | `FilasVacias_NoSeRegistranNiSeAuditan` |
| 2.2 | `NombreProducto` vacío | Columna en blanco | Se inserta con default `"SIN NOMBRE"`, se audita `ValorPorDefectoAplicado` | `ColumnasVacias_AplicanValorPorDefecto_YSeAuditan` |
| 2.3 | `Precio` vacío | Columna en blanco | Se inserta con default `0`, se audita `ValorPorDefectoAplicado` | idem |
| 2.4 | `Precio` no numérico | Texto en la celda de precio | Tratado igual que vacío: default `0`, `ValorPorDefectoAplicado` | idem |
| 2.5 | `Precio` negativo | Valor presente pero inválido | **Rechazada** — `PrecioInvalido`, no se inserta (a diferencia de 2.3/2.4, esto SÍ es un dato erróneo, no ausente) | `fixture-sucio.xlsx`, fila F005 |
| 2.6 | `Periodo` ausente | Columna en blanco | Rechazada — `PeriodoRequerido` | `fixture-sucio.xlsx`, fila F006 |
| 2.7 | `Periodo` con formato inválido | No cumple `yyyy-MM` | Rechazada — `PeriodoFormatoInvalido` | `fixture-sucio.xlsx`, fila F007 (`"2099-13"`) |
| 2.8 | `CodigoProducto` ausente | Columna en blanco | Rechazada — `CodigoRequerido` | `fixture-sucio.xlsx` |
| 2.9 | Duplicado intra-archivo (mismo periodo) | Mismo `(Periodo, CodigoProducto)` dos veces en el mismo Excel | Primera ocurrencia gana; el resto → `Existente` | `DuplicadoDentroDelMismoArchivo_GanaLaPrimeraOcurrencia` |
| 2.10 | Mismo código, **otro** periodo | `(2025-01, P001)` y `(2025-02, P001)` | **Ambas se insertan** — son claves distintas (design.md §C5) | `ArchivoDeMuestra_Inserta154_Rechaza46` |
| 2.11 | Ya existe en base (de una carga previa) | `(Periodo, CodigoProducto)` ya en `data_procesada` | No se inserta, `Existente` | `Reproceso_ConTodoYaEnBase_NoAceptaNada` |

## 3 — Resultado global de la carga (por periodo, CargaMasiva)

| # | Escenario | Dispara con | Estado terminal | Verificado en |
|---|---|---|---|---|
| 3.1 | **Camino feliz total** | Todos los periodos del archivo están libres | `Finalizado` → `Notificado`, correo con el resumen | `ArchivoDeMuestra_ProcesadoPorElConsumidorReal...` (154/46 real) |
| 3.2 | Parcial | Algunos periodos libres, otros no | `Finalizado` igual — *"hubo trabajo útil"* (maquina-estados.md) | `PeriodoYaCargado_DescartaSoloEseTramo_YProcesaElResto` |
| 3.3 | Todos los periodos `YaCargado` | El archivo entero ya fue cargado antes (mismo archivo, segunda vez) | **`Rechazada`** — 0 insertadas, todas auditadas `PeriodoYaCargado` | Ensayo del video: subida del mismo archivo dos veces |
| 3.4 | Algún periodo `Bloqueado` (otra carga activa) | Dos cargas casi simultáneas del mismo periodo | **`Bloqueada`** — gana sobre Rechazada si hay mezcla | `OtraCargaEnProceso_BloqueaElPeriodo` |
| 3.5 | La propia carga no se autobloquea | `sp_resolver_periodo` excluye su propio `IdCarga` (§C2) | `Libre`, incluso reentregado | `PropiaCarga_NoSeAutoBloquea_NiSiquieraAlReintentar` |
| 3.6 | Una carga muerta libera el periodo | Carga anterior terminó en `Fallida` sin completar | El periodo vuelve a estar `Libre` para la siguiente | `CargaFallida_LiberaElPeriodo` |
| 3.7 | Mensaje reentregado sobre carga ya resuelta | Redelivery de un mensaje ya procesado (`Finalizado`/`Rechazada`/etc.) | Se ignora, `ack`, no se reprocesa | `ManejadorCarga` — chequeo de estado antes de procesar |
| 3.8 | Fallo técnico durante el procesamiento | SeaweedFS caído, Excel corrupto, error de conexión | Reintento vía DLX hasta 3 veces (`x-death`); al agotarse → **`Fallida`** + mensaje en `carga_masiva.muertos` | `ConsumidorCargaMasivaTests` (conteo de `x-death`) |

## 4 — En la notificación (Notificaciones)

| # | Escenario | Dispara con | Resultado | Verificado en |
|---|---|---|---|---|
| 4.1 | **Camino feliz** | Carga en `Finalizado` | Correo enviado, `Notificado` | Verificación en vivo — Mailpit, cuerpo con 154/46 |
| 4.2 | Carga no está en `Finalizado` | Mensaje inconsistente (no debería ocurrir en operación normal) | Se ignora, `ack`, no reenvía correo | `ManejadorNotificacion` — chequeo de estado |
| 4.3 | SMTP caído | Mailpit/servidor real no responde | Reintento vía DLX hasta 3 veces | Mismo mecanismo que 3.8, cola `notificaciones.reintento` |
| 4.4 | Reintentos agotados | 3 intentos fallidos de envío | **No** se marca `Fallida` (no existe esa transición desde `Finalizado`) — `LogCritical` + mensaje a `notificaciones.muertos`; la carga queda en `Finalizado` para siempre, sin `Notificado` | Documentado en `ConsumidorNotificaciones`, no hay path de negocio para revertir un envío ya "exitoso en los datos" |

## 5 — Fuera del flujo de carga (transversal)

| # | Escenario | Resultado | Verificado en |
|---|---|---|---|
| 5.1 | Login con credenciales inválidas | `401` — mismo mensaje exista o no el usuario (anti-enumeración) | `Login_ConPasswordIncorrecta_NoEmiteNada`, `Login_DeUsuarioInexistente_NoEmiteNada` |
| 5.2 | Login de usuario inactivo | `401` | `Login_DeUsuarioInactivo_NoEmiteNada` |
| 5.3 | Refresh token ya usado | `401` (rotación de un solo uso) | `Refresh_ConTokenYaUsado_Falla` |
| 5.4 | Refresh token expirado | `401` | `Refresh_ConTokenExpirado_Falla` |
| 5.5 | Consulta de una carga que no existe | `404` | `Detalle_DeUnaCargaInexistente_Da404` |
| 5.6 | Descarga de contenido de una carga inexistente | `404` | `Contenido_DeUnaCargaInexistente_Da404` |

## Lo que NO tiene camino (huecos declarados, no ocultos)

- **`Rechazada`/`Bloqueada`/`Fallida` no notifican por correo.** El enunciado
  solo define `Finalizado → Notificado`. Un usuario cuya carga fue rechazada
  no se entera por email — solo consultando `GET /cargas/{id}`.
- **Reuso de un refresh token no revoca la cadena completa** (`ponytail:` en
  el código) — solo el token reusado falla con 401; una detección más
  agresiva (revocar todos los tokens del usuario) se agregaría si el alcance
  creciera.
