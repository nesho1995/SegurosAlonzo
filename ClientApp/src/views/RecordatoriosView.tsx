import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, Send, XCircle } from 'lucide-react'
import { descartarRecordatorio, enviarRecordatorio, enviarRecordatoriosPendientes, generarRecordatorios, getRecordatorios } from '../api/recordatoriosApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle, Toolbar } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import { useAutoRefresh } from '../hooks/useAutoRefresh'
import type { ReminderResponse } from '../types/recordatorios'
import { compactMeta, dateFmt, moneySafe } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'

export function RemindersView() {
  const [data, setData] = useState<ReminderResponse | null>(null)
  const [estado, setEstado] = useState('PENDIENTE_ENVIO')
  const [buscar, setBuscar] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ estado, pageSize: '50' })
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
      else if ('enviados' in (response || {})) setMessage(`Recordatorios enviados: ${(response as { enviados: number }).enviados}. Errores: ${(response as { errores: number }).errores}.`)
      else if (response?.ok === false) setError(response.response || 'No se pudo enviar.')
      else setMessage(success)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ estado, pageSize: '50' })
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

  useAutoRefresh(async () => {
    if (!busy) await load()
  }, 15000, !busy)

  return (
    <>
      <PageHeader
        eyebrow="Seguimiento"
        title="Recordatorios"
        description="Se alimenta desde las cuotas y polizas: genera pendientes, envia manualmente y deja auditoria del resultado."
        onRefresh={load}
      />
      <div className="inline-alert info">
        Generar recordatorios busca cuotas pendientes o vencidas segun los dias configurados. No se envia automaticamente si la automatizacion esta apagada; quedan en PENDIENTE_ENVIO para revisarlos.
      </div>
      <div className="action-row page-actions">
        <button className="primary-button" disabled={busy} onClick={() => void runAction(generarRecordatorios, 'Recordatorios generados.')}>
          <Send size={18} />Generar recordatorios
        </button>
        <button className="icon-button secondary" disabled={busy} onClick={() => void runAction(enviarRecordatoriosPendientes, 'Recordatorios pendientes enviados.')}>
          <Send size={18} />Enviar pendientes
        </button>
      </div>
      <Toolbar buscar={buscar} estado={estado} estados={['PENDIENTE_ENVIO', 'ENVIADO', 'ERROR_ENVIO', 'ENVIO_DESACTIVADO', 'DESCARTADO', 'TODOS']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
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
            <PanelTitle title={`${data.total} recordatorios`} subtitle="PENDIENTE_ENVIO se puede enviar, ERROR_ENVIO permite reintentar y ENVIADO queda cerrado." />
            <DataTable
              headers={['Cliente', 'Telefono', 'Poliza', 'Cuota', 'Fecha vence', 'Monto', 'Dias', 'Estado', 'Acciones']}
              rows={data.items.map((item) => [
                <CellTitle title={item.cliente} subtitle={item.telefono || item.asunto} />,
                item.telefono || 'Sin telefono valido',
                <CellTitle title={item.numeroPoliza || 'Sin poliza'} subtitle={compactMeta([item.aseguradora, item.ramo])} />,
                item.numeroCuota ? `#${item.numeroCuota}` : item.tipo,
                item.fechaObjetivo ? dateFmt.format(new Date(item.fechaObjetivo)) : 'Sin fecha',
                item.monto !== undefined ? moneySafe(item.monto) : '-',
                formatReminderDays(item.dias),
                <StatusPill text={statusLabel(item.estado)} tone={stateTone(item.estado)} />,
                <div className="table-actions">
                  <button className="icon-button secondary" disabled={busy || item.estado === 'ENVIADO'} onClick={() => void runAction(() => enviarRecordatorio(item.id), 'Recordatorio enviado.')}>
                    <Send size={16} />Enviar
                  </button>
                  {item.estado === 'ERROR_ENVIO' && (
                    <button className="icon-button secondary" disabled={busy} onClick={() => void runAction(() => enviarRecordatorio(item.id), 'Recordatorio reenviado.')}>
                      <Send size={16} />Reintentar
                    </button>
                  )}
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

function formatReminderDays(value?: number) {
  if (value === undefined || value === null) return '-'
  if (value < 0) return `${Math.abs(value)} dias vencido`
  if (value === 0) return 'Vence hoy'
  return `${value} dias para vencer`
}

