# Estándar GitHub propuesto: despliegue

**PLATFORM CAPABILITY — SRC-GH-ENV:** un environment puede exigir revisión, evitar auto-revisión, restringir refs y retener secretos hasta que el job sea autorizado. Las reglas de protección son mecanismos de GitHub, no el modelo de autorización completo.

Para CTL-010, una futura implementación debe transportar: digest aprobado, perfil de riesgo, fuente de provenance, aprobador distinto cuando corresponda, claims OIDC, target explícito, resultado de health y referencia a rollback. El job no puede desplegar una etiqueta mutable como identidad única.

No hay proveedor, environment ni pipeline de deployment implementado. **TBD-ID-01**, **TBD-OPS-01** y **TBD-GH-01** son bloqueantes.
