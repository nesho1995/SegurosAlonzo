import { useEffect, useRef, useState } from 'react'
import {
  CheckCheck, ChevronUp, ClipboardList, Link, MessageSquare,
  RefreshCw, Send, Unlink, User, UserCheck,
} from 'lucide-react'
import {
  asignarAgente, asociarReclamo, cambiarEstado,
  buscarReclamos,
  getAgentes, getConversaciones, getMensajes,
  marcarLeido, mediaUrl, responder,
  type AgenteSummary, type ConversacionDetalle,
  type ConversacionListItem, type MensajeDto,
  type ReclamoLinkOption,
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

function estadoColor(e: string) {
  if (e === 'abierta') return '#22c55e'
  if (e === 'en_espera') return '#f59e0b'
  return '#94a3b8'
}

function ultimoPreview(conv: ConversacionListItem) {
  if (!conv.ultimoMensaje && !conv.ultimoTipoContenido) return null
  const tipo = conv.ultimoTipoContenido ?? 'texto'
  if (tipo === 'imagen') return '🖼️ Imagen'
  if (tipo === 'documento') return `📄 ${conv.ultimoMensaje ?? 'Documento'}`
  if (tipo === 'audio') return '🎵 Audio'
  if (tipo === 'video') return '🎥 Video'
  if (tipo === 'sticker') return '😀 Sticker'
  return conv.ultimoMensaje?.slice(0, 60) ?? null
}

function nombreConversacion(conv: {
  nombreCliente?: string | null
  nombreContacto?: string | null
  conductorReclamo?: string | null
  telefono: string
}) {
  return conv.nombreCliente || conv.nombreContacto || conv.conductorReclamo || conv.telefono
}

// ─── Vista principal ──────────────────────────────────────────────────────────

export function WhatsAppBandejaView() {
  const [conversaciones, setConversaciones] = useState<ConversacionListItem[]>([])
  const [total, setTotal] = useState(0)
  const [totalNoLeidos, setTotalNoLeidos] = useState(0)
  const [filtroEstado, setFiltroEstado] = useState('todas')
  const [buscar, setBuscar] = useState('')
  const [cargandoLista, setCargandoLista] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [convSeleccionada, setConvSeleccionada] = useState<ConversacionDetalle | null>(null)
  const [mensajes, setMensajes] = useState<MensajeDto[]>([])
  const [totalMensajes, setTotalMensajes] = useState(0)
  const [offsetMensajes, setOffsetMensajes] = useState(0)
  const [cargandoMensajes, setCargandoMensajes] = useState(false)
  const [cargandoMas, setCargandoMas] = useState(false)
  const [respuesta, setRespuesta] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [msgError, setMsgError] = useState<string | null>(null)

  const [agentes, setAgentes] = useState<AgenteSummary[]>([])
  const [reclamoInput, setReclamoInput] = useState('')
  const [reclamosSugeridos, setReclamosSugeridos] = useState<ReclamoLinkOption[]>([])
  const [buscandoReclamos, setBuscandoReclamos] = useState(false)
  const [vinculandoReclamo, setVinculandoReclamo] = useState(false)
  const [mostrarVincular, setMostrarVincular] = useState(false)

  const chatEndRef = useRef<HTMLDivElement>(null)
  const chatTopRef = useRef<HTMLDivElement>(null)
  const listIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const chatIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const LIMIT = 50

  // ─── Carga lista ────────────────────────────────────────────────────────────

  async function cargarLista(mostrarCarga = true) {
    if (mostrarCarga) setCargandoLista(true)
    setError(null)
    try {
      const { items, total: t, totalNoLeidos: tnl } = await getConversaciones({
        estado: filtroEstado === 'todas' ? undefined : filtroEstado,
        buscar: buscar.trim() || undefined,
        limit: 100,
      })
      setConversaciones(items)
      setTotal(t)
      setTotalNoLeidos(tnl)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error cargando conversaciones.')
    } finally {
      if (mostrarCarga) setCargandoLista(false)
    }
  }

  // ─── Abrir conversación ──────────────────────────────────────────────────────

  async function abrirConversacion(conv: ConversacionListItem) {
    setCargandoMensajes(true)
    setMsgError(null)
    setRespuesta('')
    setMostrarVincular(false)
    setOffsetMensajes(0)
    try {
      const { conversacion, items, total: t } = await getMensajes(conv.id, { limit: LIMIT, offset: 0 })
      setConvSeleccionada(conversacion)
      setMensajes(items)
      setTotalMensajes(t)
      if (conv.noLeidos > 0) {
        await marcarLeido(conv.id)
        setConversaciones(prev => prev.map(c => c.id === conv.id ? { ...c, noLeidos: 0 } : c))
        setTotalNoLeidos(prev => Math.max(0, prev - conv.noLeidos))
      }
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error cargando mensajes.')
    } finally {
      setCargandoMensajes(false)
    }
  }

  // ─── Cargar mensajes anteriores ──────────────────────────────────────────────

  async function cargarMasAnteriores() {
    if (!convSeleccionada) return
    const nuevoOffset = offsetMensajes + LIMIT
    setCargandoMas(true)
    try {
      const { items } = await getMensajes(convSeleccionada.id, { limit: LIMIT, offset: nuevoOffset })
      setMensajes(prev => [...items, ...prev])
      setOffsetMensajes(nuevoOffset)
    } catch { /* silencioso */ } finally {
      setCargandoMas(false)
    }
  }

  // ─── Refresh mensajes ────────────────────────────────────────────────────────

  async function refrescarMensajes() {
    if (!convSeleccionada) return
    try {
      const { items, total: t, conversacion } = await getMensajes(
        convSeleccionada.id, { limit: LIMIT, offset: 0 })
      setMensajes(items)
      setTotalMensajes(t)
      // Actualizar info de la conversación (reclamo/agente pueden haber cambiado)
      setConvSeleccionada(prev => prev ? { ...prev, ...conversacion } : conversacion)
    } catch { /* silencioso */ }
  }

  // ─── Enviar respuesta ────────────────────────────────────────────────────────

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

  // ─── Cambiar estado ──────────────────────────────────────────────────────────

  async function cambiarEstadoConv(estado: string) {
    if (!convSeleccionada) return
    try {
      await cambiarEstado(convSeleccionada.id, estado)
      setConvSeleccionada(prev => prev ? { ...prev, estado } : prev)
      setConversaciones(prev =>
        prev.map(c => c.id === convSeleccionada.id
          ? { ...c, estado: estado as ConversacionListItem['estado'] } : c))
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error cambiando estado.')
    }
  }

  // ─── Asignar agente ──────────────────────────────────────────────────────────

  async function cambiarAgente(agenteId: number | null) {
    if (!convSeleccionada) return
    try {
      await asignarAgente(convSeleccionada.id, agenteId)
      const agente = agentes.find(a => a.id === agenteId)
      setConvSeleccionada(prev => prev
        ? { ...prev, agenteAsignadoId: agenteId, agenteNombre: agente?.username ?? null }
        : prev)
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error asignando agente.')
    }
  }

  // ─── Vincular reclamo ────────────────────────────────────────────────────────

  async function cargarReclamosSugeridos(query = reclamoInput) {
    if (!convSeleccionada) return
    setBuscandoReclamos(true)
    try {
      const items = await buscarReclamos({
        buscar: query.trim() || undefined,
        telefono: convSeleccionada.telefono,
      })
      setReclamosSugeridos(items)
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error buscando reclamos.')
    } finally {
      setBuscandoReclamos(false)
    }
  }

  async function vincularReclamo(reclamoId: number | null) {
    if (!convSeleccionada) return
    setVinculandoReclamo(true)
    setMsgError(null)
    try {
      const { conversacion } = await asociarReclamo(convSeleccionada.id, reclamoId)
      setConvSeleccionada(conversacion)
      setReclamoInput('')
      setReclamosSugeridos([])
      setMostrarVincular(false)
    } catch (e) {
      setMsgError(e instanceof Error ? e.message : 'Error vinculando reclamo.')
    } finally {
      setVinculandoReclamo(false)
    }
  }

  // ─── Scroll al fondo ─────────────────────────────────────────────────────────

  useEffect(() => {
    if (!cargandoMensajes)
      chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [mensajes.length, cargandoMensajes])

  // ─── Cargar filtros ──────────────────────────────────────────────────────────

  useEffect(() => { cargarLista() }, [filtroEstado, buscar])

  // ─── Cargar agentes una vez ──────────────────────────────────────────────────

  useEffect(() => {
    getAgentes().then(setAgentes).catch(() => { })
  }, [])

  // ─── Auto-refresh lista (8s) ─────────────────────────────────────────────────

  useEffect(() => {
    listIntervalRef.current = setInterval(() => cargarLista(false), 8000)
    return () => { if (listIntervalRef.current) clearInterval(listIntervalRef.current) }
  }, [filtroEstado, buscar])

  // ─── Auto-refresh chat activo (3s) ───────────────────────────────────────────

  useEffect(() => {
    if (!convSeleccionada) return
    chatIntervalRef.current = setInterval(refrescarMensajes, 3000)
    return () => { if (chatIntervalRef.current) clearInterval(chatIntervalRef.current) }
  }, [convSeleccionada?.id])

  // ─── Título con badge de no leídos ───────────────────────────────────────────

  useEffect(() => {
    const base = 'Bandeja WhatsApp'
    document.title = totalNoLeidos > 0 ? `(${totalNoLeidos}) ${base}` : base
    return () => { document.title = 'Seguros Alonzo' }
  }, [totalNoLeidos])

  const hayMasAnteriores = totalMensajes > offsetMensajes + LIMIT

  const tabs = [
    { key: 'todas', label: 'Todas' },
    { key: 'abierta', label: 'Abiertas' },
    { key: 'en_espera', label: 'En espera' },
    { key: 'resuelta', label: 'Resueltas' },
  ]

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>

      {/* ── Header ── */}
      <div style={{ padding: '14px 20px 0', borderBottom: '1px solid #e2e8f0' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
          <MessageSquare size={20} style={{ color: '#25d366' }} />
          <h1 style={{ margin: 0, fontSize: 18, fontWeight: 700 }}>Bandeja WhatsApp</h1>
          {totalNoLeidos > 0 && (
            <span style={{
              background: '#25d366', color: '#fff', borderRadius: 10,
              fontSize: 11, fontWeight: 700, padding: '1px 7px',
            }}>{totalNoLeidos}</span>
          )}
          <span style={{ marginLeft: 'auto', fontSize: 12, color: '#94a3b8' }}>
            {total} conversaciones
          </span>
          <button onClick={() => cargarLista()} title="Actualizar"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8', padding: 4 }}>
            <RefreshCw size={15} />
          </button>
        </div>
        <div style={{ display: 'flex', gap: 2 }}>
          {tabs.map(tab => (
            <button key={tab.key} onClick={() => setFiltroEstado(tab.key)} style={{
              padding: '5px 12px', border: 'none', borderRadius: '5px 5px 0 0', cursor: 'pointer',
              fontSize: 12, fontWeight: filtroEstado === tab.key ? 600 : 400,
              background: filtroEstado === tab.key ? '#fff' : 'transparent',
              color: filtroEstado === tab.key ? '#1e293b' : '#64748b',
              borderBottom: filtroEstado === tab.key ? '2px solid #25d366' : '2px solid transparent',
            }}>{tab.label}</button>
          ))}
        </div>
      </div>

      {/* ── Dos paneles ── */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>

        {/* ── Panel izquierdo ── */}
        <div style={{
          width: 310, minWidth: 240, borderRight: '1px solid #e2e8f0',
          display: 'flex', flexDirection: 'column', overflow: 'hidden',
        }}>
          <div style={{ padding: '8px 10px', borderBottom: '1px solid #f1f5f9' }}>
            <input type="text" placeholder="Buscar nombre o teléfono..."
              value={buscar} onChange={e => setBuscar(e.target.value)}
              style={{
                width: '100%', boxSizing: 'border-box', padding: '6px 10px',
                border: '1px solid #e2e8f0', borderRadius: 6, fontSize: 12, outline: 'none',
              }} />
          </div>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {error && <div style={{ padding: 12, color: '#dc2626', fontSize: 12 }}>{error}</div>}
            {cargandoLista && !conversaciones.length && (
              <div style={{ padding: 24, textAlign: 'center', color: '#94a3b8', fontSize: 12 }}>Cargando...</div>
            )}
            {!cargandoLista && !conversaciones.length && (
              <div style={{ padding: 24, textAlign: 'center', color: '#94a3b8', fontSize: 12 }}>Sin conversaciones</div>
            )}
            {conversaciones.map(conv => (
              <ConvItem key={conv.id} conv={conv}
                selected={convSeleccionada?.id === conv.id}
                onClick={() => abrirConversacion(conv)} />
            ))}
          </div>
        </div>

        {/* ── Panel derecho ── */}
        {!convSeleccionada ? (
          <div style={{
            flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#94a3b8', fontSize: 14, flexDirection: 'column', gap: 10,
          }}>
            <MessageSquare size={44} style={{ opacity: 0.2 }} />
            <span>Selecciona una conversación</span>
          </div>
        ) : (
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>

            {/* ── Header del chat ── */}
            <div style={{
              padding: '10px 14px', borderBottom: '1px solid #e2e8f0',
              background: '#fafafa', display: 'flex', flexDirection: 'column', gap: 6,
            }}>
              {/* Fila 1: avatar + nombre + teléfono + estado */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <div style={{
                  width: 36, height: 36, borderRadius: '50%', background: '#e2e8f0', flexShrink: 0,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                }}>
                  <User size={16} style={{ color: '#64748b' }} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600, fontSize: 14, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {nombreConversacion(convSeleccionada)}
                  </div>
                  <div style={{ fontSize: 11, color: '#64748b' }}>{convSeleccionada.telefono}</div>
                </div>

                {/* Selector estado */}
                <select value={convSeleccionada.estado} onChange={e => cambiarEstadoConv(e.target.value)}
                  style={{
                    border: '1px solid #e2e8f0', borderRadius: 5, padding: '3px 7px',
                    fontSize: 11, cursor: 'pointer', background: '#fff',
                  }}>
                  <option value="abierta">🟢 Abierta</option>
                  <option value="en_espera">🟡 En espera</option>
                  <option value="resuelta">⚫ Resuelta</option>
                </select>
              </div>

              {/* Fila 2: cliente, reclamo, agente */}
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center' }}>

                {/* Cliente vinculado */}
                {convSeleccionada.nombreCliente && (
                  <span style={{
                    fontSize: 11, background: '#eff6ff', color: '#3b82f6',
                    borderRadius: 4, padding: '2px 7px', display: 'flex', alignItems: 'center', gap: 3,
                  }}>
                    <User size={10} /> {convSeleccionada.nombreCliente}
                  </span>
                )}

                {/* Reclamo vinculado */}
                {convSeleccionada.reclamoId ? (
                  <span style={{
                    fontSize: 11, background: '#f0fdf4', color: '#16a34a',
                    borderRadius: 4, padding: '2px 7px', display: 'flex', alignItems: 'center', gap: 3,
                    cursor: 'pointer',
                  }}
                    onClick={() => window.history.pushState(null, '', '/reclamos')}
                    title="Ver reclamo">
                    <ClipboardList size={10} />
                    {convSeleccionada.numeroReclamo ?? `Reclamo #${convSeleccionada.reclamoId}`}
                    {convSeleccionada.conductorReclamo && ` · ${convSeleccionada.conductorReclamo}`}
                    <button onClick={e => { e.stopPropagation(); vincularReclamo(null) }}
                      style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, marginLeft: 2 }}
                      title="Desvincular reclamo">
                      <Unlink size={9} style={{ color: '#94a3b8' }} />
                    </button>
                  </span>
                ) : (
                  <button onClick={() => {
                    const next = !mostrarVincular
                    setMostrarVincular(next)
                    if (next) void cargarReclamosSugeridos('')
                  }}
                    style={{
                      fontSize: 11, background: '#f8fafc', color: '#64748b',
                      border: '1px dashed #cbd5e1', borderRadius: 4, padding: '2px 7px',
                      cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 3,
                    }}>
                    <Link size={10} /> Vincular reclamo
                  </button>
                )}

                {/* Agente asignado */}
                <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                  <UserCheck size={11} style={{ color: '#94a3b8' }} />
                  <select
                    value={convSeleccionada.agenteAsignadoId ?? ''}
                    onChange={e => cambiarAgente(e.target.value ? parseInt(e.target.value) : null)}
                    style={{
                      border: 'none', background: 'transparent', fontSize: 11,
                      color: convSeleccionada.agenteNombre ? '#374151' : '#94a3b8',
                      cursor: 'pointer', outline: 'none',
                    }}>
                    <option value="">Sin asignar</option>
                    {agentes.map(a => <option key={a.id} value={a.id}>{a.username}</option>)}
                  </select>
                </span>
              </div>

              {/* Vincular reclamo: input inline */}
              {mostrarVincular && (
                <div style={{ marginTop: 2, display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input type="text" placeholder="Buscar reclamo, cliente, poliza o placa..." value={reclamoInput}
                      onChange={e => {
                        setReclamoInput(e.target.value)
                        void cargarReclamosSugeridos(e.target.value)
                      }}
                      style={{
                        flex: 1, padding: '5px 8px', border: '1px solid #e2e8f0',
                        borderRadius: 5, fontSize: 12, outline: 'none',
                      }} />
                    <button onClick={() => cargarReclamosSugeridos()} disabled={buscandoReclamos}
                      style={{ padding: '5px 10px', background: '#f1f5f9', border: 'none', borderRadius: 5, fontSize: 12, cursor: 'pointer' }}>
                      {buscandoReclamos ? 'Buscando...' : 'Buscar'}
                    </button>
                    <button onClick={() => setMostrarVincular(false)}
                      style={{ padding: '5px 8px', background: '#f1f5f9', border: 'none', borderRadius: 5, fontSize: 12, cursor: 'pointer' }}>
                      Cerrar
                    </button>
                  </div>
                  <div style={{ maxHeight: 190, overflowY: 'auto', display: 'grid', gap: 4 }}>
                    {reclamosSugeridos.map(item => (
                      <button key={item.id} onClick={() => vincularReclamo(item.id)} disabled={vinculandoReclamo}
                        style={{
                          textAlign: 'left', border: item.telefonoCoincide ? '1px solid #25d366' : '1px solid #e2e8f0',
                          background: item.telefonoCoincide ? '#f0fdf4' : '#fff', borderRadius: 6,
                          padding: '6px 8px', cursor: 'pointer', fontSize: 12,
                        }}>
                        <strong>{item.referencia}</strong> · {item.cliente}
                        <div style={{ color: '#64748b', marginTop: 2 }}>
                          {item.poliza && `Poliza ${item.poliza} · `}
                          {item.placa && `Placa ${item.placa} · `}
                          {item.estado}
                          {item.telefonoCoincide && ' · telefono coincide'}
                        </div>
                      </button>
                    ))}
                    {!buscandoReclamos && reclamosSugeridos.length === 0 && (
                      <span style={{ color: '#94a3b8', fontSize: 12 }}>No hay reclamos encontrados.</span>
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* ── Mensajes ── */}
            <div style={{ flex: 1, overflowY: 'auto', padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div ref={chatTopRef} />

              {/* Botón cargar anteriores */}
              {hayMasAnteriores && (
                <div style={{ textAlign: 'center', marginBottom: 4 }}>
                  <button onClick={cargarMasAnteriores} disabled={cargandoMas}
                    style={{
                      background: '#f1f5f9', border: '1px solid #e2e8f0', borderRadius: 6,
                      padding: '5px 14px', fontSize: 12, cursor: 'pointer', color: '#475569',
                      display: 'inline-flex', alignItems: 'center', gap: 5,
                    }}>
                    <ChevronUp size={13} />
                    {cargandoMas ? 'Cargando...' : 'Cargar mensajes anteriores'}
                  </button>
                </div>
              )}

              {cargandoMensajes && (
                <div style={{ textAlign: 'center', color: '#94a3b8', fontSize: 12, padding: 20 }}>Cargando mensajes...</div>
              )}
              {msgError && (
                <div style={{ color: '#dc2626', fontSize: 12, padding: 8, background: '#fef2f2', borderRadius: 6 }}>{msgError}</div>
              )}
              {!cargandoMensajes && mensajes.length === 0 && (
                <div style={{ textAlign: 'center', color: '#94a3b8', fontSize: 12, padding: 20 }}>Sin mensajes aún</div>
              )}
              {mensajes.map(msg => <BurbujaMensaje key={msg.id} msg={msg} />)}
              <div ref={chatEndRef} />
            </div>

            {/* ── Input de respuesta ── */}
            <div style={{
              borderTop: '1px solid #e2e8f0', padding: '8px 12px',
              display: 'flex', gap: 8, alignItems: 'flex-end', background: '#fafafa',
            }}>
              <textarea value={respuesta} onChange={e => setRespuesta(e.target.value)}
                placeholder="Escribe un mensaje... (Enter para enviar, Shift+Enter para salto de línea)"
                rows={2}
                onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); enviarRespuesta() } }}
                style={{
                  flex: 1, resize: 'none', border: '1px solid #e2e8f0', borderRadius: 8,
                  padding: '7px 10px', fontSize: 13, outline: 'none', fontFamily: 'inherit',
                }} />
              <button onClick={enviarRespuesta} disabled={enviando || !respuesta.trim()}
                style={{
                  background: enviando || !respuesta.trim() ? '#e2e8f0' : '#25d366',
                  color: enviando || !respuesta.trim() ? '#94a3b8' : '#fff',
                  border: 'none', borderRadius: 8, padding: '9px 14px', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', gap: 5, fontSize: 13, fontWeight: 600,
                  flexShrink: 0,
                }}>
                <Send size={14} />
                {enviando ? 'Enviando...' : 'Enviar'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Item de conversación ─────────────────────────────────────────────────────

function ConvItem({ conv, selected, onClick }: {
  conv: ConversacionListItem; selected: boolean; onClick: () => void
}) {
  const preview = ultimoPreview(conv)
  return (
    <button onClick={onClick} style={{
      width: '100%', textAlign: 'left', border: 'none', padding: '9px 10px',
      cursor: 'pointer', borderBottom: '1px solid #f8fafc',
      background: selected ? '#f0fdf4' : 'transparent',
      display: 'flex', gap: 8, alignItems: 'flex-start',
    }}>
      <div style={{
        width: 38, height: 38, borderRadius: '50%', flexShrink: 0,
        background: selected ? '#dcfce7' : '#f1f5f9',
        display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative',
      }}>
        <User size={16} style={{ color: '#64748b' }} />
        {conv.noLeidos > 0 && (
          <span style={{
            position: 'absolute', top: -2, right: -2,
            background: '#25d366', color: '#fff', borderRadius: '50%',
            fontSize: 9, fontWeight: 700, minWidth: 16, height: 16,
            display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '0 3px',
          }}>
            {conv.noLeidos > 99 ? '99+' : conv.noLeidos}
          </span>
        )}
      </div>
      <div style={{ flex: 1, overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 4 }}>
          <span style={{
            fontWeight: conv.noLeidos > 0 ? 700 : 500, fontSize: 13,
            whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', flex: 1,
          }}>
            {nombreConversacion(conv)}
          </span>
          <span style={{ fontSize: 10, color: '#94a3b8', flexShrink: 0 }}>
            {timeFmt(conv.ultimaActividad)}
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 3, marginTop: 1 }}>
          {conv.ultimoDireccion === 'saliente' && (
            <CheckCheck size={11} style={{ color: '#64748b', flexShrink: 0 }} />
          )}
          <span style={{
            fontSize: 11, color: conv.noLeidos > 0 ? '#374151' : '#94a3b8',
            fontWeight: conv.noLeidos > 0 ? 600 : 400,
            whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          }}>
            {preview ?? <i>Sin mensajes</i>}
          </span>
        </div>
        <div style={{ display: 'flex', gap: 4, marginTop: 3, flexWrap: 'wrap' }}>
          <span style={{
            fontSize: 10, padding: '1px 5px', borderRadius: 3,
            background: estadoColor(conv.estado) + '1a', color: estadoColor(conv.estado), fontWeight: 600,
          }}>
            {conv.estado === 'abierta' ? 'Abierta' : conv.estado === 'en_espera' ? 'En espera' : 'Resuelta'}
          </span>
          {conv.numeroReclamo && (
            <span style={{ fontSize: 10, color: '#64748b', background: '#f1f5f9', padding: '1px 5px', borderRadius: 3 }}>
              {conv.numeroReclamo}
            </span>
          )}
          {conv.agenteNombre && (
            <span style={{ fontSize: 10, color: '#7c3aed', background: '#f5f3ff', padding: '1px 5px', borderRadius: 3 }}>
              {conv.agenteNombre}
            </span>
          )}
        </div>
      </div>
    </button>
  )
}

// ─── Burbuja de mensaje ───────────────────────────────────────────────────────

function BurbujaMensaje({ msg }: { msg: MensajeDto }) {
  const esSaliente = msg.direccion === 'saliente'
  const tipo = msg.tipoContenido

  return (
    <div style={{ display: 'flex', justifyContent: esSaliente ? 'flex-end' : 'flex-start' }}>
      <div style={{
        maxWidth: '72%',
        background: esSaliente ? '#dcf8c6' : '#fff',
        border: '1px solid #e2e8f0',
        borderRadius: esSaliente ? '12px 2px 12px 12px' : '2px 12px 12px 12px',
        padding: '7px 10px',
        boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
        minWidth: 80,
      }}>
        {/* Imagen */}
        {tipo === 'imagen' && msg.mediaId && (
          <div style={{ marginBottom: msg.contenido ? 6 : 0 }}>
            <img
              src={mediaUrl(msg.mediaId)}
              alt={msg.mediaNombre ?? 'Imagen'}
              style={{ maxWidth: '100%', maxHeight: 280, borderRadius: 8, display: 'block', cursor: 'pointer' }}
              onClick={() => window.open(mediaUrl(msg.mediaId!), '_blank')}
              onError={e => {
                (e.target as HTMLImageElement).style.display = 'none'
                const p = (e.target as HTMLImageElement).parentElement
                if (p) p.innerHTML = '<span style="color:#94a3b8;font-size:12px">🖼️ No se pudo cargar la imagen</span>'
              }}
            />
          </div>
        )}

        {/* Video */}
        {tipo === 'video' && msg.mediaId && (
          <div style={{ marginBottom: msg.contenido ? 6 : 0 }}>
            <video controls src={mediaUrl(msg.mediaId)}
              style={{ maxWidth: '100%', maxHeight: 240, borderRadius: 8, display: 'block' }} />
          </div>
        )}

        {/* Audio */}
        {tipo === 'audio' && msg.mediaId && (
          <div style={{ marginBottom: msg.contenido ? 4 : 0 }}>
            <audio controls src={mediaUrl(msg.mediaId)} style={{ width: '100%', maxWidth: 300 }} />
          </div>
        )}

        {/* Documento */}
        {tipo === 'document' || (tipo === 'documento' && msg.mediaId) ? (
          <a href={mediaUrl(msg.mediaId!, true)} target="_blank" rel="noopener noreferrer"
            style={{
              display: 'flex', alignItems: 'center', gap: 6,
              color: '#2563eb', fontSize: 12, textDecoration: 'none',
              background: '#eff6ff', borderRadius: 6, padding: '6px 10px',
              marginBottom: msg.contenido ? 6 : 0,
            }}>
            <span style={{ fontSize: 18 }}>📄</span>
            <span style={{ wordBreak: 'break-word' }}>{msg.mediaNombre ?? 'Descargar documento'}</span>
          </a>
        ) : null}

        {/* Sticker */}
        {tipo === 'sticker' && msg.mediaId && (
          <img src={mediaUrl(msg.mediaId)} alt="Sticker"
            style={{ width: 100, height: 100, objectFit: 'contain' }}
            onError={e => { (e.target as HTMLImageElement).replaceWith(Object.assign(document.createTextNode('😀'), {})) }}
          />
        )}

        {/* Texto / caption */}
        {msg.contenido && (
          <div style={{ fontSize: 13, lineHeight: 1.5, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
            {msg.contenido}
          </div>
        )}

        {/* Tipo desconocido sin contenido */}
        {!msg.contenido && !msg.mediaId && tipo !== 'texto' && (
          <span style={{ fontSize: 12, color: '#94a3b8', fontStyle: 'italic' }}>
            [{tipo}]
          </span>
        )}

        {/* Pie: hora + agente + estado */}
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
            <span style={{
              fontSize: 11,
              color: msg.estado === 'leido' ? '#25d366' : msg.estado === 'entregado' ? '#94a3b8' : '#c4c4c4'
            }}>
              {msg.estado === 'leido' ? '✓✓' : msg.estado === 'entregado' ? '✓✓' : '✓'}
            </span>
          )}
        </div>
      </div>
    </div>
  )
}
