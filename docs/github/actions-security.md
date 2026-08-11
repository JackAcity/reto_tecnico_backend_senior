# Estándar GitHub propuesto: seguridad de Actions

**PLATFORM CAPABILITY — SRC-GH-ACTIONS-SEC:** GitHub documenta riesgos de Actions, incluido `pull_request_target`, y la política de exigir referencias de acción por SHA completo. Este diseño propone evaluar cada workflow contra CTL-006, CTL-007 y CTL-008 antes de activarlo.

## Reglas de diseño propuestas

- Separar ejecución de código no confiable de jobs con secretos, permisos de escritura o identidad cloud.
- Declarar permisos mínimos por job; una ausencia de necesidad no justifica escritura por defecto.
- Inventariar acciones/reusable workflows, fijar referencias a identidad inmutable y revisar su actualización como cambio de dependencia.
- Proteger cambios a workflows, políticas y scripts de deployment con revisión apropiada al riesgo.
- Tratar output, título, branch name, issue/PR metadata y artefactos externos como input no confiable.
- No usar runners privilegiados/self-hosted para cargas no confiables sin diseño de aislamiento aprobado.

Estas son decisiones de arquitectura candidatas. La evaluación debe encontrar triggers inseguros y no marcar como vulnerables workflows que separan correctamente permisos e inputs.
