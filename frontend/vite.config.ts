import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // design.md §C14 — sin esto, cualquier sitio podría enmarcar el cliente en un
  // iframe invisible y usar clickjacking contra acciones autenticadas (ej.
  // "Cerrar sesión"). frame-ancestors no es soportado vía <meta>, tiene que
  // ser un header real; este es el único server HTTP que este frontend usa.
  server: {
    headers: { 'X-Frame-Options': 'DENY' },
  },
  preview: {
    headers: { 'X-Frame-Options': 'DENY' },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    globals: true,
  },
})
