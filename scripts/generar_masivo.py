"""Genera un .xlsx de gran volumen para probar el pipeline de carga masiva
fuera del archivo de muestra (200 filas). Streaming real (write_only=True de
openpyxl): memoria constante sin importar FILAS, no carga el libro en RAM
para escribirlo -- necesario para no reemplazar un problema de memoria del
lector por uno del generador.

Uso:
    python scripts/generar_masivo.py [filas] [ruta_salida]

Default: 2_000_000 filas -> samples/carga_masiva_2M.xlsx (NO se commitea,
ver .gitignore).
"""
import random
import sys
from pathlib import Path

from openpyxl import Workbook

FILAS = int(sys.argv[1]) if len(sys.argv) > 1 else 2_000_000
SALIDA = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("samples/carga_masiva_2M.xlsx")

# Periodos fuera de 2025-01/02/03 (los que usa el guion del video) para no
# chocar con una carga ya Finalizado en el reset de la demo.
PERIODOS = [f"2030-{m:02d}" for m in range(1, 13)]

wb = Workbook(write_only=True)
ws = wb.create_sheet()
ws.append(["Periodo", "CodigoProducto", "NombreProducto", "Precio"])

for i in range(FILAS):
    ws.append([
        random.choice(PERIODOS),
        f"P{i:08d}",  # único por fila -> ~0 rechazos por "Existente", mide inserción real
        f"Producto {i}",
        round(random.uniform(1, 999), 2),
    ])

SALIDA.parent.mkdir(parents=True, exist_ok=True)
wb.save(SALIDA)
print(f"{FILAS} filas -> {SALIDA} ({SALIDA.stat().st_size / 1_048_576:.1f} MB)")
