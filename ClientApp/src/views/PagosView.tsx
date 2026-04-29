import { useEffect, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle2 } from 'lucide-react'
import { getPagos, registrarPagoCuota } from '../api/pagosApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle, Toolbar } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import { DocumentosPanel } from '../components/DocumentosPanel'
import type { Payment, PaymentsResponse } from '../types/pagos'
import { compactMeta, dateFmt, moneySafe } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'

export function PaymentsView() {
  const [data, setData] = useState<PaymentsResponse | null>(null)
  const [estado, setEstado] = useState('TODOS')
  const [buscar, setBuscar] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedPayment, setSelectedPayment] = useState<Payment | null>(null)
  const [paymentForm, setPaymentForm] = useState({ monto: '', fechaPago: '', metodoPago: 'TRANSFERENCIA', documentoId: '', numeroRecibo: '', referenciaBanco: '', observaciones: '' })
  const [message, setMessage] = useState<string | null>(null)
  const [hideList, setHideList] = useState(false)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ pageSize: '15' })
      if (estado !== 'TODOS') query.set('estado', estado)
      if (buscar.trim()) query.set('buscar', buscar.trim())
      setData(await getPagos(query))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  async function registerPayment() {
    if (!selectedPayment) return
    const monto = Number(paymentForm.monto)
    if (!monto || monto <= 0) {
      setError('El monto del pago debe ser mayor a cero.')
      return
    }
    setError(null)
    setMessage(null)
    try {
      await registrarPagoCuota(selectedPayment.id, {
        monto,
        fechaPago: paymentForm.fechaPago || undefined,
        metodoPago: paymentForm.metodoPago,
        documentoId: paymentForm.documentoId ? Number(paymentForm.documentoId) : undefined,
        numeroRecibo: paymentForm.numeroRecibo || undefined,
        referenciaBanco: paymentForm.referenciaBanco || undefined,
        observaciones: paymentForm.observaciones || undefined,
      })
      setMessage('Pago registrado.')
      setPaymentForm({ monto: '', fechaPago: '', metodoPago: 'TRANSFERENCIA', documentoId: '', numeroRecibo: '', referenciaBanco: '', observaciones: '' })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ pageSize: '15' })
    if (estado !== 'TODOS') query.set('estado', estado)
    if (buscar.trim()) query.set('buscar', buscar.trim())

    getPagos(query)
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
        eyebrow="Cobranza"
        title="Pagos y cuotas"
        description="Bandeja para revisar cuotas vencidas, pendientes y pagadas desde la cartera actual."
        onRefresh={load}
      />
      <Toolbar buscar={buscar} estado={estado} estados={['TODOS', 'VENCIDA', 'PENDIENTE', 'PARCIAL', 'PAGADA']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
      {loading && <LoadingCard text="Cargando cuotas..." />}
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      {data && (
        <>
          <div className="action-row page-actions">
            <button className="icon-button secondary" type="button" onClick={() => setHideList((value) => !value)}>
              {hideList ? 'Mostrar cuotas' : 'Ocultar cuotas'}
            </button>
          </div>
          <section className="mini-grid">
            <Metric title="Vencidas" value={data.stats.vencidas} hint={moneySafe(data.stats.montoPendiente)} tone="red" icon={AlertTriangle} />
            <Metric title="Pendientes" value={data.stats.pendientes} hint="Por vencer" tone="amber" icon={CalendarClock} />
            <Metric title="Parciales" value={data.stats.parciales} hint="Con abonos" tone="blue" icon={CalendarClock} />
            <Metric title="Pagadas" value={data.stats.pagadas} hint="Historico" tone="green" icon={CheckCircle2} />
          </section>
          {!hideList && <article className="panel">
            <PanelTitle title={`${data.total} cuotas`} subtitle="Primeras filas de cobranza para validar datos antes de activar automatizaciones." />
            <DataTable
              headers={['Cliente', 'Cuota', 'Poliza', 'Vence', 'Monto', 'Pagado', 'Estado', 'Documentos']}
              rows={data.items.map((item) => [
                <CellTitle title={item.cliente} subtitle={item.telefono || 'Sin telefono'} />,
                `#${item.numeroCuota}`,
                <CellTitle title={item.numeroPoliza || 'Sin poliza'} subtitle={compactMeta([item.aseguradora, item.ramo])} />,
                dateFmt.format(new Date(item.fechaVencimiento)),
                moneySafe(item.monto),
                moneySafe(item.montoPagado),
                <StatusPill text={statusLabel(item.estado)} tone={stateTone(item.estado)} />,
                <button className="icon-button secondary" onClick={() => setSelectedPayment(item)}>Ver documentos</button>,
              ])}
            />
          </article>}
          {selectedPayment && (
            <article className="panel mt-panel">
              <PanelTitle title="Pago y comprobantes" subtitle={`${selectedPayment.cliente} / cuota ${selectedPayment.numeroCuota}`} />
              <div className="form-grid compact-form">
                <label className="field"><span>Monto</span><input type="number" value={paymentForm.monto} onChange={(event) => setPaymentForm({ ...paymentForm, monto: event.target.value })} /></label>
                <label className="field"><span>Fecha pago</span><input type="date" value={paymentForm.fechaPago} onChange={(event) => setPaymentForm({ ...paymentForm, fechaPago: event.target.value })} /></label>
                <label className="field"><span>Metodo</span><select value={paymentForm.metodoPago} onChange={(event) => setPaymentForm({ ...paymentForm, metodoPago: event.target.value })}><option>TRANSFERENCIA</option><option>DEBITO</option><option>EFECTIVO</option><option>TARJETA</option><option>OTRO</option></select></label>
                <label className="field"><span>Documento ID</span><input type="number" value={paymentForm.documentoId} onChange={(event) => setPaymentForm({ ...paymentForm, documentoId: event.target.value })} /></label>
                <label className="field"><span>Recibo</span><input value={paymentForm.numeroRecibo} onChange={(event) => setPaymentForm({ ...paymentForm, numeroRecibo: event.target.value })} /></label>
                <label className="field"><span>Referencia</span><input value={paymentForm.referenciaBanco} onChange={(event) => setPaymentForm({ ...paymentForm, referenciaBanco: event.target.value })} /></label>
                <label className="wide-field"><span>Observaciones</span><textarea value={paymentForm.observaciones} onChange={(event) => setPaymentForm({ ...paymentForm, observaciones: event.target.value })} /></label>
                <div className="form-actions wide-field"><button className="primary-button" onClick={() => void registerPayment()}>Registrar pago</button></div>
              </div>
              <DocumentosPanel entidadTipo="PAGO" entidadId={selectedPayment.id} />
            </article>
          )}
        </>
      )}
    </>
  )
}
