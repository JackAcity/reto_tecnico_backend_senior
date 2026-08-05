# Spec — Procesamiento del Excel

## Requisito

El sistema DEBE leer el archivo en streaming (memoria constante), normalizar los
datos, aplicar las reglas de negocio del enunciado y auditar cada fila descartada
con su motivo.

Columnas esperadas: `Periodo | CodigoProducto | NombreProducto | Precio`.

## Reglas de normalización

| Situación | Comportamiento | Motivo auditado |
|---|---|---|
| Fila completamente vacía | Se descarta, **no se audita** (el enunciado: *"no se deben registrar"*) | — |
| `NombreProducto` vacío | Valor por defecto `"SIN NOMBRE"` | `ValorPorDefectoAplicado` |
| `Precio` vacío o no numérico | Valor por defecto `0` | `ValorPorDefectoAplicado` |
| `Precio` negativo | Fila descartada | `PrecioInvalido` |
| `Periodo` vacío | Fila descartada (no se puede resolver el periodo) | `PeriodoRequerido` |
| `Periodo` con formato distinto de `yyyy-MM` | Fila descartada | `PeriodoFormatoInvalido` |
| `CodigoProducto` vacío | Fila descartada (es la clave) | `CodigoRequerido` |
| Espacios al inicio/fin | Se recortan antes de validar | — |

Una fila con valores por defecto aplicados **sí se inserta**; la aplicación del
default se registra en auditoría sin bloquear la carga.

## Reglas de duplicidad

Orden de evaluación, sobre las filas ya normalizadas:

1. **Periodo** — si el periodo de la fila tiene una carga previa en
   `Cargado`/`Finalizado`/`Notificado` → descartar, motivo `PeriodoYaCargado`.
   Si tiene una carga previa `Pendiente`/`EnProceso` **con distinto `IdCarga`** →
   descartar, motivo `PeriodoBloqueado`.
2. **Duplicado intra-lote** — si el par `(Periodo, CodigoProducto)` ya apareció en
   este mismo archivo → descartar, motivo `Existente`. **Gana la primera ocurrencia.**
3. **Duplicado en base** — si el par `(Periodo, CodigoProducto)` ya existe en
   `DataProcesada` → descartar, motivo `Existente`.

La clave es **`(Periodo, CodigoProducto)`**, no el código solo. Justificación completa
en `design.md` §C5: el enunciado pide una validación de duplicidad *por periodo* que
solo tiene sentido si los datos están particionados por periodo; una clave global
haría que el sistema sirviera una única vez.

## Escenarios

### Escenario: el archivo de muestra produce un resultado exacto
- **DADO** `samples/carga_masiva_productos.xlsx` (200 filas de datos)
- **Y** una base de datos vacía
- **CUANDO** se procesa
- **ENTONCES** se insertan exactamente **154** filas en `DataProcesada`
- **Y** se auditan exactamente **46** filas con motivo `Existente`
- **Y** los tres periodos `2025-01`, `2025-02`, `2025-03` quedan registrados en `CargaPeriodo`

### Escenario: reprocesar el mismo archivo es idempotente
- **DADO** que el archivo de muestra ya fue procesado (154 insertados)
- **CUANDO** el mismo mensaje se reentrega
- **ENTONCES** no se insertan filas nuevas
- **Y** el total en `DataProcesada` sigue siendo 154

### Escenario: filas vacías y defaults
- **DADO** un archivo con 3 filas de datos válidas, 2 filas totalmente vacías,
  1 fila sin `NombreProducto` y 1 fila con `Precio` no numérico
- **CUANDO** se procesa
- **ENTONCES** se insertan 5 filas
- **Y** las 2 filas vacías no aparecen en `DataProcesada` ni en `DetalleCargaError`
- **Y** la fila sin nombre queda con `"SIN NOMBRE"`
- **Y** la fila con precio inválido queda con `0`

## Test determinista

`ProcesadorExcel_ArchivoDeMuestra_Inserta154Rechaza46` — es la prueba de aceptación
del núcleo funcional (35% de la rúbrica). Si falla, el sistema es incorrecto aunque
el flujo distribuido funcione.

El test complementario `ProcesadorExcel_ClaveGlobal_Inserta116Rechaza84` deja
ejecutable el escenario alternativo, para que la decisión de `design.md` §C5 sea
verificable y no solo declarativa.
