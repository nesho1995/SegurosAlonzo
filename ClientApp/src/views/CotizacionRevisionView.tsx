import { useEffect, useState, type ReactNode } from 'react'
import {
  ArrowLeft, Check, ChevronDown, ChevronUp,
  FileSpreadsheet, FileDown, Pencil, Plus, RefreshCw, Save, Star, Trash2, X,
} from 'lucide-react'
import {
  getCotizacionDetalle,
  actualizarCotizacion,
  agregarItem,
  actualizarItem,
  eliminarItem,
  guardarCoberturas,
  guardarExclusiones,
  recalcularRanking,
  guardarAnalisis,
  urlExcelCotizacion,
  urlPdfCotizacion,
} from '../api/cotizacionApi'
import type {
  CotizacionDetalle,
  CotizacionItem,
  CotizacionCobertura,
  CotizacionExclusion,
} from '../types/cotizacion'
import { StatusPill } from '../components/Badge'
import { ErrorCard } from '../components/ErrorAlert'
import { LoadingCard } from '../components/LoadingState'
import { useAuth } from '../hooks/useAuth'
import type { StatusTone } from '../types/common'

const ESTADOS_COT = ['BORRADOR', 'REVISION', 'ENVIADA', 'ACEPTADA', 'RECHAZADA']
const FRECUENCIAS  = ['MENSUAL', 'TRIMESTRAL', 'SEMESTRAL', 'ANUAL']

const estadoTone = (e: string): StatusTone => {
  if (e === 'ACEPTADA')  return 'success'
  if (e === 'RECHAZADA') return 'danger'
  if (e === 'ENVIADA')   return 'info'
  if (e === 'REVISION')  return 'warning'
  return 'slate'
}

const L = (v?: number | null) =>
  v != null
    ? `L ${new Intl.NumberFormat('es-HN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v)}`
    : '—'

const emptyItem = (): Partial<CotizacionItem> => ({
  aseguradora: '',
  plan: '',
  primaAnual: undefined,
  primaMensual: undefined,
  frecuenciaPago: 'MENSUAL',
  sumaAsegurada: undefined,
  deducible: undefined,
  vigenciaMeses: undefined,
  notas: '',
})

interface Props {
  cotizacionId: number
  onBack: () => void
}

