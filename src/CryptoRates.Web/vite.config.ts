import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      // We proxy SignalR hubs (make sure to set `ws: true` for WebSockets!)
      '/hubs': {
        target: 'https://localhost:7208',
        changeOrigin: true,
        secure: false, // Ignores a self-signed .NET SSL certificate
        ws: true       
      },
      // If you have a REST API at the path /api, include that here as well
      '/api': {
        target: 'https://localhost:7208',
        changeOrigin: true,
        secure: false
      }
    }
  }
})