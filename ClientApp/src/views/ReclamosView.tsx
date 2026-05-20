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
  getRespuestasAseguradora,
  getSiguienteReclamoPendiente,
  marcarDocumentosCompletosReclamo,
  registrarRespuestaAseguradora,
  solicitarDocumentosReclamo,
  updateSeguimientoReclamo,
  updateSeguimientoOperativoReclamo,
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
import type { ClaimChecklistItem, ClaimInsuranceResponse, ClaimItem, ClaimsResponse } from '../types/reclamos'
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
  const [respuestasHistorial, setRespuestasHistorial] = useState<ClaimInsuranceResponse[]>([])
  const [insurerFormDirty, setInsurerFormDirty] = useState(false)
  const [datosBasicos, setDatosBasicos] = useState({ poliza: '', reclamo: '', placa: '', celular: '', ciudad: '' })
  const [datosBasicosDirty, setDatosBasicosDirty] = useState(false)
  const [operativo, setOperativo] = useState({
    montoDeducible: '',
    montoRsa: '',
    estadoDeducible: 'NO_APLICA',
    estadoRsa: 'NO_APLICA',
    estadoCotizaciones: 'PENDIENTE_VISITA_TALLERES',
    cotizacionesNota: '',
    casoEspecial: false,
    casoEspecialNota: '',
  })
  const [operativoDirty, setOperativoDirty] = useState(false)

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
    setRespuestaAseguradora('')
    setAseguradoraAprobado(false)
    setInsurerFormDirty(false)
    setDatosBasicos({
      poliza: selected.poliza || '',
      reclamo: selected.reclamo || selected.numeroReclamo || '',
      placa: selected.placa || '',
      celular: selected.celular || '',
      ciudad: selected.ciudadDetectada || '',
    })
    setDatosBasicosDirty(false)
    setOperativo({
      montoDeducible: selected.montoDeducible?.toString() || '',
      montoRsa: selected.montoRsa?.toString() || '',
      estadoDeducible: selected.estadoDeducible || 'NO_APLICA',
      estadoRsa: selected.estadoRsa || 'NO_APLICA',
      estadoCotizaciones: selected.estadoCotizaciones || 'PENDIENTE_VISITA_TALLERES',
      cotizacionesNota: selected.cotizacionesNota || '',
      casoEspecial: !!selected.casoEspecial,
      casoEspecialNota: selected.casoEspecialNota || '',
    })
    setOperativoDirty(false)
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
    getRespuestasAseguradora(selected.id)
      .then((res) => setRespuestasHistorial(res.items))
      .catch(() => setRespuestasHistorial([]))
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
    const historial = await getRespuestasAseguradora(selected.id)
    setRespuestasHistorial(historial.items)
  }

  useAutoRefresh(async () => {
    if (actionBusy) return
    if (selected) await refreshSelected()
    else await load()
  }, 10000, !actionBusy && !insurerFormDirty && !datosBasicosDirty && !operativoDirty)

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
    if (!respuestaAseguradora.trim()) {
      notify('Pega o escribe la respuesta antes de guardar.', 'error')
      return
    }
    await runAction(async () => {
      await registrarRespuestaAseguradora(selected.id, respuestaAseguradora, aseguradoraAprobado)
      setRespuestaAseguradora('')
      setAseguradoraAprobado(false)
      setInsurerFormDirty(false)
    }, aseguradoraAprobado ? 'Respuesta aprobada. Se habilitaron comprobantes de pago de RSA/deducible si aplican.' : 'Respuesta de aseguradora guardada.')
  }

  async function saveSeguimiento(value: string) {
    if (!selected) return
    await runAction(async () => {
      await updateSeguimientoReclamo(selected.id, value)
    }, 'Estado de seguimiento actualizado.')
  }

  async function saveOperativo() {
    if (!selected) return
    const toNumber = (value: string) => value.trim() ? Number(value) : null
    await runAction(async () => {
      await updateSeguimientoOperativoReclamo(selected.id, {
        montoDeducible: toNumber(operativo.montoDeducible),
        montoRsa: toNumber(operativo.montoRsa),
        estadoDeducible: operativo.estadoDeducible,
        estadoRsa: operativo.estadoRsa,
        estadoCotizaciones: operativo.estadoCotizaciones,
        cotizacionesNota: operativo.cotizacionesNota,
        casoEspecial: operativo.casoEspecial,
        casoEspecialNota: operativo.casoEspecialNota,
      })
      setOperativoDirty(false)
    }, 'Seguimiento operativo guardado.')
  }

  async function goNextPending() {
    const response = await getSiguienteReclamoPendiente(selected?.id)
    if (response.item) {
      setSelected(response.item)
      setActionMessage('')
      notify('Siguiente reclamo cargado.', 'success')
    } else {
      notify('No hay otro reclamo pendiente en la cola.', 'success')
    }
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
      <Toolbar buscar={buscar} estado={estado} estados={['TODOS', 'NO_REVISADO', 'EN_REVISION', 'ESPERANDO_CLIENTE', 'ESPERANDO_ASEGURADORA', 'LISTO', 'PENDIENTES_PAGO', 'PAGO_DEDUCIBLE_PENDIENTE', 'PAGO_RSA_PENDIENTE', 'PENDIENTE_MONTO', 'PENDIENTE_COTIZACIONES', 'CASO_ESPECIAL', 'SIN_RESPUESTA_ASEGURADORA', 'CON_RESPUESTA_ASEGURADORA', 'SIN_TELEFONO', 'SIN_POLIZA', 'EN_SEGUIMIENTO', 'DOCUMENTOS_PENDIENTES', 'COMPLETO', 'ERROR']} onBuscar={setBuscar} onEstado={setEstado} onSubmit={load} />
      {loading && <LoadingCard text="Cargando reclamos..." />}
      {error && <ErrorCard text={error} />}
      {data && (
        <section className="claims-layout">
          <div className="action-row page-actions">
            <button className="icon-button secondary" type="button" onClick={() => setHideList((value) => !value)}>
              {hideList ? 'Mostrar reclamos' : 'Ocultar reclamos'}
            </button>
            <button className="primary-button" type="button" disabled={actionBusy} onClick={() => void goNextPending()}>
              Siguiente pendiente
            </button>
          </div>
          {!hideList && <article className="panel">
            <PanelTitle title={`${data.total} reclamos`} subtitle="Selecciona un reclamo para gestionar documentos." />
            <DataTable
              headers={['Reclamo', 'Cliente', 'Poliza', 'Pendientes', 'Seguimiento']}
              rows={data.items.map((item) => [
                <button className="link-button" onClick={() => { setActionMessage(''); setSelected(item) }}><FileText size={16} />{item.reclamo || `#${item.id}`}</button>,
                <CellTitle title={item.conductor || item.asegurado || 'Sin cliente'} subtitle={item.celular || item.placa || 'Sin detalle'} />,
                item.poliza || 'Sin poliza',
                `${item.documentosPendientes ?? 0}${item.pagosPendientes ? ` / pagos ${item.pagosPendientes}` : ''}`,
                <StatusPill text={statusLabel(item.estadoSeguimiento || 'NO_REVISADO')} tone={stateTone(item.estadoSeguimiento || 'NO_REVISADO')} />,
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
                  <div className="info-item"><span>Seguimiento</span><strong>{statusLabel(selected.estadoSeguimiento || 'NO_REVISADO')}</strong></div>
                  <div className="info-item"><span>Ultima revision</span><strong>{selected.fechaUltimaRevision ? dateFmt.format(new Date(selected.fechaUltimaRevision)) : 'Sin revisar'}</strong></div>
                  <div className="info-item"><span>Deducible</span><strong>{statusLabel(selected.estadoDeducible || 'NO_APLICA')}{selected.montoDeducible ? ` - L ${selected.montoDeducible}` : ''}</strong></div>
                  <div className="info-item"><span>RSA</span><strong>{statusLabel(selected.estadoRsa || 'NO_APLICA')}{selected.montoRsa ? ` - L ${selected.montoRsa}` : ''}</strong></div>
                  <div className="info-item"><span>Cotizaciones</span><strong>{statusLabel(selected.estadoCotizaciones || 'PENDIENTE_VISITA_TALLERES')}</strong></div>
                  <div className="info-item"><span>Especial</span><strong>{selected.casoEspecial ? 'Si' : 'No'}</strong></div>
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
                <AccordionSection title="Seguimiento operativo" subtitle="Montos, pagos finales, cotizaciones y casos especiales.">
                  <div className="insurer-box">
                    <label className="field compact-field">
                      <span>Monto deducible (LPS)</span>
                      <input type="number" min="0" step="0.01" value={operativo.montoDeducible} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, montoDeducible: event.target.value }) }} placeholder="Pendiente" />
                    </label>
                    <label className="field compact-field">
                      <span>Estado deducible</span>
                      <select value={operativo.estadoDeducible} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, estadoDeducible: event.target.value }) }}>
                        {['NO_APLICA', 'PENDIENTE_MONTO', 'PENDIENTE_PAGO', 'PAGADO_CLIENTE', 'COMPROBANTE_ENVIADO', 'CONFIRMADO_ASEGURADORA'].map((item) => (
                          <option key={item} value={item}>{statusLabel(item)}</option>
                        ))}
                      </select>
                    </label>
                    <label className="field compact-field">
                      <span>Monto RSA (LPS)</span>
                      <input type="number" min="0" step="0.01" value={operativo.montoRsa} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, montoRsa: event.target.value }) }} placeholder="Pendiente" />
                    </label>
                    <label className="field compact-field">
                      <span>Estado RSA</span>
                      <select value={operativo.estadoRsa} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, estadoRsa: event.target.value }) }}>
                        {['NO_APLICA', 'PENDIENTE_MONTO', 'PENDIENTE_PAGO', 'PAGADO_CLIENTE', 'COMPROBANTE_ENVIADO', 'CONFIRMADO_ASEGURADORA'].map((item) => (
                          <option key={item} value={item}>{statusLabel(item)}</option>
                        ))}
                      </select>
                    </label>
                    <label className="field compact-field">
                      <span>Cotizaciones</span>
                      <select value={operativo.estadoCotizaciones} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, estadoCotizaciones: event.target.value }) }}>
                        {['PENDIENTE_VISITA_TALLERES', 'CLIENTE_INDICO_QUE_FUE', 'TALLER_INDICO_QUE_ENVIO', 'ASEGURADORA_CONFIRMADAS', 'NO_APLICA'].map((item) => (
                          <option key={item} value={item}>{statusLabel(item)}</option>
                        ))}
                      </select>
                    </label>
                    <label className="wide-field">
                      <span>Nota de cotizaciones</span>
                      <textarea rows={2} value={operativo.cotizacionesNota} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, cotizacionesNota: event.target.value }) }} placeholder="Ej: cliente indico que ya fue a dos talleres; pendiente confirmar recibido por aseguradora." />
                    </label>
                    <label className="check-field">
                      <input type="checkbox" checked={operativo.casoEspecial} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, casoEspecial: event.target.checked }) }} />
                      Caso especial
                    </label>
                    <label className="wide-field">
                      <span>Nota caso especial</span>
                      <textarea rows={2} value={operativo.casoEspecialNota} onChange={(event) => { setOperativoDirty(true); setOperativo({ ...operativo, casoEspecialNota: event.target.value }) }} placeholder="Deja aqui la razon operativa para darle seguimiento aparte." />
                    </label>
                    <button className="icon-button secondary" disabled={actionBusy || !operativoDirty} onClick={() => void saveOperativo()}>
                      Guardar seguimiento
                    </button>
                  </div>
                </AccordionSection>
                <div className="action-row reclamo-actions">
                  <select value={selected.estadoSeguimiento || 'NO_REVISADO'} disabled={actionBusy} onChange={(event) => void saveSeguimiento(event.target.value)}>
                    {['NO_REVISADO', 'EN_REVISION', 'ESPERANDO_CLIENTE', 'ESPERANDO_ASEGURADORA', 'LISTO'].map((item) => (
                      <option key={item} value={item}>{statusLabel(item)}</option>
                    ))}
                  </select>
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
                      <span>Nueva respuesta o nota</span>
                      <textarea value={respuestaAseguradora} rows={5} onChange={(event) => { setInsurerFormDirty(true); setRespuestaAseguradora(event.target.value) }} placeholder="Pega aqui una respuesta nueva. No reemplaza las anteriores." />
                    </label>
                    <label className="check-field">
                      <input type="checkbox" checked={aseguradoraAprobado} onChange={(event) => { setInsurerFormDirty(true); setAseguradoraAprobado(event.target.checked) }} />
                      Expediente aprobado por aseguradora
                    </label>
                    <button className="icon-button" disabled={actionBusy} onClick={() => void saveInsurerResponse()}>
                      Guardar respuesta
                    </button>
                  </div>
                  <div className="mail-review-list compact" style={{ marginTop: 12 }}>
                    {respuestasHistorial.length === 0 && <div className="empty">Sin respuestas registradas todavia.</div>}
                    {respuestasHistorial.map((item) => (
                      <details className="mail-review-card info" key={item.id}>
                        <summary>
                          <div className="mail-review-main">
                            <StatusPill text={item.origen} tone={item.origen === 'CORREO' ? 'info' : 'slate'} />
                            <div className="mail-review-copy">
                              <strong>{item.asunto || (item.aprobado ? 'Respuesta aprobada' : 'Respuesta registrada')}</strong>
                              <span>{new Date(item.creadoEn).toLocaleString('es-HN')} {item.remitente ? `- ${item.remitente}` : ''}</span>
                            </div>
                          </div>
                        </summary>
                        <div className="mail-review-detail">
                          <div style={{ whiteSpace: 'pre-wrap' }}>{item.respuesta}</div>
                          {(item.requiereDeducible || item.requiereRsa) && (
                            <div>
                              <strong>Pagos detectados:</strong>
                              {item.requiereDeducible ? ` Deducible${item.montoDeducible ? ` L ${item.montoDeducible}` : ' sin monto'}.` : ''}
                              {item.requiereRsa ? ` RSA${item.montoRsa ? ` L ${item.montoRsa}` : ' sin monto'}.` : ''}
                            </div>
                          )}
                          {item.acciones && <div><strong>Acciones:</strong> {item.acciones}</div>}
                        </div>
                      </details>
                    ))}
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
