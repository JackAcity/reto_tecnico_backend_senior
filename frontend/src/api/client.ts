const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';
const CLAVE_SESION = 'reto.tokens';

export class ApiError extends Error {
  status: number;
  body?: unknown;

  constructor(message: string, status: number, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

export type Tokens = { accessToken: string; expiraEn: string; refreshToken: string };

export type ResumenCarga = {
  idCarga: number;
  nombreArchivo: string;
  usuario: string;
  fechaRegistro: string;
  estado: string;
  totalFilas: number;
  filasInsertadas: number;
  filasRechazadas: number;
  fechaFin: string | null;
};

export type PeriodoCarga = { periodo: string; estado: string; filasInsertadas: number };

export type ErrorAuditado = {
  numeroFila: number;
  periodo: string | null;
  codigoProducto: string | null;
  columna: string | null;
  motivo: string;
  valorCrudo: string | null;
};

export type DetalleCarga = {
  carga: ResumenCarga;
  rutaArchivo: string | null;
  mensajeError: string | null;
  correlationId: string;
  periodos: PeriodoCarga[];
  errores: ErrorAuditado[];
  totalErrores: number;
};

export type ResultadoRegistro = { idCarga: number; estado: string; error?: string };

// Estado en memoria + sessionStorage (sobrevive a un refresh de la pestaña, no a
// cerrarla — razonable para un cliente de demo, no hace falta "recordarme").
let tokens: Tokens | null = leerTokensGuardados();

type Escucha = () => void;
const escuchas = new Set<Escucha>();

function leerTokensGuardados(): Tokens | null {
  const crudo = sessionStorage.getItem(CLAVE_SESION);
  return crudo ? (JSON.parse(crudo) as Tokens) : null;
}

function guardarTokens(nuevos: Tokens | null): void {
  tokens = nuevos;
  if (nuevos) sessionStorage.setItem(CLAVE_SESION, JSON.stringify(nuevos));
  else sessionStorage.removeItem(CLAVE_SESION);
  escuchas.forEach((escucha) => escucha());
}

/** Para useSyncExternalStore en AuthContext: reactivo también cuando el refresh falla a mitad de un polling. */
export function suscribirseATokens(escucha: Escucha): () => void {
  escuchas.add(escucha);
  return () => escuchas.delete(escucha);
}

export function haySesion(): boolean {
  return tokens !== null;
}

export function tokenActual(): Tokens | null {
  return tokens;
}

export function emailDelToken(accessToken: string): string | null {
  try {
    const payload = JSON.parse(atob(accessToken.split('.')[1])) as { email?: string };
    return payload.email ?? null;
  } catch {
    return null;
  }
}

function extraerMensajeError(data: unknown, fallback: string): string {
  if (data && typeof data === 'object') {
    if ('errors' in data) {
      const errores = (data as { errors: Record<string, string[]> }).errors;
      const mensajes = Object.values(errores).flat();
      if (mensajes.length > 0) return mensajes.join(' ');
    }
    if ('title' in data) return String((data as { title: unknown }).title);
    if ('error' in data && typeof (data as { error: unknown }).error === 'string') {
      return (data as { error: string }).error;
    }
  }
  return fallback;
}

export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });

  const data = await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(extraerMensajeError(data, 'Credenciales inválidas'), res.status, data);

  guardarTokens(data as Tokens);
}

export function logout(): void {
  guardarTokens(null);
}

async function refrescar(): Promise<boolean> {
  if (!tokens) return false;

  const res = await fetch(`${BASE_URL}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: tokens.refreshToken }),
  });

  if (!res.ok) {
    guardarTokens(null);
    return false;
  }

  guardarTokens((await res.json()) as Tokens);
  return true;
}

/** Reintenta una vez con refresh transparente ante 401 — la policy de permiso (403) no se reintenta, es definitiva. */
async function peticionAutenticada(path: string, init: RequestInit = {}, reintentado = false): Promise<Response> {
  const headers = new Headers(init.headers);
  if (tokens) headers.set('Authorization', `Bearer ${tokens.accessToken}`);

  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers });

  if (res.status === 401 && !reintentado && tokens) {
    if (await refrescar()) return peticionAutenticada(path, init, true);
  }

  return res;
}

export async function obtenerHistorial(limite = 50): Promise<ResumenCarga[]> {
  const res = await peticionAutenticada(`/cargas?limite=${limite}`);
  if (!res.ok) throw new ApiError('No se pudo obtener el historial.', res.status);
  return res.json();
}

export async function obtenerDetalle(idCarga: number): Promise<DetalleCarga> {
  const res = await peticionAutenticada(`/cargas/${idCarga}`);
  if (!res.ok) {
    const data = await res.json().catch(() => null);
    throw new ApiError(extraerMensajeError(data, 'No se pudo obtener el detalle.'), res.status, data);
  }
  return res.json();
}

export async function subirArchivo(archivo: File): Promise<ResultadoRegistro> {
  const form = new FormData();
  form.append('archivo', archivo);

  const res = await peticionAutenticada('/cargas', { method: 'POST', body: form });
  const data = await res.json().catch(() => null);

  // 201 (Pendiente) y 502 (Fallida, §C7) devuelven el mismo shape con idCarga —
  // ambos son "resultado", no excepción. Solo 4xx de validación/permiso son error.
  if (res.status >= 400 && res.status !== 502) {
    throw new ApiError(extraerMensajeError(data, 'No se pudo subir el archivo.'), res.status, data);
  }

  return data as ResultadoRegistro;
}

export async function descargarContenido(idCarga: number, nombreArchivo: string): Promise<void> {
  const res = await peticionAutenticada(`/cargas/${idCarga}/contenido`);
  if (!res.ok) throw new ApiError('No se pudo descargar el archivo.', res.status);

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombreArchivo;
  enlace.click();
  URL.revokeObjectURL(url);
}
