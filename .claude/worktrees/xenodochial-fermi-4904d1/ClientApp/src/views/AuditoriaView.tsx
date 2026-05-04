import { useEffect, useState } from 'react'
import { getAuditoria } from '../api/auditoriaApi'
import { CellTitle, DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import type { AuditResponse } from '../types/auditoria'
import { dateFmt } from '../utils/formatters'
import { statusLabel } from '../utils/labels'

export function AuditoriaView() {
  const [data, setData] = useState<AuditResponse | null>(null)
  const [buscar, setBuscar] = useState('')
  const [tipo, setTipo] = useState('TODOS')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ pageSize: '80' })
      if (buscar.trim()) query.set('buscar', buscar.trim())
      if (tipo !== 'TODOS') query.set('tipo', tipo)
      if (desde) query.set('desde', desde)
      if (hasta) query.set('hasta', hasta)
      setData(await getAuditoria(query))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ pageSize: '80' })
    if (buscar.trim()) query.set('buscar', buscar.trim())
    if (tipo !== 'TODOS') query.set('tipo', tipo)
    if (desde) query.set('desde', desde)
    if (hasta) query.set('hasta', hasta)

    getAuditoria(query)
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
  }, [buscar, tipo, desde, hasta])

  return (
    <>
      <PageHeader eyebrow="Auditoria" title="Trazabilidad empresarial" description="Registro de acciones relevantes para respaldo legal, seguridad y automatizaciones futuras." onRefresh={load} />
      <article className="panel form-panel">
        <PanelTitle title="Filtros" subtitle="Busca por usuario, accion, descripcion o entidad." />
        <div className="form-grid">
          <Field label="Buscar" value={buscar} onChange={setBuscar} />
          <label className="field">
            <span>Tipo</span>
            <select value={tipo} onChange={(event) => setTipo(event.target.value)}>
              {['TODOS', 'CLIENTE', 'POLIZA', 'RECLAMO', 'PAGO'].map((item) => <option key={item} value={item}>{statusLabel(item)}</option>)}
            </select>
          </label>
          <Field label="Desde" type="date" value={desde} onChange={setDesde} />
          <Field label="Hasta" type="date" value={hasta} onChange={setHasta} />
        </div>
      </article>
      {loading && <LoadingCard text="Cargando auditoria..." />}
      {error && <ErrorCard text={error} />}
      {data && (
        <article className="panel">
          <PanelTitle title={`${data.total} eventos`} subtitle="Ultimos eventos registrados en el sistema." />
          <DataTable
            headers={['Usuario', 'Accion', 'Entidad', 'Fecha', 'Descripcion']}
            rows={data.items.map((item) => [
              item.usuario || 'Sistema',
              statusLabel(item.accion),
              <CellTitle title={statusLabel(item.entidadTipo)} subtitle={item.entidadId ? `#${item.entidadId}` : 'Sin id'} />,
              dateFmt.format(new Date(item.fecha)),
              item.descripcion,
            ])}
          />
        </article>
      )}
    </>
  )
}
