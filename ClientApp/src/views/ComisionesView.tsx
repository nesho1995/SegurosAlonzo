import { useEffect, useState } from 'react'
import { Check, Search, Upload } from 'lucide-react'
import { getComisiones, importarComisiones, marcarComisionRevisada, previewComisiones, type ComisionDetalle, type ComisionLote } from '../api/comisionesApi'
import { StatusPill } from '../components/Badge'
import { DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { useAuth } from '../hooks/useAuth'
import { moneySafe } from '../utils/formatters'
import { statusLabel } from '../utils/labels'

export function ComisionesView() {
  const { hasPermission } = useAuth()
  const canLoad = hasPermission('comisiones.cargar')
  const canReview = hasPermission('comisiones.editar')
  const [file, setFile] = useState<File | null>(null)
  const [lotes, setLotes] = useState<ComisionLote[]>([])
  const [detalles, setDetalles] = useState<ComisionDetalle[]>([])
  const [preview, setPreview] = useState<ComisionDetalle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState('')

  async function load() {
    const data = await getComisiones()
    setLotes(data.lotes)
    setDetalles(data.detalles)
    setLoading(false)
  }

  useEffect(() => {
    let alive = true
    getComisiones()
      .then((data) => {
        if (!alive) return
        setLotes(data.lotes)
        setDetalles(data.detalles)
      })
      .catch((err) => { if (alive) setError(err instanceof Error ? err.message : 'No se pudo cargar comisiones.') })
      .finally(() => { if (alive) setLoading(false) })
    return () => { alive = false }
  }, [])

  async function previewFile() {
    if (!canLoad) return
    if (!file) return
    const data = await previewComisiones(file)
    setPreview(data.items)
  }

  async function importFile() {
    if (!canLoad) return
    if (!file) return
    await importarComisiones(file)
    setPreview([])
    setMessage('Reporte cargado para revision.')
    await load()
  }

  async function mark(id: number) {
    if (!canReview) return
    await marcarComisionRevisada(id)
    await load()
  }

  if (loading) return <LoadingCard text="Cargando comisiones..." />
  const rows = preview.length ? preview : detalles

  return (
    <>
      <PageHeader eyebrow="Finanzas" title="Cotejo de comisiones" description="Compara reportes de aseguradoras contra las polizas registradas y revisa diferencias." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      <section className="content-grid">
        {canLoad && <article className="panel form-panel">
          <PanelTitle title="Cargar reporte" subtitle="Sube el archivo recibido de la aseguradora y revisa el resultado antes de guardar." />
          <div className="form-grid">
            <label className="wide-field"><span>Archivo de comisiones</span><input type="file" accept=".xlsx" onChange={(e) => setFile(e.target.files?.[0] || null)} /></label>
            <div className="form-actions">
              <button className="icon-button secondary" onClick={() => void previewFile()} disabled={!file}><Search size={18} />Revisar</button>
              <button className="primary-button" onClick={() => void importFile()} disabled={!file}><Upload size={18} />Guardar lote</button>
            </div>
          </div>
        </article>}
        <article className="panel">
          <PanelTitle title="Ultimos lotes" subtitle="Reportes cargados recientemente." />
          <div className="help-list">{lotes.map((lote) => <span className="inline-alert info" key={lote.id}>{lote.archivoNombre}</span>)}</div>
        </article>
      </section>
      <article className="panel">
        <PanelTitle title={preview.length ? 'Vista previa' : 'Diferencias'} subtitle="Las diferencias quedan pendientes hasta marcarlas como revisadas." />
        <DataTable headers={['Estado', 'Cliente', 'Poliza', 'Aseguradora', 'Pagada', 'Esperada', 'Diferencia', 'Accion']} rows={rows.map((item) => [
          <StatusPill text={statusLabel(item.estado)} tone={item.estado === 'COINCIDE' ? 'success' : 'warning'} />,
          item.clienteDetectado || 'Sin cliente',
          item.polizaDetectada || 'Sin poliza',
          item.aseguradoraDetectada || 'Sin aseguradora',
          moneySafe(item.comisionDetectada),
          moneySafe(item.comisionEsperada),
          moneySafe(item.diferencia),
          preview.length ? <span className="muted-text">Pendiente de guardar</span> : item.revisado ? <StatusPill text="Revisada" tone="success" /> : canReview ? <button className="icon-button secondary" onClick={() => void mark(item.id)}><Check size={16} />Revisar</button> : <span className="muted-text">Pendiente</span>,
        ])} />
      </article>
    </>
  )
}
