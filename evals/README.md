# Plan de evaluación v0.1

La evaluación antecede a la implementación. Cada caso es un fixture conceptual o de configuración mínima que el auditor futuro debe clasificar sin depender de la afirmación del implementador.

## Estados esperados

- `fail`: falta o viola un control aplicable.
- `pass`: evidencia suficiente para el control en el alcance del fixture; no certifica el sistema completo.
- `exception`: desviación explícita, vigente y con compensación comprobable.
- `inconclusive`: evidencia insuficiente; no se convierte en pass.

## Métricas mínimas

| Métrica | Cálculo / criterio |
| --- | --- |
| Verdaderos positivos | Casos `vulnerable` marcados `fail` con finding correcto. |
| Falsos positivos | Casos `compliant` marcados `fail` sin violación real. |
| Falsos negativos | Casos `vulnerable` marcados `pass` o sin finding requerido. |
| Exactitud de evidencia | El finding enlaza el artefacto/configuración que demuestra la condición. |
| Exactitud de severidad | Severidad coincide con la definida en el catálogo y el perfil. |
| Exactitud de recomendación | Propone el control adecuado, no una herramienta irrelevante ni una afirmación de cumplimiento. |

Los casos viven en [vulnerable](vulnerable/cases.v0.1.yaml), [compliant](compliant/cases.v0.1.yaml) y [exceptions](exceptions/cases.v0.1.yaml). [expected/schema.md](expected/schema.md) define el contrato.

La [matriz de cobertura](coverage-matrix.v0.1.md) enlaza los 15 controles con al
menos un caso vulnerable y uno conforme de diseño. Ningún caso se considera
ejecutado en Gate 1: las métricas de falsos positivos/negativos solo se calculan
cuando un adaptador y sus fixtures se ejecuten realmente.

## Nota para fixtures de secretos

EVAL-V-007 expresa una intención independiente de plataforma: una exposición
plausible debe recibir prevención, detección o respuesta. Un adaptador ejecutable de
GitHub u otro proveedor debe seleccionar un fixture **sintético y compatible con el
detector evaluado**, conforme a sus patrones/limitaciones documentados. Nunca se usa
una credencial real ni se presume que una cadena arbitraria disparará secret scanning.
