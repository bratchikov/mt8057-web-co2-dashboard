import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  base: '/ui/',
  server: {
    proxy: {
      // Proxy API requests
      '/api': {
        target: 'http://localhost:8072', // Your Go backend address
        changeOrigin: true,
      },
      // Proxy WebSocket connections
      '/ws': {
        target: 'ws://localhost:8072', // Your Go backend WebSocket address
        ws: true, // Enable WebSocket proxying
        changeOrigin: true,
      }
    }
  }
})
