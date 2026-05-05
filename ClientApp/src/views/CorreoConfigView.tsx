import { useEffect, useState } from 'react'
import { getSmtpConfig, probarSmtp, updateSmtpConfig, type SmtpConfig } from '../api/configuracionApi'
import { getReclamoCorreoConfig, getReclamoWorkerStatus, processReclamosNow, recoveryReclamos, saveReclamoCorreoConfig, testReclamoCorreoConnection } from '../api/reclamosConfigApi'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import type { ReclamoCorreoConfig, ReclamoWorkerEstado } from '../types/reclamosConfig'

export function CorreoConfigView() {
  const [imapConfig, setImapConfig] = useState<ReclamoCorreoConfig | null>(null)
  const [smtpConfig, setSmtpConfig] = useState<SmtpConfig | null>(null)
  const [status, setStatus] = useState<ReclamoWorkerEstado | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    setError(null)
    const [imap, smtp, workerStatus] = await Promise.all([getReclamoCorreoConfig(), getSmtpConfig(), getReclamoWorkerStatus()])
    setImapConfig(imap)
    setSmtpConfig(smtp)
    setStatus(workerStatus)
  }

  useEffect(() => {
    let alive = true
    Promise.all([getReclamoCorreoConfig(), getSmtpConfig(), getReclamoWorkerStatus()])
      .then(([imap, smtp, workerStatus]) => {
        if (!alive) return
        setImapConfig(imap)
        setSmtpConfig(smtp)
        setStatus(workerStatus)
      })
      .catch((err) => {
        if (!alive) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion de correo.')
      })
    return () => { alive = false }
  }, [])

  async function saveImapConfig() {
    if (!imapConfig) return
    await saveReclamoCorreoConfig(imapConfig)
    setMessage('Correo de lectura guardado.')
    await load()
  }

  async function saveSmtpConfig() {
    if (!smtpConfig) return
    await updateSmtpConfig(smtpConfig)
    setMessage('Correo de produccion guardado.')
    await load()
  }

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
              <button className="primary-button" onClick={() => void saveImapConfig()}>Guardar lectura</button>
              <button className="icon-button secondary" onClick={() => void testReclamoCorreoConnection().then(() => setMessage('Conexion IMAP exitosa.')).catch((e) => setError(e.message))}>Probar IMAP</button>
              <button className="icon-button secondary" onClick={() => void processReclamosNow().then(() => load())}>Procesar ahora</button>
              <button className="icon-button secondary" onClick={() => void recoveryReclamos(72).then(() => load())}>Modo recuperacion 72h</button>
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
              <button className="primary-button" onClick={() => void saveSmtpConfig()}>Guardar produccion</button>
              <button className="icon-button secondary" onClick={() => void probarSmtp().then(() => setMessage('Conexion SMTP exitosa.')).catch((e) => setError(e.message))}>Probar SMTP</button>
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
    </>
  )
}
