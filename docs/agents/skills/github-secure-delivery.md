# Skill proposal: `github-secure-delivery`

## Propósito

Convertir controles **ya aprobados** en un plan de adaptación GitHub verificable y con privilegio mínimo.

## Disparadores

“adapta este control a GitHub”, “diseña ruleset”, “diseña CI GitHub”, “plan OIDC GitHub”, “plan de environment”.

## Entrada requerida

- `control_id` aprobado para implementar y perfil de riesgo.
- Capacidad GitHub vigente verificada en documentación oficial.
- Administrador/entorno objetivo, identidades, límites de permisos y estrategia de evidencia.

## Procedimiento

1. Mapear requisito conceptual a capacidad GitHub sin alterar el requisito.
2. Diseñar permisos por job, eventos, inputs no confiables, runner y evidencias.
3. Diseñar evaluación negativa y reversión antes de proponer YAML/configuración.
4. Señalar limitaciones de plan, administración y entornos.
5. Entregar plan revisable; implementar solo con autorización humana explícita posterior.

## Salida / evidencia

Mapa de capability, borrador de configuración, evaluación prevista, permisos y checklist administrativo.

## Límites

No usa secretos reales, no crea environments/rulesets, no autoaprueba, no despliega y no sustituye a `delivery-audit`.
