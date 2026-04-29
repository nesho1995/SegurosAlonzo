import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Bell, Check, RefreshCw, Zap } from 'lucide-react'
import { getEnvioAutomaticoConfig, type EnvioAutomaticoConfig } from '../api/configuracionApi'
import { getNotificaciones, marcarNotificacionLeida } from '../api/notificacionesApi'
import type { NotificacionInterna } from '../types/notificaciones'
import { dateFmt } from '../utils/formatters'
export function PageHeader({ eyebrow, title, description, onRefresh, action }: { eyebrow: string; title: string; description: string; onRefresh: () => void; action?: ReactNode }) { return (<header className="page-header"><div><span className="eyebrow">{eyebrow}</span><h1>{title}</h1><p>{description}</p></div><div className="header-actions">{action}<button className="icon-button" onClick={() => onRefresh()} title="Actualizar"><RefreshCw size={18} /><span>Actualizar</span></button></div></header>) }

export function AutoSendStatus() {
  const [config, setConfig] = useState<EnvioAutomaticoConfig | null>(null)

  async function load() {
    setConfig(await getEnvioAutomaticoConfig())
  }

  useEffect(() => {
    let alive = true
    getEnvioAutomaticoConfig()
      .then((data) => { if (alive) setConfig(data) })
      .catch(() => { if (alive) setConfig(null) })

    const onUpdated = () => void load().catch(() => setConfig(null))
    window.addEventListener('app:envios-updated', onUpdated)
    return () => {
      alive = false
      window.removeEventListener('app:envios-updated', onUpdated)
    }
  }, [])

  if (!config) return null

  const enabled = [
    config.autoEnviarReclamos,
    config.autoEnviarRecordatoriosPago,
    config.autoEnviarRecordatoriosPoliza,
  ].filter(Boolean).length

  return (
    <div className={enabled > 0 ? 'auto-status active' : 'auto-status off'} title="Estado de envios automaticos">
      <Zap size={16} />
      <strong>Auto {enabled}/3</strong>
      <span>Reclamos {config.autoEnviarReclamos ? 'activo' : 'apagado'}</span>
      <span>Pagos {config.autoEnviarRecordatoriosPago ? 'activo' : 'apagado'}</span>
      <span>Polizas {config.autoEnviarRecordatoriosPoliza ? 'activo' : 'apagado'}</span>
    </div>
  )
}

export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<NotificacionInterna[]>([])
  const [unread, setUnread] = useState(0)

  async function load() {
    const data = await getNotificaciones()
    setItems(data.items)
    setUnread(data.unread)
  }

  async function markRead(id: number) {
    await marcarNotificacionLeida(id)
    await load()
  }

  useEffect(() => {
    let alive = true
    getNotificaciones().then((data) => {
      if (!alive) return
      setItems(data.items)
      setUnread(data.unread)
    }).catch(() => undefined)
    return () => { alive = false }
  }, [])

  return (
    <div className="notification-bell">
      <button className="icon-button notification-button" onClick={() => setOpen(!open)} title="Notificaciones">
        <Bell size={18} />
        {unread > 0 && <span className="notification-count">{unread > 99 ? '99+' : unread}</span>}
      </button>
      {open && (
        <div className="notification-menu">
          <div className="notification-header">
            <strong>Notificaciones</strong>
            <button className="icon-button secondary" onClick={() => void load()}><RefreshCw size={15} /></button>
          </div>
          {items.length === 0 ? <div className="empty">Sin notificaciones.</div> : items.map((item) => (
            <div className={item.leida ? 'notification-item read' : 'notification-item'} key={item.id}>
              <div>
                <strong>{item.titulo}</strong>
                <span>{item.mensaje}</span>
                <small>{dateFmt.format(new Date(item.fechaCreacion))}</small>
              </div>
              {!item.leida && <button className="icon-button secondary" onClick={() => void markRead(item.id)} title="Marcar como leida"><Check size={15} /></button>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
