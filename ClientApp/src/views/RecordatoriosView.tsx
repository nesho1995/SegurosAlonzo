import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, Send, XCircle } from 'lucide-react'
import { descartarRecordatorio, enviarRecordatorio, generarRecordatorios, getRecordatorios } from '../api/recordatoriosApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle, Toolbar } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import type { ReminderResponse } from '../types/recordatorios'
import { compactMeta, dateFmt } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'

export function RemindersView() {
  const [data, setData] = useState<ReminderResponse | null>(null)
  const [estado, setEstado] = useState('PENDIENTE')
  const [buscar, setBuscar] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ estado, pageSize: '15' })
      if (buscar.trim()) query.set('buscar', buscar.trim())
      setData(await getRecordatorios(query))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  async function runAction(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setError(null)
    setMessage('')
    try {
      const result = await action()
      const response = result as { ok?: boolean; response?: string; creados?: number } | null
      if (typeof response?.creados === 'number') setMessage(`Recordatorios generados: ${response.creados}.`)
      else setMessage(response?.ok === false ? response.response || 'No se pudo enviar.' : success)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ estado, pageSize: '15' })
    if (buscar.trim()) query.set('buscar', buscar.trim())

    getRecordatorios(query)
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
  }, [estado, buscar])

  return (
    <>
      <PageHeader
        eyebrow="Seguimiento"
        title="Recordatorios"
        description="Bandeja para revisar pagos, vencimientos y renovaciones generados desde la cartera."
        onRefresh={load}
      />
      <div className="action-row page-actions">
        <button className="primary-button" disabled={busy} onClick={() => void runAction(generarRecordatorios, 'Recordatorios generados.')}>
          <Send size={18} />Generar pendientes
        </button>
      </div>
      <Toolbar buscar={buscar} estado={estado} estados={['PENDIENTE', 'ENVIADO', 'ERROR', 'DESCARTADO']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
      {loading && <LoadingCard text="Cargando recordatorios..." />}
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      {data && (
        <>
          <section className="mini-grid">
            <Metric title="Pendientes" value={data.stats.pendientes} hint="Por revisar" tone="amber" icon={Send} />
            <Metric title="Enviados" value={data.stats.enviados} hint="Completados" tone="green" icon={CheckCircle2} />
            <Metric title="Errores" value={data.stats.errores} hint="Requieren atencion" tone="red" icon={AlertTriangle} />
          </section>
          <article className="panel">
            <PanelTitle title={`${data.total} recordatorios`} subtitle="Bandeja operativa para revisar pagos, vencimientos y renovaciones." />
            <DataTable
              headers={['Cliente', 'Tipo', 'Poliza', 'Fecha', 'Estado']}
              rows={data.items.map((item) => [
                <CellTitle title={item.cliente} subtitle={item.telefono || item.asunto} />,
                item.tipo,
                <CellTitle title={item.numeroPoliza || 'Sin poliza'} subtitle={compactMeta([item.aseguradora, item.ramo])} />,
                item.fechaObjetivo ? dateFmt.format(new Date(item.fechaObjetivo)) : 'Sin fecha',
                <div className="table-actions">
                  <StatusPill text={statusLabel(item.estado)} tone={stateTone(item.estado)} />
                  <button className="icon-button secondary" disabled={busy || item.estado === 'ENVIADO'} onClick={() => void runAction(() => enviarRecordatorio(item.id), 'Recordatorio enviado.')}>
                    <Send size={16} />Enviar
                  </button>
                  <button className="icon-button danger-button" disabled={busy || item.estado === 'ENVIADO'} onClick={() => void runAction(() => descartarRecordatorio(item.id), 'Recordatorio descartado.')}>
                    <XCircle size={16} />Descartar
                  </button>
                </div>,
              ])}
            />
          </article>
        </>
      )}
    </>
  )
}

