import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5103',
        changeOrigin: true,
      },
      '/query-api': {
        target: 'http://localhost:5267',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/query-api/, ''),
      },
      '/ledger-api': {
        target: 'http://localhost:5075',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/ledger-api/, ''),
      },
    },
  },
})
