import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { Login } from './Login';

function renderLogin() {
  render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/historial" element={<div>PANTALLA_HISTORIAL</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

async function enviarFormulario(email: string, password: string) {
  const usuario = userEvent.setup();
  await usuario.type(screen.getByLabelText(/email/i), email);
  await usuario.type(screen.getByLabelText(/contraseña/i), password);
  await usuario.click(screen.getByRole('button', { name: /ingresar/i }));
}

describe('Login', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('credenciales inválidas: muestra el error del backend y no navega', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ title: 'Credenciales inválidas' }),
    } as Response);

    renderLogin();
    await enviarFormulario('admin@reto.local', 'clave-mala');

    expect(await screen.findByRole('alert')).toHaveTextContent('Credenciales inválidas');
    expect(screen.queryByText('PANTALLA_HISTORIAL')).not.toBeInTheDocument();
  });

  it('credenciales válidas: guarda el token y navega a /historial', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        accessToken: 'header.payload.firma',
        expiraEn: '2026-08-08T00:00:00Z',
        refreshToken: 'un-refresh-token',
      }),
    } as Response);

    renderLogin();
    await enviarFormulario('admin@reto.local', 'Reto2026!');

    await waitFor(() => expect(screen.getByText('PANTALLA_HISTORIAL')).toBeInTheDocument());
    expect(sessionStorage.getItem('reto.tokens')).toContain('un-refresh-token');
  });
});
