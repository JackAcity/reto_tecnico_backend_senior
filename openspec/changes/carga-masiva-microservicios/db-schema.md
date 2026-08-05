# Esquema de base de datos

El enunciado §5 dice: *"Para la construcción del modelo de datos se debe utilizar el
criterio propio del candidato, se dejan scripts y nombres de referencia"*. Se parte de
las tablas sugeridas (`CargaArchivo`, `DetalleCarga`, `DataProcesada`) y se agregan las
que las reglas de negocio exigen.

Convención: `snake_case` (idiomático en PostgreSQL), mapeado desde entidades PascalCase
por EF Core. Una sola base de datos, un solo esquema — el diagrama entregado lo
prescribe (ver `design.md` §C10).

## Propiedad de escritura

| Tabla | Escribe | Lee |
|---|---|---|
| `usuario`, `refresh_token` | Auth | Auth |
| `carga_archivo` | Control (crea), CargaMasiva (transiciona), Notificaciones (`Notificado`) | Control |
| `carga_periodo` | CargaMasiva | Control |
| `data_procesada` | CargaMasiva | Control |
| `detalle_carga_error` | CargaMasiva | Control |

Con base compartida, la disciplina reemplaza al límite físico: cada tabla tiene dueños
declarados y ningún servicio escribe fuera de su columna de esta tabla.

## Tablas

### `usuario`
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | |
| `email` | `varchar(150) UNIQUE NOT NULL` | credencial de login |
| `password_hash` | `text NOT NULL` | `PasswordHasher<T>` (PBKDF2), nunca texto plano |
| `rol` | `varchar(50) NOT NULL` | habilita la policy `carga:masiva` (§3.2a) |
| `activo` | `boolean NOT NULL DEFAULT true` | |

### `refresh_token` — §2.3d, opcional valorado
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | |
| `usuario_id` | `int FK → usuario` | |
| `token` | `varchar(200) UNIQUE NOT NULL` | |
| `expira_en` | `timestamptz NOT NULL` | |
| `revocado_en` | `timestamptz NULL` | rotación: al usarse se revoca |
| `reemplazado_por` | `varchar(200) NULL` | cadena de rotación auditable |

### `carga_archivo` — trazabilidad, §2.4c y §3.2c
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | es el `idCarga` del contrato de mensaje |
| `nombre_archivo` | `varchar(260) NOT NULL` | |
| `ruta_archivo` | `varchar(500) NULL` | `seaweed://...` — formato literal del enunciado |
| `tamano_bytes` | `bigint NOT NULL` | |
| `usuario` | `varchar(150) NOT NULL` | *"auditoría de quién"* |
| `fecha_registro` | `timestamptz NOT NULL` | *"y cuándo"* |
| `estado` | `varchar(20) NOT NULL` | ver `specs/maquina-estados.md` |
| `fecha_fin` | `timestamptz NULL` | va en el mensaje de notificación |
| `total_filas` | `int NOT NULL DEFAULT 0` | |
| `filas_insertadas` | `int NOT NULL DEFAULT 0` | |
| `filas_rechazadas` | `int NOT NULL DEFAULT 0` | |
| `mensaje_error` | `text NULL` | poblado en estado `Fallida` |
| `correlation_id` | `varchar(50) NOT NULL` | atraviesa HTTP → AMQP → consumidor |

### `carga_periodo` — resuelve §C3 (un archivo trae N periodos)
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | |
| `carga_archivo_id` | `int FK → carga_archivo` | |
| `periodo` | `varchar(7) NOT NULL` | formato `yyyy-MM` |
| `estado` | `varchar(20) NOT NULL` | `Aceptado` / `YaCargado` / `Bloqueado` |
| `filas_insertadas` | `int NOT NULL DEFAULT 0` | |

```sql
-- Impide dos cargas activas del mismo periodo a nivel motor, no a nivel aplicación.
-- Un SELECT previo al INSERT sería un TOCTOU (design.md §C9).
CREATE UNIQUE INDEX ux_carga_periodo_activo
  ON carga_periodo (periodo)
  WHERE estado = 'Aceptado';
```

