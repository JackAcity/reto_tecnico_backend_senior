# Modelo de amenazas v0.1

## Método y alcance

El modelo cubre la cadena desde intención de cambio hasta runtime. Usa activos, actores, límites de confianza y rutas de ataque; no pretende una certificación STRIDE completa. Se revisa cuando cambien identidad, runners, proveedor, datos regulados o el modo de operación de agentes.

## Activos

| Activo | Propietario propuesto | Daño si se compromete |
| --- | --- | --- |
| Código fuente, historial y workflows | Ingeniería | Ejecución no autorizada, backdoor, pérdida de trazabilidad. |
| Identidades humanas y de agentes | Seguridad/ingeniería | Cambios o accesos bajo identidad suplantada. |
| `GITHUB_TOKEN`, tokens de App, OIDC y secretos | Plataforma/seguridad | Exfiltración, publicación o despliegue no autorizado. |
| Artefactos, digests, SBOM y provenance | Plataforma | Sustitución, consumo de build no verificable. |
| Configuración de environments y aprobaciones | Dueño de riesgo | Bypass de producción o autoaprobación. |
| Runners y caches | Plataforma | Ejecución con contaminación persistente o acceso lateral. |
| Evidencia, logs y auditoría | Operación/seguridad | Incapacidad de investigar o de demostrar controles. |
| Datos de producción y secretos de runtime | Dueño de datos | Violación de confidencialidad, integridad o disponibilidad. |
| MCP, servidores de herramientas y fuentes de contexto/retrieval | Plataforma de agentes | Instrucciones, datos o acciones envenenadas que alteran el candidato o exfiltran información. |
| Plano administrativo de SCM/CI | Dueño de repositorio/plataforma | Cambio de rulesets, Apps, environments o políticas que anula controles sin cambiar código. |

## Actores

- Desarrollador, revisor, responsable de release y administrador de plataforma.
- Agente de programación, que genera candidatos y usa herramientas bajo un permiso concreto.
- GitHub Actions/runner, GitHub App y proveedor cloud como identidades no humanas separadas.
- Operador de MCP, servidor de herramientas, proveedor de contexto/retrieval y administrador de SCM/CI como actores separados del agente de código.
- Colaborador malicioso, atacante externo, dependencia/acción comprometida y PR no confiable.
- Auditor/verificador, que debe poder observar sin depender de la identidad que produjo el cambio.

## Puntos de entrada

- Commit, pull request, comentario/metadata, trigger manual y cambio de configuración de SCM.
- Archivo de workflow, action/reusable workflow, dependencia, lockfile, artefacto y cache.
- Input, secreto, token, claim OIDC, API de proveedor, manifest y endpoint de deployment.
- Prompt, documento adjunto, salida de herramienta y diff producido por un agente.

## Rutas de amenaza y controles candidatos

