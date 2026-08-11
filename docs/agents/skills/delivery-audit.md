# Skill proposal: `delivery-audit`

## Propósito

Evaluar de forma independiente si una implementación satisface el control declarado y produce la evidencia esperada, incluyendo detección de falsos positivos.

## Disparadores

“audita delivery”, “verifica este control”, “revisa CI/CD”, “evalúa evidencia”, “ejecuta matriz de controles”.

## Entrada requerida

- Control/catalog versionado, implementación observada y evidencia disponible.
- Casos en `evals/`, perfil de riesgo y excepciones activas.

## Procedimiento

1. Separar hechos observables de afirmaciones de cumplimiento.
2. Ejecutar o revisar casos vulnerable, compliant y exception relevantes.
3. Medir verdaderos positivos, falsos positivos/negativos y exactitud de evidencia/severidad/recomendación.
4. Determinar pass, fail, exception, inconclusive o not-applicable con justificación.
5. Registrar brechas y nunca modificar la implementación auditada como parte de la misma conclusión.

## Salida / evidencia

Informe con `control_id`, casos, resultado, evidencia comprobada, hallazgos, riesgo residual y recomendación.

## Límites

No aprueba merges/despliegues, no acepta su propia evidencia y no transforma findings en excepciones sin dueño humano.
