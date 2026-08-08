const COLOR_POR_ESTADO: Record<string, string> = {
  Pendiente: 'gris',
  EnProceso: 'azul',
  Cargado: 'azul',
  Finalizado: 'verde',
  Notificado: 'verde',
  Rechazada: 'rojo',
  Bloqueada: 'ambar',
  Fallida: 'rojo',
};

export function EstadoBadge({ estado }: { estado: string }) {
  const color = COLOR_POR_ESTADO[estado] ?? 'gris';
  return <span className={`badge badge-${color}`}>{estado}</span>;
}
