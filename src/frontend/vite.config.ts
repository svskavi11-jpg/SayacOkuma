import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  base: '/', // Asset yollari kokten verilsin (/assets/...) — nginx reverse proxy ile uyumlu
  plugins: [react()],
  server: {
    port: 5173,
    // Kural #2: no absolute paths - proxy target overridable via env.
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5199',
        changeOrigin: true,
        // Dev'de anahtar tanimliysa (VITE_API_KEY) proxy ekler; prod'da bunu nginx yapar (bundle'a girmez).
        headers: process.env.VITE_API_KEY ? { 'X-Api-Key': process.env.VITE_API_KEY } : {},
      },
    },
  },
  build: {
    // Code-split: vendor kutuphaneleri ayri chunk (tarayici cache'i + ilk yukleme hizlanir).
    rolldownOptions: {
      output: {
        advancedChunks: {
          groups: [
            { name: 'vendor-antd', test: /node_modules[\\/](antd|@ant-design|@rc-component)/ },
            { name: 'vendor-react', test: /node_modules[\\/](react|react-dom|react-router|@tanstack|dayjs)/ },
          ],
        },
      },
    },
  },
})