### `data_procesada` — los registros del Excel
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | |
| `periodo` | `varchar(7) NOT NULL` | |
| `codigo_producto` | `varchar(50) NOT NULL` | |
| `nombre_producto` | `varchar(200) NOT NULL` | default `'SIN NOMBRE'` si viene vacío |
| `precio` | `numeric(18,2) NOT NULL` | default `0` si viene vacío o no numérico |
| `carga_archivo_id` | `int FK → carga_archivo` | trazabilidad del origen |
| `fecha_registro` | `timestamptz NOT NULL` | |

```sql
-- La clave de negocio. Justificación en design.md §C5.
-- Sirve además como llave natural de idempotencia del consumidor (§C8).
CREATE UNIQUE INDEX ux_data_procesada_periodo_codigo
  ON data_procesada (periodo, codigo_producto);
```

### `detalle_carga_error` — §3.3c, *"tabla de auditoría y trazabilidad"*
| Columna | Tipo | Notas |
|---|---|---|
| `id` | `serial PK` | |
| `carga_archivo_id` | `int FK → carga_archivo` | |
| `numero_fila` | `int NOT NULL` | fila del Excel, para que el usuario la ubique |
| `periodo` | `varchar(7) NULL` | |
| `codigo_producto` | `varchar(50) NULL` | |
| `columna` | `varchar(50) NULL` | |
| `motivo` | `varchar(40) NOT NULL` | enumerado abajo |
| `valor_crudo` | `text NULL` | lo que venía en la celda, sin normalizar |
| `fecha_registro` | `timestamptz NOT NULL` | |

**Motivos:** `PeriodoYaCargado` · `PeriodoBloqueado` · `Existente` ·
`ValorPorDefectoAplicado` · `PrecioInvalido` · `PeriodoRequerido` ·
`PeriodoFormatoInvalido` · `CodigoRequerido`

## Procedimientos almacenados — §4.15, obligatorio

Uncle Bob: *"the database is a detail"*. Por eso los SPs viven en `Infrastructure`,
detrás de una interfaz que `Application` define, y se usan **solo donde el motor gana
de verdad**. Sin procedimientos de adorno.

### `sp_resolver_periodo(p_carga_id int, p_periodo varchar) → varchar`

Resuelve en **una sola operación atómica** si un periodo puede procesarse. El advisory
lock serializa a los competidores por el mismo periodo dentro de la transacción,
cerrando la carrera del §C9.

```
1. pg_advisory_xact_lock(hashtext(p_periodo))
2. libera las reservas 'Aceptado' cuyo carga_archivo quedó en Fallida/Rechazada/
   Bloqueada: pasan a 'Bloqueado'  ← sin esto, una carga muerta reserva el periodo
                                      para siempre por el índice único parcial
3. ¿existe carga_periodo 'Aceptado' de OTRA carga cuyo carga_archivo esté
   en Cargado/Finalizado/Notificado?           → 'YaCargado'
4. ¿existe otra carga activa (Pendiente/EnProceso) para ese periodo,
   excluyendo p_carga_id?                       → 'Bloqueado'   ← §C2: excluye la propia
5. en otro caso, inserta carga_periodo 'Aceptado' → 'Libre'
   (con ON CONFLICT DO NOTHING: la reentrega del propio mensaje es inofensiva, §C8)
```

El paso 2 usa `'Bloqueado'` y no un estado nuevo porque es exactamente lo que le pasó
a ese periodo en esa carga: no llegó a procesarse.

### `sp_insertar_data_procesada(...arrays...) → int`

Inserción masiva **set-based** con `unnest`: un round trip en vez de N. El
`ON CONFLICT DO NOTHING` sobre `ux_data_procesada_periodo_codigo` hace que reprocesar
un mensaje reentregado sea inofensivo — es el **Idempotent Consumer** de Richardson
apoyado en la llave natural, sin tabla de mensajes procesados.

Devuelve la cantidad realmente insertada; la diferencia contra el tamaño del lote son
los duplicados que ya existían en base.

## Migraciones — §4.14, obligatorio

EF Core migrations aplicadas al arranque. **Solo `Control` es dueño del esquema**; los
demás servicios esperan por health check (`design.md` §C11). Cinco servicios migrando
a la vez sería una carrera.

`scripts/sql/` se genera con `dotnet ef migrations script`, para cumplir §7.4.
