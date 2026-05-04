import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrowLeft, Download, FileDown, FileText, Loader2, Medal, Pencil, Trash2, Upload, X } from 'lucide-react'
import {
  actualizarItem, eliminarItem, getComparativoDetalle,
  subirPdf, urlExcelComparativo, urlPdfComparativo,
} from '../api/comparativoApi'
import { ErrorCard } from '../components/ErrorAlert'
import { StatusPill } from '../components/Badge'
import type { ComparativoDetalle, ComparativoItem } from '../types/comparativo'

interface Props {
  comparativoId: number
  onBack: () => void
}

const lps = (v?: number | null) =>
  v != null && v > 0 ? `L ${v.toLocaleString('es-HN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '—'

const pct = (v?: number | null, isPct?: boolean) =>
  v != null && v > 0 ? (isPct ? `${v.toFixed(2)}%` : lps(v)) : '—'

export function ComparativoRevisionView({ comparativoId, onBack }: Props) {
  const [detalle, setDetalle]     = useState<ComparativoDetalle | null>(null)
  const [error, setError]         = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [editId, setEditId]       = useState<number | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async () => {
    try { setDetalle(await getComparativoDetalle(comparativoId)); setError(null) }
    catch (e) { setError(e instanceof Error ? e.message : 'Error') }
  }, [comparativoId])

  useEffect(() => { load() }, [load])

  // ─── Upload PDF ────────────────────────────────────────────────────────────

  async function handleFiles(files: FileList | null) {
    if (!files || files.length === 0) return
    setUploading(true)
    setError(null)
    for (const f of Array.from(files)) {
      try { await subirPdf(comparativoId, f) }
      catch (e) { setError(e instanceof Error ? e.message : 'Error al subir PDF') }
    }
    setUploading(false)
    load()
  }

  // Drag & drop
  const [dragging, setDragging] = useState(false)
  function onDrop(e: React.DragEvent) {
    e.preventDefault(); setDragging(false)
    handleFiles(e.dataTransfer.files)
  }

  // ─── Eliminar item ─────────────────────────────────────────────────────────

  async function handleDelete(itemId: number) {
    if (!confirm('¿Quitar esta aseguradora del comparativo?')) return
    try { await eliminarItem(comparativoId, itemId); load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Error') }
  }

  if (!detalle) return <div className="empty-state"><Loader2 className="spin" size={24} />Cargando…</div>

  const { comparativo, items } = detalle

  // ─── Render ────────────────────────────────────────────────────────────────

  return (
    <>
      {/* Header */}
      <div className="comp-header">
        <button className="icon-button secondary" onClick={onBack}><ArrowLeft size={16} />Volver</button>
        <div className="comp-header-info">
          <h2>{comparativo.cliente}</h2>
          {comparativo.vehiculo && <span className="muted-text">{comparativo.vehiculo}</span>}
        </div>
        <div className="comp-header-actions">
          <a className="icon-button secondary" href={urlExcelComparativo(comparativoId)} download>
            <Download size={15} />Excel
          </a>
          <a className="icon-button secondary" href={urlPdfComparativo(comparativoId)} target="_blank" rel="noreferrer">
            <FileDown size={15} />PDF cliente
          </a>
        </div>
      </div>

      {error && <ErrorCard text={error} />}

      {/* Zona de upload */}
      <div
        className={`comp-dropzone${dragging ? ' dragging' : ''}`}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        onClick={() => inputRef.current?.click()}
      >
        <input ref={inputRef} type="file" accept=".pdf" multiple hidden
          onChange={e => handleFiles(e.target.files)} />
        {uploading
          ? <><Loader2 className="spin" size={20} /><span>Procesando PDFs…</span></>
          : <><Upload size={20} /><span>Arrastra aquí los PDFs de las aseguradoras o haz clic para seleccionar</span></>
        }
      </div>

      {items.length === 0 && (
        <div className="empty-state">
          <FileText size={32} />
          <p>Sube al menos 2 PDFs para generar el comparativo.</p>
        </div>
      )}

      {items.length > 0 && (
        <>
          {/* ── Tabla comparativa ───────────────────────────────────── */}
          <div className="comp-table-wrap">
            <table className="comp-table">
              <thead>
                <tr>
                  <th className="comp-th-label">Campo</th>
                  {items.map(item => (
                    <th key={item.id} className="comp-th-insurer">
                      <div className="comp-insurer-head">
                        {item.posicion === 1 && <Medal size={14} className="comp-medal" />}
                        <span>{item.aseguradora}</span>
                        <div className="comp-insurer-actions">
                          <button className="icon-button-xs" onClick={() => setEditId(editId === item.id ? null : item.id)} title="Editar"><Pencil size={12} /></button>
                          <button className="icon-button-xs danger" onClick={() => handleDelete(item.id)} title="Quitar"><X size={12} /></button>
                        </div>
                      </div>
                      {item.score != null && (
                        <div className={`comp-score score-${item.posicion === 1 ? 'gold' : item.posicion === 2 ? 'silver' : 'bronze'}`}>
                          Score: {item.score.toFixed(1)}/100
                        </div>
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                <CompRow label="Prima anual"        values={items.map(i => lps(i.primaAnual))}         highlight="min-lps" items={items} field="primaAnual" />
                <CompRow label="Prima contado"       values={items.map(i => lps(i.primaContado))}       highlight="min-lps" items={items} field="primaContado" />
                <CompRow label="Descuento contado"   values={items.map(i => pct(i.descuentoContado, i.descuentoEsPorcentaje))}  highlight="max-num" items={items} field="descuentoContado" />
                <CompRow label="Ahorro al contado"   values={items.map(i => lps(i.ahorroContado))}      highlight="max-lps" items={items} field="ahorroContado" />
                <CompRow label="Prima financiada"    values={items.map(i => lps(i.primaFinanciada))}    highlight="min-lps" items={items} field="primaFinanciada" />
                <CompRow label="Recargo financ."     values={items.map(i => pct(i.recargoFinanciamiento, i.recargoEsPorcentaje))} highlight="min-num" items={items} field="recargoFinanciamiento" />
                <CompRow label="Prima mensual"       values={items.map(i => lps(i.primaMensual))}       highlight="min-lps" items={items} field="primaMensual" />
                <CompRow label="Suma asegurada"      values={items.map(i => lps(i.sumaAsegurada))}      highlight="max-lps" items={items} field="sumaAsegurada" />
                <CompRow label="Deducible colisión"  values={items.map(i => pct(i.deducibleColision, i.deducibleColisionEsPorcentaje))} highlight="min-num" items={items} field="deducibleColision" />
                <CompRow label="Deducible robo"      values={items.map(i => pct(i.deducibleRobo, i.deducibleRoboEsPorcentaje))}      highlight="min-num" items={items} field="deducibleRobo" />
                <CompRow label="Forma de pago"       values={items.map(i => i.formaPago ?? '—')}        highlight="none"    items={items} field="formaPago" />
                <CompRow label="Vigencia"            values={items.map(i => `${i.vigenciaDesde ?? '—'} → ${i.vigenciaHasta ?? '—'}`)} highlight="none" items={items} field="vigenciaDesde" />
              </tbody>
            </table>
          </div>

          {/* ── Coberturas ───────────────────────────────────────────── */}
          <CoberturasTable items={items} />

          {/* ── Paneles de edición inline ────────────────────────────── */}
          {editId != null && (
            <EditPanel
              item={items.find(i => i.id === editId)!}
              comparativoId={comparativoId}
              onSaved={() => { setEditId(null); load() }}
              onClose={() => setEditId(null)}
            />
          )}
        </>
      )}
    </>
  )
}

// ─── Fila de comparación ──────────────────────────────────────────────────────

type HighlightMode = 'min-lps' | 'max-lps' | 'min-num' | 'max-num' | 'none'

function CompRow({
  label, values, highlight, items, field,
}: {
  label: string
  values: string[]
  highlight: HighlightMode
  items: ComparativoItem[]
  field: keyof ComparativoItem
}) {
  // Determinar índice ganador
  let winnerIdx = -1
  if (highlight !== 'none') {
    const nums = items.map(i => {
      const v = i[field]
      return typeof v === 'number' ? v : null
    })
    const valid = nums.filter(v => v != null && v > 0) as number[]
    if (valid.length > 0) {
      const target = highlight.startsWith('min') ? Math.min(...valid) : Math.max(...valid)
      winnerIdx = nums.findIndex(v => v === target)
    }
  }

  return (
    <tr className="comp-row">
      <td className="comp-td-label">{label}</td>
      {values.map((v, i) => (
        <td key={i} className={`comp-td-value${i === winnerIdx ? ' comp-winner' : ''}`}>
          {v}
          {i === winnerIdx && <span className="comp-best-badge">✓ mejor</span>}
        </td>
      ))}
    </tr>
  )
}

// ─── Tabla de coberturas ──────────────────────────────────────────────────────

function CoberturasTable({ items }: { items: ComparativoItem[] }) {
  const allCobs = [...new Set(items.flatMap(i => i.coberturas))].sort()
  if (allCobs.length === 0) return null

  return (
    <div className="comp-cob-section">
      <h3 className="comp-section-title">Coberturas incluidas</h3>
      <div className="comp-table-wrap">
        <table className="comp-table">
          <thead>
            <tr>
              <th className="comp-th-label">Cobertura</th>
              {items.map(i => (
                <th key={i.id} className="comp-th-insurer">{i.aseguradora}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {allCobs.map(cob => (
              <tr key={cob} className="comp-row">
                <td className="comp-td-label">{cob}</td>
                {items.map(i => {
                  const has = i.coberturas.includes(cob)
                  return (
                    <td key={i.id} className={`comp-td-value comp-cob-cell${has ? ' comp-winner' : ''}`}>
                      {has ? <StatusPill text="✓ Incluida" tone="success" /> : <span className="muted-text">—</span>}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ─── Panel edición ────────────────────────────────────────────────────────────

function EditPanel({ item, comparativoId, onSaved, onClose }: {
  item: ComparativoItem
  comparativoId: number
  onSaved: () => void
  onClose: () => void
}) {
  const [form, setForm] = useState({ ...item })
  const [saving, setSaving] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const set = (k: keyof ComparativoItem, v: unknown) =>
    setForm(f => ({ ...f, [k]: v }))

  async function handleSave() {
    setSaving(true)
    try { await actualizarItem(comparativoId, form); onSaved() }
    catch (e) { setErr(e instanceof Error ? e.message : 'Error') }
    finally { setSaving(false) }
  }

  return (
    <div className="comp-edit-panel">
      <div className="comp-edit-header">
        <strong>Editar — {item.aseguradora}</strong>
        <button className="icon-button-xs" onClick={onClose}><X size={14} /></button>
      </div>
      {err && <ErrorCard text={err} />}
      <div className="comp-edit-grid">
        <Field label="Aseguradora" value={form.aseguradora} onChange={v => set('aseguradora', v)} />
        <Field label="Prima anual" value={form.primaAnual} onChange={v => set('primaAnual', num(v))} type="number" />
        <Field label="Prima contado" value={form.primaContado} onChange={v => set('primaContado', num(v))} type="number" />
        <Field label="Desc. contado (%)" value={form.descuentoContado} onChange={v => set('descuentoContado', num(v))} type="number" />
        <Field label="Prima financiada" value={form.primaFinanciada} onChange={v => set('primaFinanciada', num(v))} type="number" />
        <Field label="Recargo financ. (%)" value={form.recargoFinanciamiento} onChange={v => set('recargoFinanciamiento', num(v))} type="number" />
        <Field label="Prima mensual" value={form.primaMensual} onChange={v => set('primaMensual', num(v))} type="number" />
        <Field label="Suma asegurada" value={form.sumaAsegurada} onChange={v => set('sumaAsegurada', num(v))} type="number" />
        <Field label="Deduc. colisión (%)" value={form.deducibleColision} onChange={v => set('deducibleColision', num(v))} type="number" />
        <Field label="Deduc. robo (%)" value={form.deducibleRobo} onChange={v => set('deducibleRobo', num(v))} type="number" />
        <Field label="Forma de pago" value={form.formaPago} onChange={v => set('formaPago', v)} />
        <Field label="Vigencia desde" value={form.vigenciaDesde} onChange={v => set('vigenciaDesde', v)} />
        <Field label="Vigencia hasta" value={form.vigenciaHasta} onChange={v => set('vigenciaHasta', v)} />
      </div>
      <div className="form-actions">
        <button className="primary-button" onClick={handleSave} disabled={saving}>
          {saving ? 'Guardando…' : 'Guardar cambios'}
        </button>
        <button className="secondary-button" onClick={onClose}>Cancelar</button>
      </div>
    </div>
  )
}

function Field({ label, value, onChange, type = 'text' }: {
  label: string
  value?: string | number | null
  onChange: (v: string) => void
  type?: string
}) {
  return (
    <div className="field-group">
      <label>{label}</label>
      <input
        className="form-input"
        type={type}
        value={value ?? ''}
        onChange={e => onChange(e.target.value)}
      />
    </div>
  )
}

const num = (v: string) => v === '' ? undefined : parseFloat(v)
