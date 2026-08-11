# Política propuesta: seguridad de workflows

Estado: **propuesta; requiere aprobación humana**.

Los workflows son código privilegiado. Los inputs no confiables no se ejecutan con secretos, escritura o identidad de deployment; las referencias externas se inventarían y fijan; los permisos se minimizan por job; los runners se clasifican por confianza. Implementa conceptualmente CTL-006 a CTL-008.
