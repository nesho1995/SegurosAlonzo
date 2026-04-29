import { useEffect, useState } from 'react'
import { Pencil, Plus, Power, Save, X } from 'lucide-react'
import { createGasto, deleteGasto, getGastos, updateGasto, type Gasto } from '../api/gastosApi'
import { StatusPill } from '../components/Badge'
import { DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, Info, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { useAuth } from '../hooks/useAuth'
import { dateFmt, moneySafe } from '../utils/formatters'
import { statusLabel } from '../utils/labels'

const categorias = [
  'ALIMENTACION_CLIENTE',
  'REGALIAS_CLIENTE',
  'ALIMENTACION_INTERNA',
  'TRANSPORTE',
  'PAPELERIA',
  'SERVICIOS',
  'TECNOLOGIA',
  'PUBLICIDAD_MARKETING',
  'CAPACITACION',
  'OTROS'
]
const categoriaLabel: Record<string, string> = {
  ALIMENTACION_CLIENTE: 'Alimentacion con cliente',
  REGALIAS_CLIENTE: 'Regalias a cliente',
  ALIMENTACION_INTERNA: 'Alimentacion interna',
  TRANSPORTE: 'Transporte',
  PAPELERIA: 'Papeleria',
  SERVICIOS: 'Servicios',
  TECNOLOGIA: 'Tecnologia',
  PUBLICIDAD_MARKETING: 'Publicidad y marketing',
  CAPACITACION: 'Capacitacion',
  OTROS: 'Otros'
}
const emptyGasto = (): Gasto => ({
  id: 0,
  fecha: new Date().toISOString().slice(0, 10),
  categoria: 'ALIMENTACION_CLIENTE',
  descripcion: '',
  monto: 0,
  moneda: 'HNL',
  estado: 'REGISTRADO',
  activo: true,
})

export function GastosView() {
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('gastos.crear')
  const canEdit = hasPermission('gastos.editar')
  const canDelete = hasPermission('gastos.eliminar')
  const [items, setItems] = useState<Gasto[]>([])
  const [summary, setSummary] = useState({ total: 0, totalMes: 0 })
  const [form, setForm] = useState<Gasto>(emptyGasto)
  const [categoriaDraft, setCategoriaDraft] = useState('')
  const [availableCategories, setAvailableCategories] = useState<string[]>(categorias)
  const [filters, setFilters] = useState({
    desde: '',
    hasta: '',
    categoria: '',
  })
  const [editing, setEditing] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState('')

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ pageSize: '80' })
      if (filters.desde) query.set('desde', filters.desde)
      if (filters.hasta) query.set('hasta', filters.hasta)
      if (filters.categoria) query.set('categoria', filters.categoria)
      const data = await getGastos(query)
      setItems(data.items)
      setSummary({ total: data.totalRango, totalMes: data.resumen.totalMes })
      const dynamicCategories = Array.from(new Set(data.items.map((item) => item.categoria).filter(Boolean)))
      setAvailableCategories(Array.from(new Set([...categorias, ...dynamicCategories])))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar gastos.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeout)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.desde, filters.hasta, filters.categoria])

  async function save() {
    if (editing && !canEdit) return
    if (!editing && !canCreate) return
    if (!form.descripcion.trim()) { setError('La descripcion es obligatoria.'); return }
    if (form.monto <= 0) { setError('El monto debe ser mayor que cero.'); return }
    if (!form.categoria.trim()) { setError('La categoria es obligatoria.'); return }
    const normalizedCategory = form.categoria.trim().toUpperCase().replace(/\s+/g, '_')
    const mergedCategories = Array.from(new Set([...availableCategories, normalizedCategory]))
    setAvailableCategories(mergedCategories)
    if (editing) await updateGasto({ ...form, categoria: normalizedCategory })
    else await createGasto({ ...form, categoria: normalizedCategory })
    setMessage(editing ? 'Gasto actualizado.' : 'Gasto registrado.')
    setForm(emptyGasto())
    setEditing(false)
    await load()
  }

  async function remove(gasto: Gasto) {
    if (!canDelete) return
    if (!window.confirm('Confirmas eliminar este gasto?')) return
    await deleteGasto(gasto.id)
    setMessage('Gasto eliminado.')
    await load()
  }

  if (loading) return <LoadingCard text="Cargando gastos..." />

  return (
    <>
      <PageHeader eyebrow="Finanzas" title="Gastos de correduria" description="Controla gastos operativos (comidas con cliente, regalias, transporte y mas) con filtros para analisis." onRefresh={load} action={canCreate ? <button className="primary-button" onClick={() => { setForm(emptyGasto()); setEditing(false) }}><Plus size={18} />Nuevo gasto</button> : undefined} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      <section className="toolbar clients-toolbar">
        <label className="field">
          <span>Desde</span>
          <input type="date" value={filters.desde} onChange={(event) => setFilters({ ...filters, desde: event.target.value })} />
        </label>
        <label className="field">
          <span>Hasta</span>
          <input type="date" value={filters.hasta} onChange={(event) => setFilters({ ...filters, hasta: event.target.value })} />
        </label>
        <label className="field">
          <span>Categoria</span>
          <select value={filters.categoria} onChange={(event) => setFilters({ ...filters, categoria: event.target.value })}>
            <option value="">Todas</option>
            {availableCategories.map((item) => <option key={item} value={item}>{categoriaLabel[item] || item.replaceAll('_', ' ')}</option>)}
          </select>
        </label>
        <button className="primary-button" onClick={() => setFilters({ desde: '', hasta: '', categoria: '' })}>Limpiar filtros</button>
      </section>
      <section className="metric-grid compact">
        <div className="metric-card"><span>Gastos del mes</span><strong>{moneySafe(summary.totalMes)}</strong></div>
        <div className="metric-card"><span>Total filtrado</span><strong>{moneySafe(summary.total)}</strong></div>
        <div className="metric-card"><span>Registros</span><strong>{items.length}</strong></div>
      </section>
      <section className="content-grid">
        {(canCreate || canEdit) && <article className="panel form-panel">
          <PanelTitle title={editing ? 'Editar gasto' : 'Registrar gasto'} subtitle="Registra gastos reales de la correduria para estadistica y control financiero." />
          <div className="form-grid">
            <Field label="Fecha" type="date" value={form.fecha.slice(0, 10)} onChange={(value) => setForm({ ...form, fecha: value })} />
            <label className="field">
              <span>Categoria</span>
              <input
                value={form.categoria}
                list="gastos-categorias"
                onChange={(event) => setForm({ ...form, categoria: event.target.value })}
                placeholder="Ej. ALIMENTACION_CLIENTE"
              />
              <datalist id="gastos-categorias">
                {availableCategories.map((item) => <option key={item} value={item}>{categoriaLabel[item] || item.replaceAll('_', ' ')}</option>)}
              </datalist>
            </label>
            <label className="field">
              <span>Nueva categoria (opcional)</span>
              <div className="inline-add-row">
                <input value={categoriaDraft} onChange={(event) => setCategoriaDraft(event.target.value)} placeholder="Ej. APOYO_COMERCIAL" />
                <button
                  className="icon-button secondary"
                  type="button"
                  onClick={() => {
                    const normalized = categoriaDraft.trim().toUpperCase().replace(/\s+/g, '_')
                    if (!normalized) return
                    setAvailableCategories((current) => Array.from(new Set([...current, normalized])))
                    setForm((current) => ({ ...current, categoria: normalized }))
                    setCategoriaDraft('')
                  }}
                >
                  Agregar
                </button>
              </div>
            </label>
            <Field label="Descripcion" value={form.descripcion} onChange={(value) => setForm({ ...form, descripcion: value })} />
            <Field label="Cliente / proveedor" value={form.proveedor || ''} onChange={(value) => setForm({ ...form, proveedor: value })} />
            <Field label="Monto" type="number" value={String(form.monto || '')} onChange={(value) => setForm({ ...form, monto: Number(value || 0) })} />
            <Field label="Metodo de pago" value={form.metodoPago || ''} onChange={(value) => setForm({ ...form, metodoPago: value })} />
            <Field label="Referencia" value={form.referencia || ''} onChange={(value) => setForm({ ...form, referencia: value })} />
            <Info label="Estado" value="Registrado" />
            <div className="form-actions wide-field"><button className="primary-button" onClick={() => void save()}><Save size={18} />Guardar</button>{editing && <button className="icon-button secondary" onClick={() => { setEditing(false); setForm(emptyGasto()) }}><X size={18} />Cancelar</button>}</div>
          </div>
        </article>}
        <article className="panel">
          <PanelTitle title="Listado de gastos" subtitle="Control historico de gastos operativos, listos para analitica y estadistica." />
          <DataTable headers={['Fecha', 'Categoria', 'Descripcion', 'Cliente/Proveedor', 'Monto', 'Estado', 'Acciones']} rows={items.map((gasto) => [
            dateFmt.format(new Date(gasto.fecha)),
            categoriaLabel[gasto.categoria] || gasto.categoria,
            gasto.descripcion,
            gasto.proveedor || 'N/A',
            moneySafe(gasto.monto),
            <StatusPill text={statusLabel(gasto.estado)} tone="info" />,
            <div className="form-actions">{canEdit && <button className="icon-button secondary" onClick={() => { setForm(gasto); setEditing(true) }}><Pencil size={16} /></button>}{canDelete && <button className="icon-button danger-button" onClick={() => void remove(gasto)}><Power size={16} /></button>}</div>,
          ])} />
        </article>
      </section>
    </>
  )
}
