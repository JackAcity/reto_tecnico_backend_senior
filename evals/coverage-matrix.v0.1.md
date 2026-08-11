# Matriz de cobertura de evaluaciones v0.1

## Regla de cobertura de diseño

Cada control del catálogo debe tener al menos un escenario vulnerable (`fail`) y uno
conforme (`pass`) diseñados. Las excepciones se añaden donde una desviación temporal
es parte material del control; no son sustituto de un caso conforme. La matriz mide
**cobertura de diseño**, no resultados ejecutados ni tasas reales de falsos positivos
o falsos negativos.

| Control | Vulnerable | Conforme | Excepción | Estado de diseño |
| --- | --- | --- | --- | --- |
| CTL-001 | EVAL-V-001, EVAL-V-012 | EVAL-C-001 | — | Completo. |
| CTL-002 | EVAL-V-002 | EVAL-C-006 | — | Completo. |
| CTL-003 | EVAL-V-003 | EVAL-C-007 | — | Completo. |
| CTL-004 | EVAL-V-008 | EVAL-C-008 | EVAL-E-001 | Completo. |
| CTL-005 | EVAL-V-007 | EVAL-C-009 | — | Completo. |
| CTL-006 | EVAL-V-005, EVAL-V-006, EVAL-V-011 | EVAL-C-002 | — | Completo. |
| CTL-007 | EVAL-V-004 | EVAL-C-010 | — | Completo. |
| CTL-008 | EVAL-V-013 | EVAL-C-011 | — | Completo. |
| CTL-009 | EVAL-V-009 | EVAL-C-012 | — | Completo. |
| CTL-010 | EVAL-V-010 | EVAL-C-003 | EVAL-E-002 | Completo. |
| CTL-011 | EVAL-V-014 | EVAL-C-013 | — | Completo. |
| CTL-012 | EVAL-V-017 | EVAL-C-014 | — | Completo. |
| CTL-013 | EVAL-V-018 | EVAL-C-015 | — | Completo. |
| CTL-014 | EVAL-V-016 | EVAL-C-004 | — | Completo. |
| CTL-015 | EVAL-V-015 | EVAL-C-005 | — | Completo. |

## Ejecución posterior al Gate 1

La primera implementación vertical debe ejecutar ambos caminos de cada control que
active y conservar los findings/evidencia. Solo entonces se podrán calcular
verdaderos positivos, falsos positivos y falsos negativos para ese adaptador y
fixture. Los controles no implementados siguen siendo escenarios de diseño, no
afirmaciones de cobertura operativa.
