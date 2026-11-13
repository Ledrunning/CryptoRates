import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  // The gateway allows exactly this origin in Cors:AllowedOrigins.
  server: { port: 5173, strictPort: true },
})
