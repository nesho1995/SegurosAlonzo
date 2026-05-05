import { useEffect, useRef, useState } from 'react'
import { CheckCheck, MessageSquare, RefreshCw, Send, User } from 'lucide-react'
import {
  cambiarEstado,
  getConversaciones,
  getMensajes,
  marcarLeido,
  responder,
  type ConversacionDetalle,
  type ConversacionListItem,
  type MensajeDto,
} from '../api/whatsappBandeja'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function timeFmt(iso: string) {
  const d = new Date(iso)
  const now = new Date()
  const diffDays = Math.floor((now.getTime() - d.getTime()) / 86400000)
  if (diffDays === 0) return d.toLocaleTimeString('es-HN', { hour: '2-digit', minute: '2-digit' })
  if (diffDays === 1) return 'Ayer'
  if (diffDays < 7) return d.toLocaleDateString('es-HN', { weekday: 'short' })
  return d.toLocaleDateString('es-HN', { day: '2-digit', month: '2-digit' })
}

function estadoColor(estado: string) {
  if (estado === 'abierta') return '#22c55e'
  if (estado === 'en_espera') return '#f59e0b'
  return '#94a3b8'
}

function estadoLabel(estado: string) {
  if (estado === 'abierta') return 'Abierta'
  if (estado === 'en_espera') return 'En espera'
  return 'Resuelta'
}

// ─── Componente principal ─────────────────────────────────────────────────────

