# Gobernanza de agentes v0.1

## Principio de confianza

`intención humana → candidato generado por agente → verificación determinista independiente → CI independiente → gates de seguridad/supply chain → evidencia → aprobación proporcional → integración/despliegue`.

**ENGINEERING DECISION:** un agente de código no es una fuente de confianza. Su salida es input no confiable hasta que controles independientes y una identidad autorizada la validen. No se diseñarán prompts ocultos, mecanismos de evasión, instrucciones para engañar revisores ni privilegios que permitan al agente completar por sí solo una modificación material.

## Separación de funciones

| Responsabilidad | Actor permitido | Actor prohibido como único responsable |
| --- | --- | --- |
| Expresar intención/riesgo | Humano responsable | Agente. |
| Generar candidato | Humano o agente | N/A. |
| Verificar comportamiento | Tests/análisis deterministas y revisor independiente | El mismo agente generador como única prueba. |
| Auditar control/evidencia | `delivery-audit` o auditor independiente | Implementador del control como única fuente. |
| Aprobar materialidad | Rol humano definido por perfil | Agente. |
| Integrar/desplegar | Identidad de plataforma limitada, tras gates | Agente con autorización autónoma. |

## Superficies Codex

**FACT — SRC-OPENAI-CODEX:** Codex usa `AGENTS.md` para instrucciones durables y skills para flujos reutilizables. Por ello:

- `AGENTS.md` se mantiene corto, imperativo y no negociable.
- Documentación contiene conocimiento, fuentes y decisiones extensas.
- Skills encapsulan procedimientos repetibles con entrada/salida/evidencia.
- MCPs entregan datos/acciones autorizadas; no sustituyen políticas ni validación.

Las tres skills iniciales propuestas se describen en [skills](skills/). Ninguna se instala o habilita hasta aprobar TBD-AGENT-01 y el modelo de evaluación.
