import { useEffect, useState } from 'react'
import { getSmtpConfig, probarSmtp, updateSmtpConfig, type SmtpConfig } from '../api/configuracionApi'
import { getCorreoRevision, getReclamoCorreoConfig, getReclamoWorkerStatus, processReclamosNow, recoveryReclamos, saveReclamoCorreoConfig, testReclamoCorreoConnection } from '../api/reclamosConfigApi'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { useAutoRefresh } from '../hooks/useAutoRefresh'
import type { CorreoRevisionItem, ReclamoCorreoConfig, ReclamoWorkerEstado } from '../types/reclamosConfig'

export function CorreoConfigView() {
  const [imapConfig, setImapConfig] = useState<ReclamoCorreoConfig | null>(null)
  const [smtpConfig, setSmtpConfig] = useState<SmtpConfig | null>(null)
  const [status, setStatus] = useState<ReclamoWorkerEstado | null>(null)
  const [bandejaEstado, setBandejaEstado] = useState('TODOS')
  const [bandeja, setBandeja] = useState<CorreoRevisionItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busyAction, setBusyAction] = useState<string | null>(null)

  async function load() {
    setError(null)
    const [imap, smtp, workerStatus, revision] = await Promise.all([getReclamoCorreoConfig(), getSmtpConfig(), getReclamoWorkerStatus(), getCorreoRevision(bandejaEstado)])
    setImapConfig(imap)
    setSmtpConfig(smtp)
    setStatus(workerStatus)
    setBandeja(revision.items)
  }

  async function loadWorkerActivity() {
    const [workerStatus, revision] = await Promise.all([getReclamoWorkerStatus(), getCorreoRevision(bandejaEstado)])
    setStatus(workerStatus)
    setBandeja(revision.items)
  }

  useEffect(() => {
    let alive = true
    Promise.all([getReclamoCorreoConfig(), getSmtpConfig(), getReclamoWorkerStatus(), getCorreoRevision(bandejaEstado)])
      .then(([imap, smtp, workerStatus, revision]) => {
        if (!alive) return
        setImapConfig(imap)
        setSmtpConfig(smtp)
        setStatus(workerStatus)
        setBandeja(revision.items)
      })
      .catch((err) => {
        if (!alive) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion de correo.')
      })
    return () => { alive = false }
  }, [bandejaEstado])

  async function saveImapConfig() {
    if (!imapConfig) return
    await runAction('save-imap', async () => {
      await saveReclamoCorreoConfig(imapConfig)
      setMessage('Correo de lectura guardado.')
      await load()
    })
  }

  async function saveSmtpConfig() {
    if (!smtpConfig) return
    await runAction('save-smtp', async () => {
      await updateSmtpConfig(smtpConfig)
      setMessage('Correo de produccion guardado.')
      await load()
    })
  }

  async function runAction(action: string, callback: () => Promise<void>) {
    if (busyAction) return
    setBusyAction(action)
    setError(null)
    setMessage(null)
    try {
      await callback()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo completar la accion.')
    } finally {
      setBusyAction(null)
    }
  }

  async function testImap() {
    await runAction('test-imap', async () => {
      await testReclamoCorreoConnection()
      setMessage('Conexion IMAP exitosa.')
    })
  }

  async function testSmtp() {
    await runAction('test-smtp', async () => {
      await probarSmtp()
      setMessage('Conexion SMTP exitosa.')
    })
  }

  async function processNow() {
    await runAction('process-now', async () => {
      await processReclamosNow()
      await load()
      setMessage('Procesamiento ejecutado.')
    })
  }

  async function recoverNow() {
    await runAction('recover-now', async () => {
      await recoveryReclamos(72)
      await load()
      setMessage('Modo recuperacion ejecutado.')
    })
  }

  useAutoRefresh(async () => {
    if (busyAction) return
    await loadWorkerActivity()
  }, 10000, !busyAction)

  if (!imapConfig || !smtpConfig) return <LoadingCard text="Cargando configuracion de correo..." />

  return (
    <>
      <PageHeader eyebrow="Configuracion" title="Correo" description="Administra las cuentas de lectura IMAP y envio SMTP del sistema." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Lectura IMAP" subtitle="Cuenta usada para leer correos entrantes y crear reclamos." />
          <div className="form-grid">
            <label className="field"><span>Email habilitado</span><select value={String(imapConfig.emailEnabled)} onChange={(e) => setImapConfig({ ...imapConfig, emailEnabled: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <label className="field"><span>Worker habilitado</span><select value={String(imapConfig.workerEnabled)} onChange={(e) => setImapConfig({ ...imapConfig, workerEnabled: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Mailbox" value={imapConfig.mailbox} onChange={(v) => setImapConfig({ ...imapConfig, mailbox: v })} />
            <label className="field"><span>Marcar leidos</span><select value={String(imapConfig.markAsRead)} onChange={(e) => setImapConfig({ ...imapConfig, markAsRead: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Lookback horas" type="number" value={String(imapConfig.lookbackHours)} onChange={(v) => setImapConfig({ ...imapConfig, lookbackHours: Number(v) || 24 })} />
            <Field label="Host IMAP" value={imapConfig.host} onChange={(v) => setImapConfig({ ...imapConfig, host: v })} />
            <Field label="Puerto IMAP" type="number" value={String(imapConfig.port)} onChange={(v) => setImapConfig({ ...imapConfig, port: Number(v) || 993 })} />
            <label className="field"><span>SSL</span><select value={String(imapConfig.useSsl)} onChange={(e) => setImapConfig({ ...imapConfig, useSsl: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Correo de lectura" value={imapConfig.username} onChange={(v) => setImapConfig({ ...imapConfig, username: v })} />
            <Field label={`Password IMAP${imapConfig.passwordMasked ? ' configurado' : ''}`} type="password" value={imapConfig.password || ''} onChange={(v) => setImapConfig({ ...imapConfig, password: v })} />
            <div className="form-actions">
              <button className="primary-button" disabled={!!busyAction} onClick={() => void saveImapConfig()}>{busyAction === 'save-imap' ? 'Guardando...' : 'Guardar lectura'}</button>
              <button className="icon-button secondary" disabled={!!busyAction} onClick={() => void testImap()}>{busyAction === 'test-imap' ? 'Probando...' : 'Probar IMAP'}</button>
              <button className="icon-button secondary" disabled={!!busyAction} onClick={() => void processNow()}>{busyAction === 'process-now' ? 'Procesando...' : 'Procesar ahora'}</button>
              <button className="icon-button secondary" disabled={!!busyAction} onClick={() => void recoverNow()}>{busyAction === 'recover-now' ? 'Procesando...' : 'Modo recuperacion 72h'}</button>
            </div>
          </div>
          {status && (
            <div className="inline-alert info">
              Ultima ejecucion: {status.ultimaEjecucionUtc || 'N/A'} | Leidos: {status.correosEncontrados} | Reclamos validos: {status.reclamosValidos} | Creados: {status.correosProcesados} | Ignorados: {status.correosIgnorados} | Duplicados: {status.correosDuplicados} | Errores: {status.correosConError}
              {status.ultimoError ? ` | Error general: ${status.ultimoError}` : ''}
            </div>
          )}
        </article>

        <article className="panel">
          <PanelTitle title="Envio SMTP" subtitle="Cuenta de produccion usada para enviar documentos a aseguradoras." />
          <div className="form-grid">
            <label className="field"><span>SMTP habilitado</span><select value={String(smtpConfig.enabled)} onChange={(e) => setSmtpConfig({ ...smtpConfig, enabled: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Host SMTP" value={smtpConfig.host} onChange={(v) => setSmtpConfig({ ...smtpConfig, host: v })} />
            <Field label="Puerto SMTP" type="number" value={String(smtpConfig.port)} onChange={(v) => setSmtpConfig({ ...smtpConfig, port: Number(v) || 587 })} />
            <label className="field"><span>SSL directo</span><select value={String(smtpConfig.useSsl)} onChange={(e) => setSmtpConfig({ ...smtpConfig, useSsl: e.target.value === 'true' })}><option value="false">No</option><option value="true">Si</option></select></label>
            <Field label="Correo de produccion" value={smtpConfig.username} onChange={(v) => setSmtpConfig({ ...smtpConfig, username: v, fromAddress: smtpConfig.fromAddress || v })} />
            <Field label={`Password SMTP${smtpConfig.passwordMasked ? ' configurado' : ''}`} type="password" value={smtpConfig.password || ''} onChange={(v) => setSmtpConfig({ ...smtpConfig, password: v })} />
            <Field label="Remitente" value={smtpConfig.fromAddress} onChange={(v) => setSmtpConfig({ ...smtpConfig, fromAddress: v })} />
            <Field label="Nombre remitente" value={smtpConfig.fromName} onChange={(v) => setSmtpConfig({ ...smtpConfig, fromName: v })} />
            <div className="form-actions">
              <button className="primary-button" disabled={!!busyAction} onClick={() => void saveSmtpConfig()}>{busyAction === 'save-smtp' ? 'Guardando...' : 'Guardar produccion'}</button>
              <button className="icon-button secondary" disabled={!!busyAction} onClick={() => void testSmtp()}>{busyAction === 'test-smtp' ? 'Probando...' : 'Probar SMTP'}</button>
            </div>
          </div>
        </article>
      </section>

      {status?.detalles && status.detalles.length > 0 && (
        <article className="panel mt-panel">
          <PanelTitle title="Ultimos correos evaluados" subtitle="Resultado exacto del worker para entender que se creo, ignoro o rechazo." />
          <div className="result-panel">
            {status.detalles.map((item, index) => (
              <div key={`${item.messageId}-${index}`} className="renewal-row">
                <strong>{item.estado}{item.reclamoId ? ` #${item.reclamoId}` : ''}</strong>
                <span>{item.subject || 'Sin asunto'} - {item.motivo}</span>
              </div>
            ))}
          </div>
        </article>
      )}

      <article className="panel mt-panel">
        <div className="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-2">
          <PanelTitle title="Bandeja de correos revisados" subtitle="Historial persistente de correos procesados, ignorados, duplicados y con error." />
          <label className="field compact-field">
            <span>Estado</span>
            <select value={bandejaEstado} onChange={(event) => setBandejaEstado(event.target.value)}>
              <option value="TODOS">Todos</option>
              <option value="PROCESADO">Procesados</option>
              <option value="IGNORADO">Ignorados</option>
              <option value="DUPLICADO">Duplicados</option>
              <option value="ERROR">Errores</option>
            </select>
          </label>
        </div>
        <div className="result-panel">
          {bandeja.length === 0 && <div className="inline-alert info">Aun no hay correos en esta bandeja.</div>}
          {bandeja.map((item) => (
            <div key={item.id} className="renewal-row correo-review-row">
              <strong>{item.estado}{item.reclamoId ? ` #${item.reclamoId}` : ''}</strong>
              <span>{item.subject || 'Sin asunto'} - {item.motivo}</span>
              {item.bodyPreview && <small>{item.bodyPreview}</small>}
            </div>
          ))}
        </div>
      </article>
    </>
  )
}
