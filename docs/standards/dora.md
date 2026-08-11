# DORA

## Estado de la fuente

**RESEARCH EVIDENCE — SRC-DORA-CD, SRC-DORA-CI, SRC-DORA-TBD y SRC-DORA-SMALL-BATCHES:** DORA relaciona entrega continua, CI, testing continuo, pequeños lotes, seguridad integrada y observabilidad con mejor rendimiento de entrega, disponibilidad y menor dolor de despliegue. Es investigación, no un estándar de cumplimiento.

## Uso correcto

**FACT:** DORA describe CI alrededor de integración frecuente a mainline/trunk, pequeños lotes y pruebas automatizadas rápidas; describe TBD como práctica necesaria de CI en su modelo. **ENGINEERING DECISION:** esta arquitectura conservará esa evidencia sin imponer un número de ramas, límite de minutos o topología de Git universal. El modelo de ramas del repositorio se decide mediante TBD-SCM-01.

**ENGINEERING DECISION:** se proponen métricas para aprender sobre el sistema: lead time, frecuencia de despliegue, change failure rate, tiempo de recuperación y calidad de evidencia. No se fijan objetivos ni se usan para evaluar personas sin una decisión explícita de la organización.

**ASSUMPTION:** el futuro runtime emitirá eventos suficientes para calcular las métricas sin inferencias manuales. La fuente, retención y semántica de cada métrica son preguntas abiertas de operación.
