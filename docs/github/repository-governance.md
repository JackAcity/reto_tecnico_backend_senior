# Estándar GitHub propuesto: gobernanza de repositorio

**PLATFORM CAPABILITY:** GitHub Rulesets puede regular interacciones con ramas y tags; [la documentación](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets) indica que su creación exige permisos administrativos.

## Propuesta condicionada a aprobación

1. Identificar la rama confiable, el modelo de ramas y los roles de bypass.
2. Proteger archivos de control, especialmente workflows, políticas y adaptadores de despliegue.
3. Exigir PR y gates cuya efectividad esté probada por la matriz de evals.
4. Configurar propietarios reales, no usuarios o equipos inventados, antes de exigir CODEOWNERS.
5. Registrar cualquier bypass de emergencia y revisarlo después.

**TBD-GH-01** y **TBD-SCM-01** bloquean esta propuesta. No existe un ruleset implementado en v0.1.
