# Estándar GitHub propuesto: CI

El adaptador GitHub de CTL-002 y CTL-003 se diseñará después de la evaluación. El contrato mínimo de cada ejecución es: commit/ref exacto, definición de workflow, versiones de toolchain, inputs/dependencias, resultados, duración, identidad de runner y enlaces a artefactos de evidencia.

Un check debe fallar de forma explícita cuando la condición que controla no se cumpla. Un check opcional o informativo no debe presentarse como gate. Los resultados deben poder consultarse desde el PR y exportarse conforme a TBD-EVID-01.

**HYPOTHESIS:** se podrán implementar validaciones de .NET, frontend, OpenSpec y Docker Compose como verificadores deterministas. La fase actual no crea esos workflows ni afirma su cobertura.
