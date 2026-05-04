import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, Info, X } from 'lucide-react'

type ToastType = 'success' | 'error' | 'info'
type ToastEvent = { message: string; type?: ToastType }
type ToastItem = ToastEvent & { id: number; type: ToastType }

export function notify(message: string, type: ToastType = 'info') {
  window.dispatchEvent(new CustomEvent<ToastEvent>('app:toast', { detail: { message, type } }))
}

export function ToastHost() {
  const [items, setItems] = useState<ToastItem[]>([])

  useEffect(() => {
    const onToast = (event: Event) => {
      const detail = (event as CustomEvent<ToastEvent>).detail
      if (!detail?.message) return
      const item: ToastItem = { id: Date.now() + Math.random(), type: detail.type || 'info', message: detail.message }
      setItems((current) => [...current.slice(-3), item])
      window.setTimeout(() => setItems((current) => current.filter((toast) => toast.id !== item.id)), 4500)
    }
    window.addEventListener('app:toast', onToast)
    return () => window.removeEventListener('app:toast', onToast)
  }, [])

  if (items.length === 0) return null

  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {items.map((item) => {
        const Icon = item.type === 'success' ? CheckCircle2 : item.type === 'error' ? AlertTriangle : Info
        return (
          <div className={`toast toast-${item.type}`} key={item.id}>
            <Icon size={18} />
            <span>{item.message}</span>
            <button type="button" onClick={() => setItems((current) => current.filter((toast) => toast.id !== item.id))} aria-label="Cerrar mensaje">
              <X size={16} />
            </button>
          </div>
        )
      })}
    </div>
  )
}
