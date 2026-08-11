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
