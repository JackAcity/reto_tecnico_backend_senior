# Rúbrica de clasificación de riesgo v0.1

## Propósito y límites

Esta rúbrica hace repetible la asignación inicial de perfil para un cambio. No es una
certificación, no sustituye una evaluación de seguridad y no permite rebajar el
riesgo por conveniencia. Aplica antes de seleccionar controles de entrega, no después
de configurar una plataforma.

## Factores y puntuación

Para cada factor se asigna `0` (ninguno), `1` (limitado) o `2` (material). La persona
que propone el cambio debe registrar el razonamiento y enlazar el diff, ticket o
plan correspondiente.

| Factor | 0 | 1 | 2 / gatillo alto |
| --- | --- | --- | --- |
| Sensibilidad de datos | Sin datos sensibles. | Datos internos limitados o sintéticos. | PII, secretos, datos regulados o financieros. |
| Blast radius | Componente local, sin usuarios. | Un servicio o conjunto limitado de usuarios. | Producción compartida, varios servicios/clientes o impacto amplio. |
| Reversibilidad | Rollback trivial, sin estado. | Rollback planificado y probado parcialmente. | Migración/destrucción irreversible, restauración compleja o ventana reducida. |
| Acceso a producción | Ninguno. | Lectura o staging con alcance limitado. | Escritura, despliegue o administración de producción. |
| IAM / seguridad | Sin cambio de permiso ni frontera. | Scope/rol limitado sin elevar privilegio. | Autenticación, autorización, secretos, roles, políticas o privilegio elevado. |
| Persistencia | Sin estado persistente. | Datos de servicio reversibles. | Esquema, datos de cliente, retención, borrado o consistencia crítica. |
| Infraestructura | Sin cambio de plataforma. | Configuración acotada y reversible. | IaC, red, runtime, cifrado, IAM o cambio con efecto transversal. |
| Criticidad de negocio | Demo o capacidad no crítica. | Función operativa acotada. | Ingresos, seguridad, cumplimiento, disponibilidad crítica o confianza pública. |

## Regla de clasificación

1. **Alto** si algún factor tiene `2` en datos sensibles, blast radius,
   reversibilidad, acceso a producción, IAM/seguridad, persistencia o
   infraestructura; o si la suma de todos los factores es `8` o más.
2. **Bajo** solo si la suma es `0–2`, ningún factor vale `2` y el cambio no toca
   producción, IAM, secretos, datos persistentes ni infraestructura.
3. **Medio** en los demás casos.

Un revisor puede elevar el resultado ante evidencia nueva. Un resultado más bajo
solo puede aceptarse mediante una excepción temporal que deje visible la puntuación,
el riesgo residual, controles compensatorios, dueño y vencimiento.

## Autoridad y evidencia mínima

| Paso | Autoridad propuesta | Evidencia mínima |
| --- | --- | --- |
| Clasificación inicial | Autor humano del cambio. | Rúbrica rellenada, diff/ticket y perfil calculado. |
| Validación | Revisor de pares para medio; revisor independiente y dueño del componente para alto. | Confirmación de factores y controles aplicables. |
| Aceptación de riesgo alto o excepción | Dueño de riesgo humano, con seguridad y operación/datos cuando corresponda. | Decisión, riesgo residual, compensación y vencimiento. |
| Cambio de perfil | Dueño de riesgo; nunca un agente como autoridad única. | Historial del perfil anterior, motivo y referencias. |

**DECISIÓN PENDIENTE — `TBD-RISK-01`:** los roles son una propuesta operable. El
repositorio aún debe nombrar a la autoridad humana y aceptar formalmente los umbrales
antes de activar controles de promoción.

## Ejemplos de aplicación

| Cambio | Resultado | Motivo |
| --- | --- | --- |
| Corrección de documentación sin artefacto ejecutable. | Bajo. | Reversible, sin datos ni runtime. |
| Actualización de paquete en un servicio no productivo. | Medio. | Dependencia y superficie de build cambian; requiere CTL-004 y CI. |
| Migración de esquema con datos de cliente. | Alto. | Persistencia e irreversibilidad potencial; exige CTL-013, recuperación y autorización proporcional. |
| Cambio de rol OIDC que permite desplegar producción. | Alto. | IAM y acceso de producción son gatillos altos. |
