import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle2, Eye, FileUp, Pencil } from 'lucide-react'
import { actualizarFechaCuota, getPagos, registrarPagoCuota } from '../api/pagosApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle, Toolbar } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import { DocumentosPanel } from '../components/DocumentosPanel'
import { AccordionSection } from '../components/AccordionSection'
import { notify } from '../components/ToastHost'
import { useAutoRefresh } from '../hooks/useAutoRefresh'
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
  const [selectedAction, setSelectedAction] = useState<'pago' | 'comprobante' | 'docs'>('pago')
  const [paymentForm, setPaymentForm] = useState({ monto: '', fechaPago: '', metodoPago: 'TRANSFERENCIA', documentoId: '', numeroRecibo: '', referenciaBanco: '', observaciones: '' })
  const [message, setMessage] = useState<string | null>(null)
  const [hideList, setHideList] = useState(false)
  const [editingFecha, setEditingFecha] = useState<{ id: number; fecha: string } | null>(null)
  const paymentPanelRef = useRef<HTMLElement | null>(null)
  const saldoSeleccionado = selectedPayment ? Math.max(0, Number(selectedPayment.monto || 0) - Number(selectedPayment.montoPagado || 0)) : 0

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ pageSize: '50' })
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
      notify('Pago registrado correctamente.', 'success')
      setPaymentForm({ monto: '', fechaPago: '', metodoPago: 'TRANSFERENCIA', documentoId: '', numeroRecibo: '', referenciaBanco: '', observaciones: '' })
      await load()
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error inesperado.'
      setError(text)
      notify(text, 'error')
    }
  }

  async function guardarFecha() {
    if (!editingFecha) return
    try {
      await actualizarFechaCuota(editingFecha.id, editingFecha.fecha)
      notify('Fecha actualizada.', 'success')
      setEditingFecha(null)
      load()
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error al actualizar fecha.'
      notify(text, 'error')
    }
  }

  function selectPayment(item: Payment, action: 'pago' | 'comprobante' | 'docs' = 'pago') {
    const saldo = Math.max(0, Number(item.monto || 0) - Number(item.montoPagado || 0))
    setSelectedPayment(item)
    setSelectedAction(action)
    setPaymentForm((current) => ({ ...current, monto: saldo > 0 ? String(saldo) : current.monto }))
    window.setTimeout(() => paymentPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 50)
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ pageSize: '50' })
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

  useAutoRefresh(load, 15000)

  return (
    <>
      <PageHeader
        eyebrow="Cobranza"
        title="Pagos y cuotas"
        description="Cada fila representa una cuota real de una poliza: pendiente por cobrar, parcial o pagada."
        onRefresh={load}
      />
      <Toolbar buscar={buscar} estado={estado} estados={['TODOS', 'PENDIENTE', 'VENCIDA', 'PAGADA', 'PARCIAL', 'HOY', 'PROXIMOS_7']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
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
          <div className="inline-alert info">
            Si una cuota no tiene pago registrado aparece como cuenta por cobrar. Al registrar un abono se actualiza el estado y aqui mismo puedes adjuntar o revisar el comprobante.
          </div>
          <section className="mini-grid">
            <Metric title="Vencidas" value={data.stats.vencidas} hint={moneySafe(data.stats.montoPendiente)} tone="red" icon={AlertTriangle} />
            <Metric title="Pendientes" value={data.stats.pendientes} hint="Por vencer" tone="amber" icon={CalendarClock} />
            <Metric title="Parciales" value={data.stats.parciales} hint="Con abonos" tone="blue" icon={CalendarClock} />
            <Metric title="Pagadas" value={data.stats.pagadas} hint="Historico" tone="green" icon={CheckCircle2} />
          </section>
          {data.alertas && data.alertas.length > 0 && (
            <div className="inline-alert warning">
              {data.alertas.length === 1
                ? `Esta poliza no tiene cuotas generadas: ${data.alertas[0].cliente} / ${data.alertas[0].numeroPoliza || 'sin poliza'}.`
                : `${data.alertas.length} polizas no tienen cuotas generadas.`}
            </div>
          )}
          {!hideList && <article className="panel">
            <PanelTitle title={`${data.total} cuotas visibles`} subtitle="Incluye pagos registrados y cuotas pendientes aunque aun no tengan pago." />
            <DataTable
              headers={['Cliente', 'Poliza', 'Cuota', 'Vence', 'Monto cuota', 'Estado', 'Pagado', 'Metodo', 'Referencia', 'Acciones']}
              rows={data.items.map((item) => [
                <CellTitle title={item.cliente} subtitle={item.telefono || 'Sin telefono'} />,
                <CellTitle title={item.numeroPoliza || 'Sin poliza'} subtitle={compactMeta([item.aseguradora, item.ramo])} />,
                `#${item.numeroCuota}`,
                dateFmt.format(new Date(item.fechaVencimiento)),
                moneySafe(item.monto),
                <StatusPill text={statusLabel(item.estado)} tone={stateTone(item.estado)} />,
                moneySafe(item.montoPagado),
                item.metodoPago || 'Sin pago',
                item.referenciaBanco || item.numeroRecibo || 'Sin referencia',
                <div className="table-actions">
                  <button className="icon-button secondary" onClick={() => selectPayment(item, 'pago')}><CheckCircle2 size={16} />Registrar pago</button>
                  <button className="icon-button secondary" onClick={() => selectPayment(item, 'comprobante')}><FileUp size={16} />Comprobante</button>
                  <button className="icon-button secondary" onClick={() => selectPayment(item, 'docs')}><Eye size={16} />Ver docs</button>
                  {item.estado !== 'PAGADA' && (
                    <button className="icon-button secondary" onClick={() => setEditingFecha({ id: item.id, fecha: item.fechaVencimiento.slice(0, 10) })}><Pencil size={16} />Fecha</button>
                  )}
                </div>,
              ])}
            />
            {data.items.length === 0 && <div className="empty">No hay cuotas para el filtro seleccionado.</div>}
          </article>}
          {editingFecha && (
            <article className="panel mt-panel">
              <PanelTitle title="Editar fecha de vencimiento" subtitle="Solo aplica si la cuota no ha sido pagada." />
              <div className="form-grid compact-form">
                <label className="field">
                  <span>Nueva fecha de vencimiento</span>
                  <input type="date" value={editingFecha.fecha} onChange={(e) => setEditingFecha({ ...editingFecha, fecha: e.target.value })} />
                </label>
                <div className="form-actions">
                  <button className="primary-button" onClick={() => void guardarFecha()}>Guardar fecha</button>
                  <button className="secondary-button" onClick={() => setEditingFecha(null)}>Cancelar</button>
                </div>
              </div>
            </article>
          )}
          {selectedPayment && (
            <article className="panel mt-panel" ref={paymentPanelRef}>
              <PanelTitle
                title={selectedAction === 'pago' ? 'Registrar pago' : selectedAction === 'comprobante' ? 'Subir comprobante' : 'Documentos del pago'}
                subtitle={`${selectedPayment.cliente} / cuota ${selectedPayment.numeroCuota}`}
              />
              <div className="info-grid compact">
                <div className="info-item"><span>Monto cuota</span><strong>{moneySafe(selectedPayment.monto)}</strong></div>
                <div className="info-item"><span>Pagado</span><strong>{moneySafe(selectedPayment.montoPagado)}</strong></div>
                <div className="info-item"><span>Saldo</span><strong>{moneySafe(saldoSeleccionado)}</strong></div>
                <div className="info-item"><span>Estado</span><strong>{statusLabel(selectedPayment.estado)}</strong></div>
              </div>
              <AccordionSection title="Datos del pago" subtitle="Registra abonos, recibos y referencias bancarias." defaultOpen={selectedAction === 'pago'}>
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
              </AccordionSection>
              <AccordionSection title="Comprobantes y documentos" subtitle="Archivos asociados a esta cuota de pago." defaultOpen={selectedAction !== 'pago'}>
                <DocumentosPanel entidadTipo="PAGO" entidadId={selectedPayment.id} compact />
              </AccordionSection>
            </article>
          )}
        </>
      )}
    </>
  )
}
