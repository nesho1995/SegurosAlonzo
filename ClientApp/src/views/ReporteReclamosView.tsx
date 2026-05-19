import { useEffect, useMemo, useState } from 'react'
import { ClipboardList, FileText, Phone, TimerReset } from 'lucide-react'
import { getReporteReclamos } from '../api/reportesApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { Metric } from '../components/StatCard'
import { PageHeader } from '../components/Topbar'
import type { ReporteReclamosResponse } from '../types/reportes'
import { dateFmt } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'

const estados = ['TODOS', 'EN_SEGUIMIENTO', 'DOCUMENTOS_PENDIENTES', 'DOCUMENTOS_COMPLETOS', 'COMPLETO', 'ASEGURADORA_APROBADO', 'ERROR']

function defaultDesde() {
  const date = new Date()
  date.setDate(date.getDate() - 7)
  return date.toISOString().slice(0, 10)
}

export function ReporteReclamosView() {
  const [data, setData] = useState<ReporteReclamosResponse | null>(null)
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('TODOS')
  const [ciudad, setCiudad] = useState('')
  const [desde, setDesde] = useState(defaultDesde)
  const [hasta, setHasta] = useState('')
  const [soloConMovimiento, setSoloConMovimiento] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const query = useMemo(() => {
    const params = new URLSearchParams({ pageSize: '300' })
    if (buscar.trim()) params.set('buscar', buscar.trim())
    if (estado !== 'TODOS') params.set('estado', estado)
    if (ciudad.trim()) params.set('ciudad', ciudad.trim())
    if (desde) params.set('desde', desde)
    if (hasta) params.set('hasta', hasta)
    if (soloConMovimiento) params.set('soloConMovimiento', 'true')
    return params
  }, [buscar, estado, ciudad, desde, hasta, soloConMovimiento])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await getReporteReclamos(query))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    setLoading(true)
    getReporteReclamos(query)
      .then((json) => {
        if (alive) setData(json)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })
    return () => {
      alive = false
    }
  }, [query])

  return (
    <>
      <PageHeader eyebrow="Reportes" title="Seguimiento de reclamos" description="Vista operativa para revisar movimientos, pendientes y ultimo seguimiento por reclamo." onRefresh={load} />
      <article className="panel form-panel">
        <PanelTitle title="Filtros" subtitle="Revisa por dia, ciudad, estado, cliente, poliza, placa o reclamo." />
        <div className="form-grid">
          <Field label="Buscar" value={buscar} onChange={setBuscar} />
          <label className="field">
            <span>Estado</span>
            <select value={estado} onChange={(event) => setEstado(event.target.value)}>
              {estados.map((item) => <option key={item} value={item}>{statusLabel(item)}</option>)}
            </select>
          </label>
          <Field label="Ciudad" value={ciudad} onChange={setCiudad} />
          <Field label="Desde" type="date" value={desde} onChange={setDesde} />
          <Field label="Hasta" type="date" value={hasta} onChange={setHasta} />
          <label className="check-field">
            <input type="checkbox" checked={soloConMovimiento} onChange={(event) => setSoloConMovimiento(event.target.checked)} />
            Solo con movimiento
          </label>
        </div>
      </article>
      {loading && <LoadingCard text="Cargando reporte..." />}
      {error && <ErrorCard text={error} />}
      {data && (
        <>
          <section className="mini-grid">
            <Metric title="Reclamos" value={data.resumen.total} hint="Segun filtros" tone="blue" icon={ClipboardList} />
            <Metric title="Con pendientes" value={data.resumen.conPendientes} hint="Documentos abiertos" tone="amber" icon={FileText} />
            <Metric title="Sin movimiento" value={data.resumen.sinMovimientoPeriodo} hint="En el rango" tone="red" icon={TimerReset} />
            <Metric title="Sin telefono" value={data.resumen.sinTelefono} hint="Revisar contacto" tone="slate" icon={Phone} />
          </section>
          <article className="panel">
            <PanelTitle title={`${data.items.length} reclamos visibles`} subtitle="Ordenado por el ultimo movimiento registrado." />
            <DataTable
              headers={['Reclamo', 'Cliente', 'Estado', 'Pendientes', 'Observacion inicial', 'Movimientos', 'Ultimo seguimiento', 'Contacto']}
              rows={data.items.map((item) => [
                <CellTitle title={item.reclamo || `#${item.id}`} subtitle={[item.poliza, item.placa, item.ciudadDetectada].filter(Boolean).join(' / ')} />,
                <CellTitle title={item.conductor || item.asegurado || 'Sin cliente'} subtitle={item.asegurado && item.asegurado !== item.conductor ? item.asegurado : undefined} />,
                <StatusPill text={statusLabel(item.estado)} tone={stateTone(item.estado)} />,
                <CellTitle title={`${item.documentosPendientes} pendientes`} subtitle={`${item.documentosRecibidos} recibidos`} />,
                <span style={{ whiteSpace: 'pre-wrap' }}>{extractObservacion(item.descripcion)}</span>,
                <CellTitle title={`${item.eventosPeriodo} en rango`} subtitle={item.cantidadRecordatorios ? `${item.cantidadRecordatorios} recordatorios` : 'Sin recordatorios'} />,
                <CellTitle
                  title={item.ultimoMovimientoAccion ? statusLabel(item.ultimoMovimientoAccion) : 'Sin movimiento'}
                  subtitle={item.ultimoMovimientoFecha ? `${dateFmt.format(new Date(item.ultimoMovimientoFecha))} / ${item.ultimoMovimientoUsuario || 'Sistema'} / ${item.ultimoMovimientoDescripcion || ''}` : 'Sin auditoria'}
                />,
                <CellTitle title={item.celular || 'Sin telefono'} subtitle={item.fechaUltimoRecordatorio ? `Ultimo recordatorio ${dateFmt.format(new Date(item.fechaUltimoRecordatorio))}` : 'Sin recordatorio'} />,
              ])}
              maxHeight="680px"
            />
          </article>
        </>
      )}
    </>
  )
}

function extractObservacion(value?: string) {
  if (!value?.trim()) return 'Sin observacion'
  const marker = 'Observaciones:'
  const index = value.indexOf(marker)
  if (index < 0) return value
  return value.slice(index + marker.length).trim() || 'Sin observacion'
}