| ID | Escenario | Ruta de ataque | Impacto | Controles candidatos | Riesgo residual |
| --- | --- | --- | --- | --- | --- |
| TM-01 | Identidad de desarrollador comprometida | Credencial robada integra código en main. | Backdoor o exfiltración. | Gobernanza de rama, MFA/identidad corporativa, revisión, auditoría. | Collusion o revisión negligente. |
| TM-02 | Identidad de agente | Prompt/contexto malicioso induce cambio inseguro o acceso fuera de alcance. | Código vulnerable o fuga de datos. | Aislamiento, permisos mínimos, instrucciones concisas, revisión humana y CI independiente. | Error semántico que pasa tests. |
| TM-03 | Token de GitHub | Workflow expone token con permisos de escritura. | Compromiso de repo, paquetes o acciones. | `permissions` mínimas por job, separación de eventos y auditoría. | Acción confiada comprometida. |
| TM-04 | GitHub App | App con scope excesivo modifica controles. | Bypass de gobernanza. | Registro de Apps, scopes mínimos, revisión y rotación. | Vulnerabilidad del proveedor/App. |
| TM-05 | Identidad cloud/OIDC | Claims débiles permiten asumir rol de producción. | Despliegue o lectura de secretos. | OIDC con condiciones de repo, branch, environment y audiencia; role separado. | Configuración cloud errónea. |
| TM-06 | Compromiso de repositorio | Cambio a workflow, policy o código evita gate. | Supply-chain poisoning. | Protección de archivos/ramas, CODEOWNERS real, required checks. | Administrador con bypass. |
| TM-07 | PR malicioso | Evento inseguro ejecuta código de fork con secretos. | Exfiltración y publicación. | Evitar ejecutar código no confiable con secretos, modelar eventos separados. | Vulnerabilidad del runner. |
| TM-08 | Dependencia comprometida | Paquete o lockfile introduce código malicioso. | Compromiso de build/runtime. | Revisión de dependencias, lockfiles, SCA, SBOM, actualización trazada. | Ataque desconocido o dependencia legítima maliciosa. |
| TM-09 | Acción de terceros | Referencia mutable cambia comportamiento. | Robo de token o build manipulado. | Inventario, allowlist y SHA inmutable; actualización revisada. | Compromiso del commit confiado. |
| TM-10 | Runner comprometido | Estado persistente/cache filtra secretos entre jobs. | Fuga lateral y manipulación de artefactos. | Runners efímeros/aislados, no self-hosted para PR no confiable, limpieza. | Compromiso de imagen base. |
| TM-11 | Secreto hardcodeado | Credencial entra en código, log o artefacto. | Acceso no autorizado. | Prevención/detección, revocación, redacción y capacitación. | Secreto usado antes de detectarse. |
| TM-12 | Sustitución de artefacto | Runtime consume etiqueta mutable o artefacto diferente. | Ejecución no aprobada. | Digest, provenance/SBOM, verificación de consumo y promoción. | Política de verificación no aplicada en runtime. |
| TM-13 | Forgery de provenance | Evidencia se genera desde build no confiable. | Falsa confianza. | Aislar builder, identidad verificable y validación de subject/predicate. | Errores de política de verificación. |
| TM-14 | Bypass de aprobación | Autor se autoaprueba o administrador salta gates sin registro. | Escalación a producción. | Environments, no autoaprobación, restricción de bypass y auditoría. | Emergencia real o abuso administrativo. |
| TM-15 | Escalación de environment | Artefacto o token de staging accede a producción. | Daño productivo. | Cuentas/roles/secretos separados y claims restringidos. | Configuración cruzada del proveedor. |
| TM-16 | Falta de recuperación | Migración o release sin rollback probado. | Indisponibilidad prolongada o corrupción. | Estrategia de rollback, backup/restauración y pruebas. | Cambios irreversibles. |
| TM-17 | Evidencia insuficiente | Logs se vencen o no enlazan commit/artifact/approver. | Auditoría imposible. | Modelo de evidencia, retención y exportación. | Acceso indebido o pérdida del proveedor. |
| TM-18 | Agente autoautoriza | Mismo agente genera, verifica, aprueba, mergea y despliega. | Error o abuso sin independencia. | Segregación de deberes, verificador determinista y aprobación humana. | Automatización coludida o políticas mal configuradas. |
| TM-19 | MCP, herramienta o contexto envenenado | Un servidor MCP, output de herramienta o fuente de retrieval comprometida entrega instrucciones/datos maliciosos al agente. | Cambio inseguro, exfiltración o uso indebido de credenciales. | Inventario y procedencia de herramientas, permisos mínimos, aislamiento, validación independiente y revisión humana. | Herramienta confiada comprometida o validación semántica insuficiente. |
| TM-20 | Compromiso del plano administrativo | Un administrador, App o integración altera rulesets, environments, políticas, Apps o configuración organizacional. | Bypass silencioso de gates, despliegue o acceso privilegiado. | Administración mínima, auditoría/export de configuración, revisión de cambios de control y alertas de bypass. | Abuso de administrador legítimo o compromiso del proveedor. |

## Priorización inicial

**HYPOTHESIS:** TM-03, TM-05, TM-07, TM-09, TM-10, TM-12, TM-14, TM-18, TM-19 y TM-20 son de prioridad alta antes de conceder autoridad material a CI/CD o agentes. La probabilidad e impacto cuantificados requieren activos, proveedor, exposición pública y tolerancia de riesgo confirmados; no se inventan en v0.1.

## Verificación del modelo

Los casos de `evals/vulnerable/` y `evals/compliant/` convierten varias rutas en pruebas. Una prueba que no detecta su escenario actualiza el control o la evaluación; no se oculta como excepción.
