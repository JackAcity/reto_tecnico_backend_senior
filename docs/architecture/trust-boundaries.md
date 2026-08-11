# Límites de confianza v0.1

| Boundary ID | Lado A | Lado B | Activos en tránsito | Riesgo principal | Control conceptual candidato |
| --- | --- | --- | --- | --- | --- |
| TB-01 | Desarrollador humano | SCM | Código, identidad, aprobación | Cuenta comprometida o cambio no trazado | Autenticación fuerte, branch governance, auditoría. |
| TB-02 | Agente de código | Repositorio / herramientas | Prompt, contexto, diff, tokens | Cambio inseguro o exfiltración por instrucciones no confiables | Menor privilegio, aislamiento, revisión y verificación independiente. |
| TB-03 | Pull request no confiable | Ejecutor CI | Código, metadata, dependencias | Ejecución de código malicioso con secretos/permisos | Separar eventos no confiables, sin secretos, permisos mínimos. |
| TB-04 | Workflow versionado | Runner | Definición de build, token, secretos | Modificación de workflow o runner comprometido | Protección de archivos, pinning, runner efímero/aislado. |
| TB-05 | CI | Registro de artefactos | Imagen, paquete, digest, attestation | Sustitución o publicación no autorizada | Identidad inmutable, autorización limitada, provenance. |
| TB-06 | CI | Proveedor cloud | Token OIDC, claims, despliegue | Escalación de ambiente o credenciales duraderas | Federación con condiciones estrechas y scopes mínimos. |
| TB-07 | Registro | Runtime | Digest, SBOM, manifest | Artefacto distinto al aprobado | Verificación de digest/attestation antes de ejecutar. |
| TB-08 | Runtime | Observabilidad/evidencia | Logs, métricas, auditoría | Falta de trazabilidad, borrado o manipulación de evidencia | Retención, acceso restringido e integridad verificable. |
| TB-09 | Agente de código | MCP / herramienta / fuente de contexto | Prompt, instrucciones, resultados, tokens y datos recuperados | Envenenamiento de contexto, acción no autorizada o exfiltración | Inventario/procedencia, mínimo privilegio, aislamiento y validación independiente. |
| TB-10 | Administrador / App | Plano de control SCM/CI | Rulesets, environments, Apps, policies y bypasses | Anulación silenciosa de controles | Administración mínima, auditoría de configuración y revisión de cambios de control. |

**ENGINEERING DECISION:** los cambios producidos por agentes cruzan TB-02 como entrada no confiable. Su autoría asistida se registra cuando el proceso lo permita, pero no reduce requisitos de revisión, tests o aprobación.