export function WhatsAppBandejaView() {
  const [conversaciones, setConversaciones] = useState<ConversacionListItem[]>([])
  const [total, setTotal] = useState(0)
  const [filtroEstado, setFiltroEstado] = useState('todas')
  const [buscar, setBuscar] = useState('')
  const [cargandoLista, setCargandoLista] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [convSeleccionada, setConvSeleccionada] = useState<ConversacionDetalle | null>(null)
  const [mensajes, setMensajes] = useState<MensajeDto[]>([])
  const [totalMensajes, setTotalMensajes] = useState(0)
  const [cargandoMensajes, setCargandoMensajes] = useState(false)
  const [respuesta, setRespuesta] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [msgError, setMsgError] = useState<string | null>(null)

  const chatEndRef = useRef<HTMLDivElement>(null)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  async function cargarLista(mostrarCarga = true) {
    if (mostrarCarga) setCargandoLista(true)
    setError(null)
    try {
      const { items, total: t } = await getConversaciones({
        estado: filtroEstado === 'todas' ? undefined : filtroEstado,
        buscar: buscar.trim() || undefined,
        limit: 100,
      })
      setConversaciones(items)
      setTotal(t)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error cargando conversaciones.')
    } finally {
      if (mostrarCarga) setCargandoLista(false)
    }
  }

  async function abrirConversacion(conv: ConversacionListItem) {
    setCargandoMensajes(true)
    setMsgError(null)
    setRespuesta('')
    try {
      const { conversacion, items, total: t } = await getMensajes(conv.id, { limit: 100 })
      setConvSeleccionada(conversacion)
      setMensajes(items)
      setTotalMensajes(t)
      if (conv.noLeidos > 0) {
        await marcarLeido(conv.id)
        setConversaciones(prev =>
          prev.map(c => c.id === conv.id ? { ...c, noLeidos: 0 } : c)
        )
      }
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error cargando mensajes.')
    } finally {
      setCargandoMensajes(false)
    }
  }

  async function refrescarMensajes() {
    if (!convSeleccionada) return
    try {
      const { items, total: t } = await getMensajes(convSeleccionada.id, { limit: 100 })
      setMensajes(items)
      setTotalMensajes(t)
    } catch { /* silencioso */ }
  }

  async function enviarRespuesta() {
    if (!convSeleccionada || !respuesta.trim()) return
    setEnviando(true)
    setMsgError(null)
    try {
      await responder(convSeleccionada.id, respuesta.trim())
      setRespuesta('')
      await refrescarMensajes()
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error enviando mensaje.')
    } finally {
      setEnviando(false)
    }
  }

  async function cambiarEstadoConv(estado: string) {
    if (!convSeleccionada) return
    try {
      await cambiarEstado(convSeleccionada.id, estado)
      setConvSeleccionada(prev => prev ? { ...prev, estado } : prev)
      setConversaciones(prev =>
        prev.map(c => c.id === convSeleccionada.id ? { ...c, estado: estado as ConversacionListItem['estado'] } : c)
      )
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error cambiando estado.')
    }
  }

  // Scroll al fondo del chat cuando llegan mensajes nuevos
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [mensajes])

  // Cargar lista al cambiar filtros
  useEffect(() => {
    cargarLista()
  }, [filtroEstado, buscar])

  // Auto-refresh cada 10s
  useEffect(() => {
    intervalRef.current = setInterval(() => {
      cargarLista(false)
      if (convSeleccionada) refrescarMensajes()
    }, 10000)
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current)
    }
  }, [filtroEstado, buscar, convSeleccionada])

  const tabs = [
    { key: 'todas', label: 'Todas' },
    { key: 'abierta', label: 'Abiertas' },
    { key: 'en_espera', label: 'En espera' },
    { key: 'resuelta', label: 'Resueltas' },
  ]

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      {/* Header */}
      <div style={{ padding: '16px 24px 0', borderBottom: '1px solid var(--border, #e2e8f0)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
          <MessageSquare size={22} style={{ color: '#25d366' }} />
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Bandeja WhatsApp</h1>
          <span style={{
            marginLeft: 'auto', fontSize: 12, color: '#64748b',
            background: '#f1f5f9', borderRadius: 6, padding: '2px 8px'
          }}>
            {total} conversaciones
          </span>
          <button
            onClick={() => cargarLista()}
            title="Actualizar"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#64748b', padding: 4 }}
          >
            <RefreshCw size={16} />
          </button>
        </div>

        {/* Tabs de estado */}
        <div style={{ display: 'flex', gap: 4, marginBottom: 0 }}>
          {tabs.map(tab => (
            <button
              key={tab.key}
              onClick={() => setFiltroEstado(tab.key)}
              style={{
                padding: '6px 14px',
                border: 'none',
                borderRadius: '6px 6px 0 0',
                cursor: 'pointer',
                fontSize: 13,
                fontWeight: filtroEstado === tab.key ? 600 : 400,
                background: filtroEstado === tab.key ? '#fff' : 'transparent',
                color: filtroEstado === tab.key ? '#1e293b' : '#64748b',
                borderBottom: filtroEstado === tab.key ? '2px solid #25d366' : '2px solid transparent',
              }}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {/* Contenido en dos paneles */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>

        {/* Panel izquierdo: lista de conversaciones */}
        <div style={{
          width: 320,
          minWidth: 260,
          borderRight: '1px solid var(--border, #e2e8f0)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}>
          {/* Búsqueda */}
          <div style={{ padding: '10px 12px', borderBottom: '1px solid var(--border, #e2e8f0)' }}>
            <input
              type="text"
              placeholder="Buscar nombre o teléfono..."
              value={buscar}
              onChange={e => setBuscar(e.target.value)}
              style={{
                width: '100%', boxSizing: 'border-box',
                padding: '7px 10px', border: '1px solid #e2e8f0',
                borderRadius: 6, fontSize: 13, outline: 'none',
              }}
            />
          </div>

          {/* Lista */}
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {error && (
              <div style={{ padding: 16, color: '#dc2626', fontSize: 13 }}>{error}</div>
            )}
            {cargandoLista && !conversaciones.length && (
              <div style={{ padding: 24, textAlign: 'center', color: '#94a3b8', fontSize: 13 }}>
                Cargando...
              </div>
            )}
            {!cargandoLista && !conversaciones.length && (
              <div style={{ padding: 24, textAlign: 'center', color: '#94a3b8', fontSize: 13 }}>
                Sin conversaciones
              </div>
            )}
            {conversaciones.map(conv => (
              <ConvItem
                key={conv.id}
                conv={conv}
                selected={convSeleccionada?.id === conv.id}
                onClick={() => abrirConversacion(conv)}
              />
            ))}
          </div>
        </div>

        {/* Panel derecho: chat */}
        {!convSeleccionada ? (
          <div style={{
            flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#94a3b8', fontSize: 15, flexDirection: 'column', gap: 12
          }}>
            <MessageSquare size={48} style={{ opacity: 0.25 }} />
            <span>Selecciona una conversación</span>
          </div>
        ) : (
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
            {/* Header del chat */}
            <div style={{
              padding: '12px 16px',
              borderBottom: '1px solid var(--border, #e2e8f0)',
              display: 'flex', alignItems: 'center', gap: 10,
              background: '#fafafa',
            }}>
              <div style={{
                width: 38, height: 38, borderRadius: '50%',
                background: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}>
                <User size={18} style={{ color: '#64748b' }} />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: 14 }}>
                  {convSeleccionada.nombreContacto ?? convSeleccionada.telefono}
                </div>
                <div style={{ fontSize: 12, color: '#64748b' }}>{convSeleccionada.telefono}</div>
              </div>
              {/* Selector de estado */}
              <select
                value={convSeleccionada.estado}
                onChange={e => cambiarEstadoConv(e.target.value)}
                style={{
                  border: '1px solid #e2e8f0', borderRadius: 6,
                  padding: '4px 8px', fontSize: 12, cursor: 'pointer',
                  background: '#fff',
                }}
              >
                <option value="abierta">Abierta</option>
                <option value="en_espera">En espera</option>
                <option value="resuelta">Resuelta</option>
              </select>
              <span style={{
                display: 'inline-block', width: 10, height: 10, borderRadius: '50%',
                background: estadoColor(convSeleccionada.estado),
                flexShrink: 0,
              }} />
            </div>

            {/* Mensajes */}
            <div style={{ flex: 1, overflowY: 'auto', padding: '16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              {cargandoMensajes && (
                <div style={{ textAlign: 'center', color: '#94a3b8', fontSize: 13, padding: 24 }}>
                  Cargando mensajes...
                </div>
              )}
              {msgError && (
                <div style={{ color: '#dc2626', fontSize: 13, padding: 8 }}>{msgError}</div>
              )}
              {!cargandoMensajes && mensajes.length === 0 && (
                <div style={{ textAlign: 'center', color: '#94a3b8', fontSize: 13, padding: 24 }}>
                  Sin mensajes aún
                </div>
              )}
              {mensajes.map(msg => (
                <BurbujaMensaje key={msg.id} msg={msg} />
              ))}
              <div ref={chatEndRef} />
            </div>

            {/* Input de respuesta */}
            <div style={{
              borderTop: '1px solid var(--border, #e2e8f0)',
              padding: '10px 12px',
              display: 'flex', gap: 8, alignItems: 'flex-end',
              background: '#fafafa',
            }}>
              <textarea
                value={respuesta}
                onChange={e => setRespuesta(e.target.value)}
                placeholder="Escribe un mensaje..."
                rows={2}
                onKeyDown={e => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault()
                    enviarRespuesta()
                  }
                }}
                style={{
                  flex: 1, resize: 'none', border: '1px solid #e2e8f0',
                  borderRadius: 8, padding: '8px 10px', fontSize: 13,
                  outline: 'none', fontFamily: 'inherit',
                }}
              />
              <button
                onClick={enviarRespuesta}
                disabled={enviando || !respuesta.trim()}
                style={{
                  background: enviando || !respuesta.trim() ? '#e2e8f0' : '#25d366',
                  color: enviando || !respuesta.trim() ? '#94a3b8' : '#fff',
                  border: 'none', borderRadius: 8, padding: '10px 14px',
                  cursor: enviando || !respuesta.trim() ? 'not-allowed' : 'pointer',
                  display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 600,
                }}
              >
                <Send size={15} />
                {enviando ? 'Enviando...' : 'Enviar'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Sub-componentes ──────────────────────────────────────────────────────────

function ConvItem({
  conv, selected, onClick
}: {
  conv: ConversacionListItem
  selected: boolean
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      style={{
        width: '100%', textAlign: 'left', border: 'none', padding: '10px 12px',
        cursor: 'pointer', borderBottom: '1px solid #f1f5f9',
        background: selected ? '#f0fdf4' : 'transparent',
        display: 'flex', gap: 10, alignItems: 'flex-start',
      }}
    >
      {/* Avatar */}
      <div style={{
        width: 40, height: 40, borderRadius: '50%', flexShrink: 0,
        background: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'center',
        position: 'relative',
      }}>
        <User size={18} style={{ color: '#64748b' }} />
        {conv.noLeidos > 0 && (
          <span style={{
            position: 'absolute', top: -2, right: -2,
            background: '#25d366', color: '#fff', borderRadius: '50%',
            fontSize: 10, fontWeight: 700, minWidth: 18, height: 18,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            padding: '0 4px',
          }}>
            {conv.noLeidos > 99 ? '99+' : conv.noLeidos}
          </span>
        )}
      </div>

      <div style={{ flex: 1, overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
          <span style={{ fontWeight: conv.noLeidos > 0 ? 700 : 500, fontSize: 13, textOverflow: 'ellipsis', whiteSpace: 'nowrap', overflow: 'hidden', maxWidth: 160 }}>
            {conv.nombreContacto ?? conv.telefono}
          </span>
          <span style={{ fontSize: 11, color: '#94a3b8', flexShrink: 0, marginLeft: 4 }}>
            {timeFmt(conv.ultimaActividad)}
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 2 }}>
          {conv.ultimoDireccion === 'saliente' && (
            <CheckCheck size={13} style={{ color: '#64748b', flexShrink: 0 }} />
          )}
          <span style={{
            fontSize: 12, color: '#64748b',
            whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
            fontWeight: conv.noLeidos > 0 ? 600 : 400,
          }}>
            {conv.ultimoMensaje
              ? conv.ultimoMensaje.slice(0, 60)
              : <span style={{ fontStyle: 'italic' }}>Sin mensajes</span>}
          </span>
        </div>
        <div style={{ marginTop: 3 }}>
          <span style={{
            fontSize: 10, padding: '1px 6px', borderRadius: 4,
            background: estadoColor(conv.estado) + '22',
            color: estadoColor(conv.estado),
            fontWeight: 600,
          }}>
            {estadoLabel(conv.estado)}
          </span>
        </div>
      </div>
    </button>
  )
}

function BurbujaMensaje({ msg }: { msg: MensajeDto }) {
  const esSaliente = msg.direccion === 'saliente'

  return (
    <div style={{
      display: 'flex',
      justifyContent: esSaliente ? 'flex-end' : 'flex-start',
    }}>
      <div style={{
        maxWidth: '70%',
        background: esSaliente ? '#dcf8c6' : '#fff',
        border: '1px solid #e2e8f0',
        borderRadius: esSaliente ? '12px 2px 12px 12px' : '2px 12px 12px 12px',
        padding: '8px 12px',
        boxShadow: '0 1px 2px rgba(0,0,0,0.06)',
      }}>
        {msg.tipoContenido !== 'texto' && (
          <div style={{
            fontSize: 11, color: '#64748b', marginBottom: 4,
            display: 'flex', alignItems: 'center', gap: 4,
          }}>
            <span style={{ fontSize: 16 }}>
              {msg.tipoContenido === 'imagen' ? '🖼️' :
               msg.tipoContenido === 'documento' ? '📄' :
               msg.tipoContenido === 'audio' ? '🎵' :
               msg.tipoContenido === 'video' ? '🎥' : '📎'}
            </span>
            {msg.mediaNombre ?? msg.tipoContenido}
            {msg.mediaId && (
              <span style={{ fontSize: 10, color: '#94a3b8' }}>(ID: {msg.mediaId.slice(0, 12)}...)</span>
            )}
          </div>
        )}
        {msg.contenido && (
          <div style={{ fontSize: 13, lineHeight: 1.5, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
            {msg.contenido}
          </div>
        )}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 4,
          justifyContent: 'flex-end', marginTop: 4,
        }}>
          {esSaliente && msg.nombreUsuario && (
            <span style={{ fontSize: 10, color: '#94a3b8' }}>{msg.nombreUsuario}</span>
          )}
          <span style={{ fontSize: 10, color: '#94a3b8' }}>
            {new Date(msg.creadoEn).toLocaleTimeString('es-HN', { hour: '2-digit', minute: '2-digit' })}
          </span>
          {esSaliente && (
            <span style={{ fontSize: 10, color: msg.estado === 'leido' ? '#25d366' : '#94a3b8' }}>
              {msg.estado === 'leido' ? '✓✓' : msg.estado === 'entregado' ? '✓✓' : '✓'}
            </span>
          )}
        </div>
      </div>
    </div>
  )
}
