import { useEffect, useState } from 'react'
import { FileSearch, Plus, Trash2 } from 'lucide-react'
import { crearComparativo, eliminarComparativo, getComparativos } from '../api/comparativoApi'
import { ErrorCard } from '../components/ErrorAlert'
import { PageHeader } from '../components/Topbar'
import { DataTable } from '../components/DataTable'
import { StatusPill } from '../components/Badge'
import type { Comparativo } from '../types/comparativo'

interface Props { onOpen: (id: number) => void }

export function ComparativosView({ onOpen }: Props) {
  const [items, setItems]     = useState<Comparativo[]>([])
  const [total, setTotal]     = useState(0)
  const [page, setPage]       = useState(1)
  const [q, setQ]             = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError]     = useState<string | null>(null)

  // Nuevo
  const [showNew, setShowNew]     = useState(false)
  const [cliente, setCliente]     = useState('')
  const [vehiculo, setVehiculo]   = useState('')
  const [notas, setNotas]         = useState('')
  const [saving, setSaving]       = useState(false)

  const pageSize = 20

  async function load() {
    setLoading(true)
    try {
      const r = await getComparativos(page, pageSize, q || undefined)
      setItems(r.items); setTotal(r.total); setError(null)
    } catch (e) { setError(e instanceof Error ? e.message : 'Error') }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [page, q]) // eslint-disable-line

  async function handleCrear() {
    if (!cliente.trim()) return
    setSaving(true)
    try {
      const { id } = await crearComparativo({ cliente, vehiculo: vehiculo || undefined, notas: notas || undefined })
      onOpen(id)
    } catch (e) { setError(e instanceof Error ? e.message : 'Error') }
    finally { setSaving(false) }
  }

  async function handleEliminar(id: number, e: React.MouseEvent) {
    e.stopPropagation()
    if (!confirm('¿Eliminar este comparativo?')) return
    try { await eliminarComparativo(id); load() }
    catch (err) { setError(err instanceof Error ? err.message : 'Error') }
  }

  const totalPages = Math.ceil(total / pageSize)

  return (
    <>
      <PageHeader
        eyebrow="Cartera"
        title="Comparativos de cotizaciones"
        description="Sube los PDFs de cada aseguradora y el sistema genera la tabla comparativa automáticamente."
        onRefresh={load}
        action={
          <button className="primary-button" onClick={() => setShowNew(v => !v)}>
            <Plus size={16} />Nuevo comparativo
          </button>
        }
      />

      {error && <ErrorCard text={error} />}

      {/* Formulario nuevo */}
      {showNew && (
        <div className="comp-new-form">
          <div className="form-row">
            <div className="field-group">
              <label>Cliente *</label>
              <input className="form-input" placeholder="Nombre del cliente" value={cliente}
                onChange={e => setCliente(e.target.value)} />
            </div>
            <div className="field-group">
              <label>Vehículo</label>
              <input className="form-input" placeholder="Ej. Toyota Hilux 2022" value={vehiculo}
                onChange={e => setVehiculo(e.target.value)} />
            </div>
            <div className="field-group" style={{ flex: 2 }}>
              <label>Notas</label>
              <input className="form-input" placeholder="Observaciones opcionales" value={notas}
                onChange={e => setNotas(e.target.value)} />
            </div>
          </div>
          <div className="form-actions">
            <button className="primary-button" onClick={handleCrear} disabled={saving || !cliente.trim()}>
              {saving ? 'Creando…' : 'Crear y subir PDFs →'}
            </button>
            <button className="secondary-button" onClick={() => setShowNew(false)}>Cancelar</button>
          </div>
        </div>
      )}

      {/* Buscador */}
      <div className="filter-bar">
        <input className="form-input" placeholder="Buscar por cliente o vehículo…"
          value={q} onChange={e => { setQ(e.target.value); setPage(1) }} style={{ maxWidth: 320 }} />
        <span className="muted-text">{total} comparativo{total !== 1 ? 's' : ''}</span>
      </div>

      {/* Tabla */}
      {loading
        ? <div className="empty-state">Cargando…</div>
        : items.length === 0
          ? <div className="empty-state">
              <FileSearch size={32} />
              <p>Sin comparativos. Crea uno nuevo para empezar.</p>
            </div>
          : (
            <DataTable
              headers={['Cliente', 'Vehículo', 'Estado', 'Aseguradoras', 'Fecha', '']}
              rows={items.map(c => [
                <button className="link-button" onClick={() => onOpen(c.id)}>{c.cliente}</button>,
                c.vehiculo || <span className="muted-text">—</span>,
                <StatusPill text={c.estado === 'listo' ? 'Listo' : 'Borrador'}
                  tone={c.estado === 'listo' ? 'success' : 'warning'} />,
                <span className="muted-text">—</span>,
                new Date(c.creadoEn).toLocaleDateString('es-HN'),
                <button className="icon-button danger" onClick={e => handleEliminar(c.id, e)} title="Eliminar">
                  <Trash2 size={14} />
                </button>,
              ])}
            />
          )
      }

      {/* Paginación */}
      {totalPages > 1 && (
        <div className="pagination">
          <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>‹</button>
          <span>Página {page} de {totalPages}</span>
          <button disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>›</button>
        </div>
      )}
    </>
  )
}
