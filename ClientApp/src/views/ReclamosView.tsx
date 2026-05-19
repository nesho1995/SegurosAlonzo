import { useEffect, useState } from 'react'
import { CheckCircle2, FileText, Send } from 'lucide-react'
import {
  aceptarDocumentoConExcepcion,
  enviarDocumentosAseguradora,
  enviarRecordatorioReclamo,
  enviarWhatsAppReclamo,
  getReclamoChecklist,
  getReclamoDocumentosPendientes,
  getReclamos,
  marcarDocumentosCompletosReclamo,
  registrarRespuestaAseguradora,
  solicitarDocumentosReclamo,
  updateCorreosAseguradora,
  updateDatosBasicosReclamo,
  updateReclamoDocumento,
  type ClaimPendingDocument,
} from '../api/reclamosApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { DocumentosPanel } from '../components/DocumentosPanel'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle, Toolbar } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { AccordionSection } from '../components/AccordionSection'
import { notify } from '../components/ToastHost'
import { useAutoRefresh } from '../hooks/useAutoRefresh'
import type { ClaimChecklistItem, ClaimItem, ClaimsResponse } from '../types/reclamos'
import { compactMeta, dateFmt } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'
import { reclamoDocumentLabel } from '../utils/reclamos'

export function ReclamosView() {
  const [data, setData] = useState<ClaimsResponse | null>(null)
  const [selected, setSelected] = useState<ClaimItem | null>(null)
  const [estado, setEstado] = useState('TODOS')
  const [buscar, setBuscar] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [checklist, setChecklist] = useState<ClaimChecklistItem[]>([])
  const [checklistPendientes, setChecklistPendientes] = useState(0)
  const [documentosPendientes, setDocumentosPendientes] = useState<ClaimPendingDocument[]>([])
  const [actionMessage, setActionMessage] = useState('')
  const [actionBusy, setActionBusy] = useState(false)
  const [correoAseguradora, setCorreoAseguradora] = useState('')
  const [correoCopia, setCorreoCopia] = useState('')
  const [respuestaAseguradora, setRespuestaAseguradora] = useState('')
  const [aseguradoraAprobado, setAseguradoraAprobado] = useState(false)
  const [insurerFormDirty, setInsurerFormDirty] = useState(false)
  const [datosBasicos, setDatosBasicos] = useState({ poliza: '', reclamo: '', placa: '', celular: '', ciudad: '' })
  const [datosBasicosDirty, setDatosBasicosDirty] = useState(false)

  async function aceptarExcepcionDocumento(doc: ClaimPendingDocument) {
    if (!selected) return
    const observacion = window.prompt(`Motivo para aceptar ${reclamoDocumentLabel(doc.documento)} con ${doc.adjuntosRecibidos} de ${doc.cantidadRequerida} adjuntos:`)
    if (!observacion?.trim()) return
    await runAction(
      () => aceptarDocumentoConExcepcion(selected.id, doc.id, observacion.trim()),
      'Documento aceptado con excepcion.'
    )
  }
  const [hideList, setHideList] = useState(false)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ estado })
      if (buscar.trim()) query.set('buscar', buscar.trim())
      const response = await getReclamos(query)
      setData(response)
      setSelected((current) => current ?? response.items[0] ?? null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ estado })
    if (buscar.trim()) query.set('buscar', buscar.trim())

    getReclamos(query)
      .then((response) => {
        if (!alive) return
        setData(response)
        setSelected((current) => current ?? response.items[0] ?? null)
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

  useEffect(() => {
    if (!selected) return
    setCorreoAseguradora(selected.correoAseguradoraPrincipal || '')
    setCorreoCopia(selected.correoAseguradoraCopia || '')
    setRespuestaAseguradora(selected.respuestaAseguradora || '')
    setAseguradoraAprobado(Boolean(selected.aseguradoraAprobado))
    setInsurerFormDirty(false)
    setDatosBasicos({
      poliza: selected.poliza || '',
      reclamo: selected.reclamo || selected.numeroReclamo || '',
      placa: selected.placa || '',
      celular: selected.celular || '',
      ciudad: selected.ciudadDetectada || '',
    })
    setDatosBasicosDirty(false)
    getReclamoChecklist(selected.id, selected.tipoReclamo)
      .then((res) => {
        setChecklist(res.requisitos)
        setChecklistPendientes(res.documentosPendientes)
      })
      .catch(() => {
        setChecklist([])
        setChecklistPendientes(0)
      })
    getReclamoDocumentosPendientes(selected.id)
      .then((res) => {
        setDocumentosPendientes(res.items)
        setChecklistPendientes(res.pendientes)
      })
      .catch(() => setDocumentosPendientes([]))
  }, [selected])

  async function refreshSelected() {
    if (!selected) return
    const docs = await getReclamoDocumentosPendientes(selected.id)
    setDocumentosPendientes(docs.items)
    setChecklistPendientes(docs.pendientes)
    const query = new URLSearchParams({ estado })
    if (buscar.trim()) query.set('buscar', buscar.trim())
    const response = await getReclamos(query)
    setData(response)
    setSelected(response.items.find((item) => item.id === selected.id) ?? selected)
  }

  useAutoRefresh(async () => {
    if (actionBusy) return
    if (selected) await refreshSelected()
    else await load()
  }, 10000, !actionBusy && !insurerFormDirty && !datosBasicosDirty)

  async function runAction(action: () => Promise<unknown>, success: string) {
    setActionBusy(true)
    setActionMessage('')
    setError(null)
    try {
      const result = await action()
      const maybeResponse = result as { ok?: boolean; response?: string } | null
      const message = maybeResponse?.ok === false ? maybeResponse.response || 'La accion no se completo.' : success
      setActionMessage(message)
      notify(message, maybeResponse?.ok === false ? 'error' : 'success')
      await refreshSelected()
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error inesperado.'
      setError(text)
      notify(text, 'error')
    } finally {
      setActionBusy(false)
    }
  }

  async function saveInsurerEmails() {
    if (!selected) return
    await runAction(async () => {
      await updateCorreosAseguradora(selected.id, correoAseguradora, correoCopia)
      setInsurerFormDirty(false)
    }, 'Correos de aseguradora guardados.')
  }

  async function saveDatosBasicos() {
    if (!selected) return
    await runAction(async () => {
      await updateDatosBasicosReclamo(selected.id, datosBasicos.poliza, datosBasicos.reclamo, datosBasicos.placa, datosBasicos.celular, datosBasicos.ciudad)
      setDatosBasicosDirty(false)
    }, 'Datos del reclamo guardados.')
  }

  async function saveInsurerResponse() {
    if (!selected) return
    await runAction(async () => {
      await registrarRespuestaAseguradora(selected.id, respuestaAseguradora, aseguradoraAprobado)
      setInsurerFormDirty(false)
    }, aseguradoraAprobado ? 'Respuesta aprobada. Se habilitaron comprobantes de pago de RSA/deducible si aplican.' : 'Respuesta de aseguradora guardada.')
  }

  function confirmCompleteAndSend() {
    if (!selected) return
    const pendientes = documentosPendientes.filter((doc) => !doc.recibido).length
    const message = pendientes === 0
      ? 'Este reclamo ya aparece con todos los documentos recibidos. Si continuas no se reenviara WhatsApp si ya estaba completo. Deseas revisar igualmente?'
      : `Se marcaran como recibidos ${pendientes} documento(s) pendiente(s) y se notificara al cliente. Deseas continuar?`
    if (!window.confirm(message)) return
    void runAction(() => marcarDocumentosCompletosReclamo(selected.id), 'Reclamo marcado con documentos completos.')
  }

  return (
    <div className="reclamos-page">
      <PageHeader eyebrow="Reclamos" title="Expedientes de reclamos" description="Consulta reclamos y adjunta documentos de respaldo sin tocar el flujo probado de WhatsApp." onRefresh={load} />
      <Toolbar buscar={buscar} estado={estado} estados={['TODOS', 'PENDIENTE', 'PENDIENTE_ENVIO', 'ENVIADO', 'ERROR', 'COMPLETO', 'EN_SEGUIMIENTO', 'DOCUMENTOS_PENDIENTES']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
      {loading && <LoadingCard text="Cargando reclamos..." />}
      {error && <ErrorCard text={error} />}
      {data && (
        <section className="claims-layout">
          <div className="action-row page-actions">
            <button className="icon-button secondary" type="button" onClick={() => setHideList((value) => !value)}>
              {hideList ? 'Mostrar reclamos' : 'Ocultar reclamos'}
            </button>
          </div>
          {!hideList && <article className="panel">
            <PanelTitle title={`${data.total} reclamos`} subtitle="Selecciona un reclamo para gestionar documentos." />
            <DataTable
              headers={['Reclamo', 'Cliente', 'Poliza', 'Fecha', 'Estado']}
              rows={data.items.map((item) => [
                <button className="link-button" onClick={() => { setActionMessage(''); setSelected(item) }}><FileText size={16} />{item.reclamo || `#${item.id}`}</button>,
                <CellTitle title={item.conductor || item.asegurado || 'Sin cliente'} subtitle={item.celular || item.placa || 'Sin detalle'} />,
                item.poliza || 'Sin poliza',
                item.fechaCreacion ? dateFmt.format(new Date(item.fechaCreacion)) : 'Sin fecha',
                <StatusPill text={statusLabel(item.estadoReclamo || item.estado)} tone={stateTone(item.estadoReclamo || item.estado)} />,
              ])}
            />
          </article>}
          <article className="panel">
            <PanelTitle title="Gestion del reclamo" subtitle={selected ? compactMeta([selected.reclamo, selected.conductor, selected.placa]) : 'Selecciona un reclamo.'} />
            {selected ? (
              <>
                {actionMessage && <div className="inline-alert success">{actionMessage}</div>}
                <div className="info-grid compact reclamo-info">
                  <div className="info-item"><span>Ciudad detectada</span><strong>{selected.ciudadDetectada || 'Sin detectar'}</strong></div>
                  <div className="info-item"><span>Taller</span><strong>{selected.tallerAsignadoId || selected.tallerSugeridoId ? `Asignado #${selected.tallerAsignadoId || selected.tallerSugeridoId}` : 'Sin taller'}</strong></div>
                  <div className="info-item"><span>Motivo sugerencia</span><strong>{selected.motivoSugerenciaTaller || 'Sin sugerencia'}</strong></div>
                  <div className="info-item"><span>WhatsApp</span><strong>{statusLabel(selected.estadoReclamo || selected.estado)}</strong></div>
                </div>
                {selected.descripcion && (
                  <div className="inline-alert info" style={{ whiteSpace: 'pre-wrap', fontSize: '0.85rem' }}>{selected.descripcion}</div>
                )}
                <AccordionSection title="Datos del reclamo" subtitle="Completa identificadores pendientes de la carga.">
                  <div className="insurer-box">
                    <label className="field compact-field">
                      <span>Poliza</span>
                      <input value={datosBasicos.poliza} onChange={(event) => { setDatosBasicosDirty(true); setDatosBasicos({ ...datosBasicos, poliza: event.target.value }) }} placeholder="Numero de poliza" />
                    </label>
                    <label className="field compact-field">
                      <span>Reclamo</span>
                      <input value={datosBasicos.reclamo} onChange={(event) => { setDatosBasicosDirty(true); setDatosBasicos({ ...datosBasicos, reclamo: event.target.value }) }} placeholder="SAS-0000-2026" />
                    </label>
                    <label className="field compact-field">
                      <span>Placa</span>
                      <input value={datosBasicos.placa} onChange={(event) => { setDatosBasicosDirty(true); setDatosBasicos({ ...datosBasicos, placa: event.target.value }) }} placeholder="ABC-1234" />
                    </label>
                    <label className="field compact-field">
                      <span>Celular</span>
                      <input value={datosBasicos.celular} onChange={(event) => { setDatosBasicosDirty(true); setDatosBasicos({ ...datosBasicos, celular: event.target.value }) }} placeholder="9999-9999" />
                    </label>
                    <label className="field compact-field">
                      <span>Ciudad</span>
                      <input value={datosBasicos.ciudad} onChange={(event) => { setDatosBasicosDirty(true); setDatosBasicos({ ...datosBasicos, ciudad: event.target.value }) }} placeholder="SAN PEDRO SULA" />
                    </label>
                    <button className="icon-button secondary" disabled={actionBusy || !datosBasicosDirty} onClick={() => void saveDatosBasicos()}>
                      Guardar datos
                    </button>
                  </div>
                </AccordionSection>
                <div className="action-row reclamo-actions">
                  <button className="icon-button secondary" disabled={actionBusy} onClick={() => void runAction(() => solicitarDocumentosReclamo(selected.id), 'Reclamo marcado con documentos pendientes.')}>
                    <FileText size={16} />Documentos pendientes
                  </button>
                  <button className="icon-button secondary" disabled={actionBusy} onClick={confirmCompleteAndSend}>
                    <CheckCircle2 size={16} />Documentos completos
                  </button>
                  <button className="icon-button" disabled={actionBusy} onClick={() => void runAction(() => enviarWhatsAppReclamo(selected.id), 'WhatsApp inicial enviado.')}>
                    <Send size={16} />Enviar inicial
                  </button>
                  <button className="icon-button" disabled={actionBusy} onClick={() => void runAction(() => enviarRecordatorioReclamo(selected.id), 'Recordatorio manual enviado.')}>
                    <Send size={16} />Recordatorio
                  </button>
                </div>
                <AccordionSection title="Documentos requeridos" subtitle={`${checklistPendientes} pendientes por revisar.`}>
                  <div className="legacy-documents">
                    {documentosPendientes.length === 0 ? (
                      <div className="empty">No hay requisitos pendientes registrados.</div>
                    ) : documentosPendientes.map((doc) => (
                      <div className="check-field" key={doc.id}>
                        <input
                          type="checkbox"
                          checked={doc.recibido}
                          disabled={actionBusy}
                          onChange={(event) => void runAction(() => updateReclamoDocumento(selected.id, doc.id, event.target.checked), 'Documento actualizado.')}
                        />
                        <span>
                          {reclamoDocumentLabel(doc.documento)}
                          {doc.cantidadRequerida > 1 && (
                            <small style={{ display: 'block', color: '#64748b', fontWeight: 500 }}>
                              Adjuntos: {doc.adjuntosRecibidos}/{doc.cantidadRequerida}
                              {doc.excepcionAceptada && doc.excepcionObservacion ? ` - Excepcion: ${doc.excepcionObservacion}` : ''}
                            </small>
                          )}
                        </span>
                        {doc.permiteExcepcion && !doc.recibido && doc.adjuntosRecibidos >= doc.minimoAceptable && (
                          <button
                            className="icon-button secondary"
                            type="button"
                            disabled={actionBusy}
                            onClick={() => void aceptarExcepcionDocumento(doc)}
                          >
                            Aceptar con excepcion
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                  <div className="inline-alert warning">Checklist: {checklist.length} requisitos / pendientes: {checklistPendientes}</div>
                  {checklist.length > 0 && (
                    <div className="mini-grid">
                      {checklist.map((req) => (
                        <StatusPill key={req.id} text={`${reclamoDocumentLabel(req.tipoDocumento)}${req.requerido ? ' *' : ''}`} tone={req.requerido ? 'warning' : 'info'} />
                      ))}
                    </div>
                  )}
                </AccordionSection>
                <AccordionSection title="Expediente digital" subtitle="Documentos asociados al reclamo.">
                  <DocumentosPanel entidadTipo="RECLAMO" entidadId={selected.id} />
                </AccordionSection>
                <AccordionSection title="Envio a aseguradora" subtitle="Comparte documentos adjuntos por correo.">
                  <div className="insurer-box">
                    <label className="field compact-field">
                      <span>Correo principal</span>
                      <input value={correoAseguradora} onChange={(event) => { setInsurerFormDirty(true); setCorreoAseguradora(event.target.value) }} placeholder="correo@aseguradora.com" />
                    </label>
                    <label className="field compact-field">
                      <span>Correo copia</span>
                      <input value={correoCopia} onChange={(event) => { setInsurerFormDirty(true); setCorreoCopia(event.target.value) }} placeholder="copia@correo.com" />
                    </label>
                    <button className="icon-button secondary" disabled={actionBusy} onClick={() => void saveInsurerEmails()}>
                      Guardar correos
                    </button>
                    <button className="icon-button" disabled={actionBusy} onClick={() => void runAction(() => enviarDocumentosAseguradora(selected.id, correoAseguradora, correoCopia), 'Documentos enviados a la aseguradora.')}>
                      <Send size={16} />Enviar adjuntos
                    </button>
                  </div>
                </AccordionSection>
                <AccordionSection title="Respuesta de aseguradora" subtitle="Registra si el expediente fue aceptado para pedir pagos de RSA/deducible o documentos adicionales.">
                  <div className="insurer-box">
                    <label className="wide-field">
                      <span>Correo o respuesta recibida</span>
                      <textarea value={respuestaAseguradora} rows={5} onChange={(event) => { setInsurerFormDirty(true); setRespuestaAseguradora(event.target.value) }} placeholder="Pega aqui la respuesta de la aseguradora." />
                    </label>
                    <label className="check-field">
                      <input type="checkbox" checked={aseguradoraAprobado} onChange={(event) => { setInsurerFormDirty(true); setAseguradoraAprobado(event.target.checked) }} />
                      Expediente aprobado por aseguradora
                    </label>
                    <button className="icon-button" disabled={actionBusy} onClick={() => void saveInsurerResponse()}>
                      Guardar respuesta
                    </button>
                  </div>
                </AccordionSection>
              </>
            ) : <div className="empty">Selecciona un reclamo para ver documentos.</div>}
          </article>
        </section>
      )}
    </div>
  )
}