export function CotizacionRevisionView({ cotizacionId, onBack }: Props) {
  const { hasPermission } = useAuth()
  const canEdit   = hasPermission('cotizaciones.editar')

  const [detalle, setDetalle]     = useState<CotizacionDetalle | null>(null)
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState<string | null>(null)
  const [saving, setSaving]       = useState(false)

  // Tab: 'comparativa' | 'analisis'
  const [tab, setTab] = useState<'comparativa' | 'analisis'>('comparativa')

  // Edit cotización header
  const [editingHeader, setEditingHeader] = useState(false)
  const [headerForm, setHeaderForm] = useState({ estado: '', notas: '', ramo: '', clienteNombre: '', fechaInicio: '' })

  // Add / edit item
  const [addingItem, setAddingItem]   = useState(false)
  const [editingItemId, setEditingItemId] = useState<number | null>(null)
  const [itemForm, setItemForm]       = useState<Partial<CotizacionItem>>(emptyItem())

  // Expanded item (coberturas / exclusiones panel)
  const [expandedItemId, setExpandedItemId] = useState<number | null>(null)

  // Coberturas draft (per expanded item)
  const [cobDraft, setCobDraft]   = useState<Partial<CotizacionCobertura>[]>([])
  const [excDraft, setExcDraft]   = useState<Partial<CotizacionExclusion>[]>([])

  // Análisis draft
  const [analisisDraft, setAnalisisDraft] = useState({ id: 0, analisisTexto: '', recomendacion: '' })

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const data = await getCotizacionDetalle(cotizacionId)
      setDetalle(data)
      setHeaderForm({
        estado:         data.cotizacion.estado,
        notas:          data.cotizacion.notas ?? '',
        ramo:           data.cotizacion.ramo,
        clienteNombre:  data.cotizacion.clienteNombre,
        fechaInicio:    data.cotizacion.fechaInicio ?? '',
      })
      if (data.analisis) {
        setAnalisisDraft({
          id:             data.analisis.id,
          analisisTexto:  data.analisis.analisisTexto ?? '',
          recomendacion:  data.analisis.recomendacion ?? '',
        })
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al cargar cotización.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [cotizacionId]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Header save ────────────────────────────────────────────────────────────

  async function saveHeader() {
    if (!detalle) return
    setSaving(true)
    try {
      await actualizarCotizacion(cotizacionId, {
        estado:        headerForm.estado,
        notas:         headerForm.notas || null,
        ramo:          headerForm.ramo,
        clienteNombre: headerForm.clienteNombre,
        fechaInicio:   headerForm.fechaInicio || null,
        clienteId:     detalle.cotizacion.clienteId,
      })
      setEditingHeader(false)
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al guardar.')
    } finally {
      setSaving(false)
    }
  }

  // ── Item CRUD ──────────────────────────────────────────────────────────────

  function openAddItem() {
    setItemForm(emptyItem())
    setEditingItemId(null)
    setAddingItem(true)
    setExpandedItemId(null)
  }

  function openEditItem(item: CotizacionItem) {
    setItemForm({ ...item })
    setEditingItemId(item.id)
    setAddingItem(false)
  }

  async function saveItem() {
    if (!itemForm.aseguradora?.trim()) return
    setSaving(true)
    try {
      if (editingItemId !== null) {
        await actualizarItem(cotizacionId, editingItemId, itemForm)
      } else {
        await agregarItem(cotizacionId, itemForm)
      }
      setAddingItem(false)
      setEditingItemId(null)
      setItemForm(emptyItem())
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al guardar opción.')
    } finally {
      setSaving(false)
    }
  }

  async function deleteItemAction(itemId: number) {
    if (!confirm('¿Eliminar esta opción de la cotización?')) return
    setSaving(true)
    try {
      await eliminarItem(cotizacionId, itemId)
      if (expandedItemId === itemId) setExpandedItemId(null)
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al eliminar opción.')
    } finally {
      setSaving(false)
    }
  }

  // ── Expand item → coberturas / exclusiones ────────────────────────────────

  function toggleExpand(item: CotizacionItem) {
    if (expandedItemId === item.id) {
      setExpandedItemId(null)
      return
    }
    setExpandedItemId(item.id)
    setCobDraft(item.coberturas.map(c => ({ ...c })))
    setExcDraft(item.exclusiones.map(e => ({ ...e })))
  }

  async function saveCoberturas(itemId: number) {
    setSaving(true)
    try {
      await guardarCoberturas(cotizacionId, itemId, cobDraft.filter(c => c.nombre?.trim()))
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al guardar coberturas.')
    } finally {
      setSaving(false)
    }
  }

  async function saveExclusiones(itemId: number) {
    setSaving(true)
    try {
      await guardarExclusiones(cotizacionId, itemId, excDraft.filter(e => e.descripcion?.trim()))
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al guardar exclusiones.')
    } finally {
      setSaving(false)
    }
  }

  // ── Ranking ────────────────────────────────────────────────────────────────

  async function handleRecalcular() {
    setSaving(true)
    try {
      await recalcularRanking(cotizacionId)
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al recalcular ranking.')
    } finally {
      setSaving(false)
    }
  }

  // ── Análisis ───────────────────────────────────────────────────────────────

  async function saveAnalisis() {
    setSaving(true)
    try {
      await guardarAnalisis(cotizacionId, analisisDraft)
      void load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Error al guardar análisis.')
    } finally {
      setSaving(false)
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  if (loading) return <LoadingCard text="Cargando cotización..." />
  if (!detalle) return <ErrorCard text={error ?? 'Cotización no encontrada.'} />

  const cot   = detalle.cotizacion
  const items = detalle.items

  return (
    <div className="view-wrap">
      {/* Header */}
      <div className="page-header" style={{ marginBottom: 16 }}>
        <button className="btn-secondary icon-btn" onClick={onBack} title="Volver">
          <ArrowLeft size={16} />
        </button>
        <div style={{ flex: 1 }}>
          <h1 className="page-title" style={{ marginBottom: 2 }}>
            {cot.clienteNombre}
            {items.some(i => i.recomendado) && <Star size={14} style={{ marginLeft: 6, color: '#f59e0b' }} />}
          </h1>
          <span className="page-subtitle">{cot.ramo}</span>
        </div>
        <StatusPill tone={estadoTone(cot.estado)} text={cot.estado.charAt(0) + cot.estado.slice(1).toLowerCase()} />
        <div style={{ display: 'flex', gap: 6 }}>
          <a href={urlExcelCotizacion(cotizacionId)} target="_blank" rel="noreferrer" className="btn-secondary" title="Exportar Excel">
            <FileSpreadsheet size={15} /> Excel
          </a>
          <a href={urlPdfCotizacion(cotizacionId)} target="_blank" rel="noreferrer" className="btn-secondary" title="Exportar PDF">
            <FileDown size={15} /> PDF
          </a>
          {canEdit && !editingHeader && (
            <button className="btn-secondary" onClick={() => setEditingHeader(true)}>
              <Pencil size={15} /> Editar
            </button>
          )}
        </div>
      </div>

      {error && <ErrorCard text={error} />}

      {/* Edit header form */}
      {editingHeader && canEdit && (
        <div className="panel" style={{ marginBottom: 16 }}>
          <strong>Editar cotización</strong>
          <div className="info-grid" style={{ marginTop: 10 }}>
            <div className="field-group">
              <label className="field-label">Cliente</label>
              <input className="field-input" value={headerForm.clienteNombre}
                onChange={e => setHeaderForm(f => ({ ...f, clienteNombre: e.target.value }))} />
            </div>
            <div className="field-group">
              <label className="field-label">Ramo</label>
              <input className="field-input" value={headerForm.ramo}
                onChange={e => setHeaderForm(f => ({ ...f, ramo: e.target.value }))} />
            </div>
            <div className="field-group">
              <label className="field-label">Fecha inicio</label>
              <input type="date" className="field-input" value={headerForm.fechaInicio}
                onChange={e => setHeaderForm(f => ({ ...f, fechaInicio: e.target.value }))} />
            </div>
            <div className="field-group">
              <label className="field-label">Estado</label>
              <select className="field-input" value={headerForm.estado}
                onChange={e => setHeaderForm(f => ({ ...f, estado: e.target.value }))}>
                {ESTADOS_COT.map(s => <option key={s} value={s}>{s.charAt(0) + s.slice(1).toLowerCase()}</option>)}
              </select>
            </div>
            <div className="field-group" style={{ gridColumn: '1 / -1' }}>
              <label className="field-label">Notas</label>
              <textarea className="field-input" rows={2} value={headerForm.notas}
                onChange={e => setHeaderForm(f => ({ ...f, notas: e.target.value }))} />
            </div>
          </div>
          <div className="form-actions" style={{ marginTop: 10 }}>
            <button className="btn-primary" onClick={saveHeader} disabled={saving}><Save size={14} /> Guardar</button>
            <button className="btn-secondary" onClick={() => setEditingHeader(false)}><X size={14} /> Cancelar</button>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="tab-bar" style={{ marginBottom: 16 }}>
        <button className={`tab-btn ${tab === 'comparativa' ? 'active' : ''}`} onClick={() => setTab('comparativa')}>
          Tabla comparativa
        </button>
        <button className={`tab-btn ${tab === 'analisis' ? 'active' : ''}`} onClick={() => setTab('analisis')}>
          Análisis y recomendación
        </button>
      </div>

      {/* ── TAB: Comparativa ──────────────────────────────────────────────── */}
      {tab === 'comparativa' && (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <span className="text-muted text-sm">{items.length} opciones</span>
            <div style={{ display: 'flex', gap: 8 }}>
              {canEdit && (
                <>
                  <button className="btn-secondary" onClick={handleRecalcular} disabled={saving} title="Recalcular ranking">
                    <RefreshCw size={14} /> Ranking
                  </button>
                  <button className="btn-primary" onClick={openAddItem}>
                    <Plus size={14} /> Agregar opción
                  </button>
                </>
              )}
            </div>
          </div>

          {/* Add / edit item form */}
          {(addingItem || editingItemId !== null) && (
            <div className="panel" style={{ marginBottom: 16 }}>
              <strong>{editingItemId !== null ? 'Editar opción' : 'Nueva opción'}</strong>
              <div className="info-grid compact" style={{ marginTop: 10 }}>
                <div className="field-group">
                  <label className="field-label">Aseguradora *</label>
                  <input className="field-input" value={itemForm.aseguradora ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, aseguradora: e.target.value }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Plan / Producto</label>
                  <input className="field-input" value={itemForm.plan ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, plan: e.target.value }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Prima anual (L)</label>
                  <input type="number" className="field-input" value={itemForm.primaAnual ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, primaAnual: e.target.value ? Number(e.target.value) : undefined }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Prima mensual (L)</label>
                  <input type="number" className="field-input" value={itemForm.primaMensual ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, primaMensual: e.target.value ? Number(e.target.value) : undefined }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Frecuencia de pago</label>
                  <select className="field-input" value={itemForm.frecuenciaPago ?? 'MENSUAL'}
                    onChange={e => setItemForm(f => ({ ...f, frecuenciaPago: e.target.value }))}>
                    {FRECUENCIAS.map(f => <option key={f} value={f}>{f.charAt(0) + f.slice(1).toLowerCase()}</option>)}
                  </select>
                </div>
                <div className="field-group">
                  <label className="field-label">Suma asegurada (L)</label>
                  <input type="number" className="field-input" value={itemForm.sumaAsegurada ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, sumaAsegurada: e.target.value ? Number(e.target.value) : undefined }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Deducible (L)</label>
                  <input type="number" className="field-input" value={itemForm.deducible ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, deducible: e.target.value ? Number(e.target.value) : undefined }))} />
                </div>
                <div className="field-group">
                  <label className="field-label">Vigencia (meses)</label>
                  <input type="number" className="field-input" value={itemForm.vigenciaMeses ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, vigenciaMeses: e.target.value ? Number(e.target.value) : undefined }))} />
                </div>
                <div className="field-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="field-label">Notas</label>
                  <textarea className="field-input" rows={2} value={itemForm.notas ?? ''}
                    onChange={e => setItemForm(f => ({ ...f, notas: e.target.value }))} />
                </div>
              </div>
              <div className="form-actions" style={{ marginTop: 10 }}>
                <button className="btn-primary" onClick={saveItem} disabled={saving || !itemForm.aseguradora?.trim()}>
                  <Save size={14} /> {saving ? 'Guardando...' : 'Guardar'}
                </button>
                <button className="btn-secondary" onClick={() => { setAddingItem(false); setEditingItemId(null) }}>
                  <X size={14} /> Cancelar
                </button>
              </div>
            </div>
          )}

          {/* Comparison table */}
          {items.length === 0 ? (
            <div className="panel empty-state">
              <p>No hay opciones. Agrega al menos dos opciones para comparar.</p>
            </div>
          ) : (
            <div className="cotizacion-compare-wrap">
              <table className="cotizacion-table">
                <thead>
                  <tr>
                    <th className="cotizacion-field-col">Campo</th>
                    {items.map(item => (
                      <th key={item.id} className={`cotizacion-item-col ${item.recomendado ? 'recomendado' : ''}`}>
                        <div className="cotizacion-th-inner">
                          {item.recomendado && <Star size={12} className="star-icon" />}
                          <span>{item.aseguradora}</span>
                          {item.rankingPosicion && <span className="rank-badge">#{item.rankingPosicion}</span>}
                        </div>
                        {item.plan && <div className="cotizacion-plan">{item.plan}</div>}
                        {canEdit && (
                          <div className="cotizacion-item-actions">
                            <button className="icon-button compact" onClick={() => openEditItem(item)}><Pencil size={12} /></button>
                            <button className="icon-button compact danger" onClick={() => deleteItemAction(item.id)}><Trash2 size={12} /></button>
                          </div>
                        )}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  <CotizRow label="Prima anual"     items={items} fn={i => L(i.primaAnual)} shade />
                  <CotizRow label="Prima mensual"   items={items} fn={i => L(i.primaMensual)} />
                  <CotizRow label="Frecuencia pago" items={items} fn={i => i.frecuenciaPago.charAt(0) + i.frecuenciaPago.slice(1).toLowerCase()} shade />
                  <CotizRow label="Suma asegurada"  items={items} fn={i => L(i.sumaAsegurada)} />
                  <CotizRow label="Deducible"       items={items} fn={i => L(i.deducible)} shade />
                  <CotizRow label="Vigencia"        items={items} fn={i => i.vigenciaMeses ? `${i.vigenciaMeses} meses` : '—'} />
                  <CotizRow label="Ranking"         items={items}
                    fn={i => i.rankingPuntos != null ? `${i.rankingPuntos.toFixed(1)} pts` : '—'}
                    renderCell={(i, v) => (
                      <span className={i.recomendado ? 'rank-highlight' : ''}>{v}</span>
                    )}
                    shade
                  />

                  {/* Coberturas expandable */}
                  {items.some(i => i.coberturas.length > 0) && (() => {
                    const allCobs = [...new Set(items.flatMap(i => i.coberturas.map(c => c.nombre)))].sort()
                    return (
                      <>
                        <tr className="cotizacion-section-row">
                          <td colSpan={items.length + 1}>Coberturas</td>
                        </tr>
                        {allCobs.map(cobName => (
                          <tr key={cobName} className="cotizacion-row">
                            <td className="cotizacion-field-label">{cobName}</td>
                            {items.map(item => {
                              const cob = item.coberturas.find(c => c.nombre.toLowerCase() === cobName.toLowerCase())
                              if (!cob) return <td key={item.id} className="cob-missing">No incluye</td>
                              if (!cob.aplica) return <td key={item.id} className="cob-excluded">Excluida</td>
                              return <td key={item.id} className="cob-included">{cob.limite ?? 'Incluida'}</td>
                            })}
                          </tr>
                        ))}
                      </>
                    )
                  })()}
                </tbody>
              </table>
            </div>
          )}

          {/* Per-item coberturas / exclusiones editor */}
          {canEdit && items.length > 0 && (
            <div style={{ marginTop: 20 }}>
              <strong className="section-label">Coberturas y exclusiones por opción</strong>
              {items.map(item => (
                <div key={item.id} className="panel" style={{ marginTop: 8 }}>
                  <div
                    className="collapsible-header"
                    onClick={() => toggleExpand(item)}
                    style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}
                  >
                    {expandedItemId === item.id ? <ChevronUp size={15} /> : <ChevronDown size={15} />}
                    <span><strong>{item.aseguradora}</strong>{item.plan ? ` — ${item.plan}` : ''}</span>
                    <span className="text-muted text-sm" style={{ marginLeft: 'auto' }}>
                      {item.coberturas.length} coberturas · {item.exclusiones.length} exclusiones
                    </span>
                  </div>

                  {expandedItemId === item.id && (
                    <div style={{ marginTop: 12 }}>
                      {/* Coberturas */}
                      <div className="cob-editor-section">
                        <div className="cob-editor-header">
                          <span className="policy-section-title">Coberturas</span>
                          <button className="btn-secondary compact" onClick={() =>
                            setCobDraft(d => [...d, { nombre: '', limite: '', aplica: true }])}>
                            <Plus size={12} /> Agregar
                          </button>
                        </div>
                        {cobDraft.map((cob, idx) => (
                          <div key={idx} className="cob-row">
                            <input
                              className="field-input"
                              placeholder="Nombre de cobertura"
                              value={cob.nombre ?? ''}
                              onChange={e => setCobDraft(d => d.map((c, i) => i === idx ? { ...c, nombre: e.target.value } : c))}
                            />
                            <input
                              className="field-input"
                              placeholder="Límite / monto"
                              value={cob.limite ?? ''}
                              onChange={e => setCobDraft(d => d.map((c, i) => i === idx ? { ...c, limite: e.target.value } : c))}
                            />
                            <label className="checkbox-label">
                              <input type="checkbox" checked={cob.aplica ?? true}
                                onChange={e => setCobDraft(d => d.map((c, i) => i === idx ? { ...c, aplica: e.target.checked } : c))} />
                              Aplica
                            </label>
                            <button className="icon-button danger compact"
                              onClick={() => setCobDraft(d => d.filter((_, i) => i !== idx))}>
                              <X size={12} />
                            </button>
                          </div>
                        ))}
                        <button className="btn-primary compact" style={{ marginTop: 8 }}
                          onClick={() => saveCoberturas(item.id)} disabled={saving}>
                          <Check size={12} /> Guardar coberturas
                        </button>
                      </div>

                      {/* Exclusiones */}
                      <div className="cob-editor-section" style={{ marginTop: 16 }}>
                        <div className="cob-editor-header">
                          <span className="policy-section-title">Exclusiones</span>
                          <button className="btn-secondary compact" onClick={() =>
                            setExcDraft(d => [...d, { descripcion: '' }])}>
                            <Plus size={12} /> Agregar
                          </button>
                        </div>
                        {excDraft.map((exc, idx) => (
                          <div key={idx} className="cob-row">
                            <input
                              className="field-input"
                              placeholder="Descripción de exclusión"
                              value={exc.descripcion ?? ''}
                              onChange={e => setExcDraft(d => d.map((x, i) => i === idx ? { ...x, descripcion: e.target.value } : x))}
                              style={{ flex: 1 }}
                            />
                            <button className="icon-button danger compact"
                              onClick={() => setExcDraft(d => d.filter((_, i) => i !== idx))}>
                              <X size={12} />
                            </button>
                          </div>
                        ))}
                        <button className="btn-primary compact" style={{ marginTop: 8 }}
                          onClick={() => saveExclusiones(item.id)} disabled={saving}>
                          <Check size={12} /> Guardar exclusiones
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {/* ── TAB: Análisis ─────────────────────────────────────────────────── */}
      {tab === 'analisis' && (
        <div className="panel">
          <strong className="panel-title">Análisis y recomendación</strong>
          <div style={{ marginTop: 14 }}>
            <div className="field-group">
              <label className="field-label">Análisis comparativo</label>
              <textarea
                className="field-input"
                rows={8}
                placeholder="Escribe el análisis de las opciones: diferencias, ventajas de cada aseguradora, calidad de coberturas..."
                value={analisisDraft.analisisTexto}
                onChange={e => setAnalisisDraft(d => ({ ...d, analisisTexto: e.target.value }))}
                disabled={!canEdit}
              />
            </div>
            <div className="field-group" style={{ marginTop: 12 }}>
              <label className="field-label">Recomendación al cliente</label>
              <textarea
                className="field-input"
                rows={4}
                placeholder="Opción recomendada y razón..."
                value={analisisDraft.recomendacion}
                onChange={e => setAnalisisDraft(d => ({ ...d, recomendacion: e.target.value }))}
                disabled={!canEdit}
              />
            </div>
            {canEdit && (
              <button className="btn-primary" style={{ marginTop: 12 }} onClick={saveAnalisis} disabled={saving}>
                <Save size={14} /> {saving ? 'Guardando...' : 'Guardar análisis'}
              </button>
            )}
          </div>

          {/* Ranking summary */}
          {items.length > 0 && (
            <div style={{ marginTop: 24 }}>
              <strong className="policy-section-title">Ranking actual</strong>
              <div style={{ marginTop: 10 }}>
                {[...items].sort((a, b) => (a.rankingPosicion ?? 99) - (b.rankingPosicion ?? 99)).map(item => (
                  <div key={item.id} className={`ranking-row ${item.recomendado ? 'ranking-row-top' : ''}`}>
                    <span className="rank-num">#{item.rankingPosicion ?? '—'}</span>
                    <span className="rank-name">
                      {item.recomendado && <Star size={12} className="star-icon" />}
                      {item.aseguradora}{item.plan ? ` — ${item.plan}` : ''}
                    </span>
                    <span className="rank-pts">{item.rankingPuntos != null ? `${item.rankingPuntos.toFixed(1)} pts` : '—'}</span>
                    <span className="rank-prima">{L(item.primaAnual ?? (item.primaMensual ? item.primaMensual * 12 : null))}/año</span>
                  </div>
                ))}
              </div>
              <button className="btn-secondary" style={{ marginTop: 10 }} onClick={handleRecalcular} disabled={saving}>
                <RefreshCw size={13} /> Recalcular
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Helper component ───────────────────────────────────────────────────────────

function CotizRow({
  label, items, fn, shade = false, renderCell,
}: {
  label: string
  items: CotizacionItem[]
  fn: (i: CotizacionItem) => string
  shade?: boolean
  renderCell?: (item: CotizacionItem, value: string) => ReactNode
}) {
  return (
    <tr className={`cotizacion-row ${shade ? 'shade' : ''}`}>
      <td className="cotizacion-field-label">{label}</td>
      {items.map(item => {
        const val = fn(item)
        return (
          <td key={item.id} className={item.recomendado ? 'cotizacion-rec-col' : ''}>
            {renderCell ? renderCell(item, val) : val}
          </td>
        )
      })}
    </tr>
  )
}
