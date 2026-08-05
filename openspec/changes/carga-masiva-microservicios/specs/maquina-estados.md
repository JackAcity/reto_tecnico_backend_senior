# Spec — Máquina de estados de una carga

## Requisito

El enunciado enumera cinco estados (`Pendiente`, `En proceso`, `Cargado`,
`Finalizado`, `Notificado`) pero exige comportamientos que **no tienen estado
asignado**: *"la carga debe ser rechazada"*, *"la carga debe ser bloqueada"*, y
*"almacenar los fallidos"*. Tampoco define la diferencia entre `Cargado` y
`Finalizado`.

El sistema DEBE implementar una máquina de estados completa y explícita, con las
transiciones válidas cerradas.

## Estados

| Estado | Significado (definición del sistema) | Lo escribe |
|---|---|---|
| `Pendiente` | Archivo almacenado en SeaweedFS, mensaje publicado, aún no consumido | Control |
| `EnProceso` | Consumido; el Excel se está leyendo y validando | CargaMasiva |
| `Cargado` | Filas válidas insertadas en `DataProcesada` | CargaMasiva |
| `Finalizado` | Auditoría de fallidos cerrada y notificación publicada | CargaMasiva |
| `Notificado` | Correo enviado al usuario | Notificaciones |
| `Rechazada` | **Terminal.** Todos los periodos del archivo ya tenían carga `Cargado`/`Finalizado`/`Notificado` | CargaMasiva |
| `Bloqueada` | **Terminal.** Todos los periodos tienen otra carga activa (`Pendiente`/`EnProceso`) | CargaMasiva |
| `Fallida` | **Terminal.** Error técnico: archivo ilegible, fallo de publicación, mensaje envenenado | Control o CargaMasiva |

## Transiciones válidas

```
Pendiente  → EnProceso | Fallida
EnProceso  → Cargado | Rechazada | Bloqueada | Fallida
Cargado    → Finalizado
Finalizado → Notificado
```

Todo otro par ES inválido y DEBE lanzar excepción de dominio.

## Escenarios

### Escenario: carga con periodos mixtos avanza parcialmente
- **DADO** un archivo con periodos `2025-01`, `2025-02`, `2025-03`
- **Y** que `2025-02` ya tiene una carga previa en estado `Finalizado`
- **CUANDO** CargaMasiva procesa el archivo
- **ENTONCES** las filas de `2025-01` y `2025-03` se insertan
- **Y** las filas de `2025-02` se registran en `DetalleCargaError` con motivo `PeriodoYaCargado`
- **Y** la carga termina en `Finalizado` (no en `Rechazada`: hubo trabajo útil)

### Escenario: todos los periodos ya estaban cargados
- **DADO** un archivo cuyos tres periodos tienen carga previa `Finalizado`
- **CUANDO** CargaMasiva lo procesa
- **ENTONCES** no se inserta ninguna fila en `DataProcesada`
- **Y** todas las filas se auditan con motivo `PeriodoYaCargado`
- **Y** el estado terminal es `Rechazada`

### Escenario: la validación de periodo no se auto-bloquea
- **DADO** que Control ya creó la fila de esta carga en estado `Pendiente`
- **CUANDO** CargaMasiva consulta si el periodo tiene cargas activas
- **ENTONCES** la consulta DEBE excluir el `IdCarga` en curso
- **Y** la carga NO se bloquea a sí misma

### Escenario: publicación fallida no deja la carga huérfana
- **DADO** que Control confirmó el `INSERT` en estado `Pendiente`
- **CUANDO** la publicación en RabbitMQ lanza excepción
- **ENTONCES** la carga transiciona a `Fallida` con el error auditado
- **Y** el endpoint responde error, no `201`

## Test determinista

`Estados_TransicionInvalida_LanzaExcepcion` — recorre el producto cartesiano de los
ocho estados y afirma que solo el conjunto de pares listado arriba es aceptado.
