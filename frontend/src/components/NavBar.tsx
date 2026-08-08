import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { emailDelToken, tokenActual } from '../api/client';

export function NavBar() {
  const { logout } = useAuth();
  const token = tokenActual();
  const email = token ? emailDelToken(token.accessToken) : null;

  return (
    <nav className="navbar">
      <span className="marca">Carga masiva</span>
      <NavLink to="/historial">Historial</NavLink>
      <NavLink to="/subir">Subir Excel</NavLink>
      <span className="espaciador" />
      {email && <span className="usuario">{email}</span>}
      <button type="button" onClick={logout}>Cerrar sesión</button>
    </nav>
  );
}
