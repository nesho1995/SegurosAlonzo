import { useEffect, useState } from 'react'
import { FileText, Plus, Trash2 } from 'lucide-react'
import {
  getCotizaciones,
  crearCotizacion,
  eliminarCotizacion,
} from '../api/cotizacionApi'
import type { CotizacionResumen } from '../types/cotizacion'
import { StatusPill } from '../components/Badge'
import { ErrorCard } from '../components/ErrorAlert'
import { LoadingCard } from '../components/LoadingState'
import { useAuth } from '../hooks/useAuth'

const ESTADOS = ['TODOS', 'BORRADOR', 'REVISION', 'ENVIADA', 'ACEPTADA', 'RECHAZADA']

const estadoTone = (e: string) => {
  if (e === 'ACEPTADA')  return 'success'
  if (e === 'RECHAZADA') return 'danger'
  if (e === 'ENVIADA')   return 'info'
  if (e === 'REVISION')  return 'warning'
  return 'slate'
}

const L = (v?: number | null) =>
  v != null
    ? `L ${new Intl.NumberFormat('es-HN', { minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(v)}`
    : '—'

const dateFmt = (s?: string | null) =>
  s ? new Date(s).toLocaleDateString('es-HN', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'

interface Props {
  onOpen: (id: number) => void
}

export function CotizacionesView({ onOpen }: Props) {
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('cotizaciones.crear')
  const canDelete = hasPermission('cotizaciones.eliminar')

  const [items, setItems]   = useState<CotizacionResumen[]>([])
  const [total, setTotal]   = useState(0)
  const [pagina, setPagina] = useState(1)
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('TODOS')
  const [loading, setLoading] = useState(true)
  const [error, setError]   = useState<string | null>(null)

  // New form state
  const [showNew, setShowNew]         = useState(false)
  const [newClienteNombre, setNewClienteNombre] = useState('')
  const [newRamo, setNewRamo]         = useState('')
  const [newFecha, setNewFecha]       = useState('')
  const [newNotas, setNewNotas]       = useState('')
  const [saving, setSaving]           = useState(false)

  async function load(p = pagina) {
    setLoading(true)
    setError(null)
    try {
      const data = await getCotizaciones(estado, buscar || undefined, p)
      setItems(data.items)
      setTotal(data.total)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al cargar cotizaciones.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load(1); setPagina(1) }, [estado, buscar])  // eslint-disable-line react-hooks/exhaustive-deps

  async function handleCreate() {
    if (!newClienteNombre.trim() || !newRamo.trim()) return
    setSaving(true)
    try {
      const res = await crearCotizacion({
        clienteNombre: newClienteNombre.trim(),
        ramo: newRamo.trim(),
        fechaInicio: newFecha || null,
        notas: newNotas || null,
      })
      setShowNew(false)
      setNewClienteNombre(''); setNewRamo(''); setNewFecha(''); setNewNotas('')
      if (res?.id) onOpen(res.id)
      else void load(1)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'No se pudo crear la cotización.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(id: number, nombre: string) {
    if (!confirm(`¿Eliminar cotización de "${nombre}"?`)) return
    try {
      await eliminarCotizacion(id)
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al eliminar.')
    }
  }

  const pageSize = 25
  const totalPaginas = Math.max(1, Math.ceil(total / pageSize))

  return (
    <div className="view-wrap">
      <header className="page-header">
        <div>
          <span className="eyebrow">Cartera</span>
          <h1>Cotizaciones</h1>
          <p>{total} cotizaciones registradas</p>
        </div>
        <div className="header-actions">
          {canCreate && (
            <button className="btn-primary" onClick={() => setShowNew(true)}>
              <Plus size={16} /> Nueva cotización
            </button>
          )}
        </div>
      </header>

      {error && <ErrorCard text={error} />}

      {/* New cotización modal-inline */}
      {showNew && (
        <div className="panel" style={{ marginBottom: 16 }}>
          <strong className="panel-title">Nueva cotización</strong>
          <div className="info-grid" style={{ marginTop: 12 }}>
            <div className="field-group">
              <label className="field-label">Cliente / Prospecto *</label>
              <input
                className="field-input"
                value={newClienteNombre}
                onChange={e => setNewClienteNombre(e.target.value)}
                placeholder="Nombre del cliente"
              />
            </div>
            <div className="field-group">
              <label className="field-label">Ramo *</label>
              <input
                className="field-input"
                value={newRamo}
                onChange={e => setNewRamo(e.target.value)}
                placeholder="Ej: Autos, Vida, Hogar..."
              />
            </div>
            <div className="field-group">
              <label className="field-label">Fecha inicio vigencia</label>
              <input type="date" className="field-input" value={newFecha} onChange={e => setNewFecha(e.target.value)} />
            </div>
            <div className="field-group" style={{ gridColumn: '1 / -1' }}>
              <label className="field-label">Notas</label>
              <textarea
                className="field-input"
                rows={2}
                value={newNotas}
                onChange={e => setNewNotas(e.target.value)}
                placeholder="Observaciones o requisitos del cliente..."
              />
            </div>
          </div>
          <div className="form-actions" style={{ marginTop: 12 }}>
            <button className="btn-primary" onClick={handleCreate} disabled={saving || !newClienteNombre.trim() || !newRamo.trim()}>
              {saving ? 'Guardando...' : 'Crear y abrir'}
            </button>
            <button className="btn-secondary" onClick={() => setShowNew(false)}>Cancelar</button>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="filter-bar">
        <input
          className="filter-input"
          placeholder="Buscar cliente, ramo..."
          value={buscar}
          onChange={e => setBuscar(e.target.value)}
        />
        <select className="filter-select" value={estado} onChange={e => setEstado(e.target.value)}>
          {ESTADOS.map(e => (
            <option key={e} value={e}>{e === 'TODOS' ? 'Todos los estados' : e.charAt(0) + e.slice(1).toLowerCase()}</option>
          ))}
        </select>
      </div>

      {loading ? (
        <LoadingCard text="Cargando cotizaciones..." />
      ) : (
        <>
          <div className="panel" style={{ padding: 0 }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Cliente</th>
                  <th>Ramo</th>
                  <th>Opciones</th>
                  <th>Mejor prima</th>
                  <th>Fecha inicio</th>
                  <th>Estado</th>
                  <th>Creado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.length === 0 && (
                  <tr><td colSpan={8} className="empty-row">No hay cotizaciones registradas.</td></tr>
                )}
                {items.map(cot => (
                  <tr key={cot.id} className="table-row-clickable" onClick={() => onOpen(cot.id)}>
                    <td>
                      <span className="cell-primary">{cot.clienteNombre}</span>
                    </td>
                    <td>{cot.ramo}</td>
                    <td>
                      {cot.totalItems > 0
                        ? <span className="badge badge-blue">{cot.totalItems} opciones</span>
                        : <span className="text-muted">Sin opciones</span>}
                    </td>
                    <td className="cell-mono">{L(cot.mejorPrima)}</td>
                    <td>{dateFmt(cot.fechaInicio)}</td>
                    <td>
                      <StatusPill
                        tone={estadoTone(cot.estado)}
                        text={cot.estado.charAt(0) + cot.estado.slice(1).toLowerCase()}
                      />
                    </td>
                    <td className="text-muted text-sm">{dateFmt(cot.fechaCreacion)}</td>
                    <td onClick={e => e.stopPropagation()}>
                      <div style={{ display: 'flex', gap: 4 }}>
                        <button
                          className="icon-button"
                          title="Ver detalle"
                          onClick={() => onOpen(cot.id)}
                        >
                          <FileText size={15} />
                        </button>
                        {canDelete && (
                          <button
                            className="icon-button danger"
                            title="Eliminar"
                            onClick={() => handleDelete(cot.id, cot.clienteNombre)}
                          >
                            <Trash2 size={15} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPaginas > 1 && (
            <div className="pagination">
              <button
                className="btn-secondary"
                disabled={pagina <= 1}
                onClick={() => { setPagina(p => p - 1); void load(pagina - 1) }}
              >
                Anterior
              </button>
              <span className="pagination-info">{pagina} / {totalPaginas}</span>
              <button
                className="btn-secondary"
                disabled={pagina >= totalPaginas}
                onClick={() => { setPagina(p => p + 1); void load(pagina + 1) }}
              >
                Siguiente
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
