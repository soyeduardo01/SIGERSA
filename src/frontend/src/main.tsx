import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import 'sweetalert2/dist/sweetalert2.min.css'
import './index.css'
import App from './App.tsx'

if (window.location.hash && !window.location.hash.startsWith('#/')) {
  window.history.replaceState(null, '', `#/${window.location.hash.slice(1)}`)
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
