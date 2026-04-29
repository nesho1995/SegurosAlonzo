import { useEffect, useState } from 'react'
import { Field, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { ErrorCard } from '../components/ErrorAlert'
import { LoadingCard } from '../components/LoadingState'
import { getCorreoPatrones, getReclamoCorreoConfig, getReclamoWorkerStatus, processReclamosNow, recoveryReclamos, saveCorreoPatron, saveReclamoCorreoConfig, testCorreoPatrones, testReclamoCorreoConnection } from '../api/reclamosConfigApi'
import type { CorreoReclamoPatron, ProbarPatronesResult, ReclamoCorreoConfig, ReclamoWorkerEstado } from '../types/reclamosConfig'

const emptyPatron: CorreoReclamoPatron = {
  id: 0, nombre: '', activo: true, prioridad: 100, campoDestino: 'NumeroReclamo', fuente: 'SUBJECT_BODY', tipoRegla: 'REGEX', patron: '', grupoRegex: '', requerido: false, normalizarTexto: true
}

export function ReclamosConfiguracionView() {
  const [config, setConfig] = useState<ReclamoCorreoConfig | null>(null)
  const [status, setStatus] = useState<ReclamoWorkerEstado | null>(null)
  const [patrones, setPatrones] = useState<CorreoReclamoPatron[]>([])
  const [patronForm, setPatronForm] = useState<CorreoReclamoPatron>(emptyPatron)
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [result, setResult] = useState<ProbarPatronesResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    setError(null)
    const [cfg, st, pats] = await Promise.all([getReclamoCorreoConfig(), getReclamoWorkerStatus(), getCorreoPatrones()])
    setConfig(cfg)
    setStatus(st)
    setPatrones(pats.items)
  }

  useEffect(() => {
    let alive = true
    Promise.all([getReclamoCorreoConfig(), getReclamoWorkerStatus(), getCorreoPatrones()])
      .then(([cfg, st, pats]) => {
        if (!alive) return
        setConfig(cfg)
        setStatus(st)
        setPatrones(pats.items)
      })
      .catch((err) => {
        if (!alive) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion.')
      })
    return () => { alive = false }
  }, [])

  async function saveConfig() {
    if (!config) return
    await saveReclamoCorreoConfig(config)
    setMessage('Configuracion de correo guardada.')
  }

  async function savePattern() {
    await saveCorreoPatron(patronForm)
    setMessage('Patron guardado.')
    setPatronForm(emptyPatron)
    await load()
  }

  async function runTest() {
    setResult(await testCorreoPatrones(subject, body) as ProbarPatronesResult)
  }

  if (!config) return <LoadingCard text="Cargando configuracion de reclamos..." />

  return (
    <>
      <PageHeader eyebrow="Admin" title="Reclamos / Correos" description="Configura lectura de correo, recuperacion y patrones de extraccion sin tocar codigo." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Correo y worker" subtitle="Configura conexion, ventana de lectura y ejecuciones manuales." />
          <div className="form-grid">
            <label className="field"><span>Email habilitado</span><select value={String(config.emailEnabled)} onChange={(e) => setConfig({ ...config, emailEnabled: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <label className="field"><span>Worker habilitado</span><select value={String(config.workerEnabled)} onChange={(e) => setConfig({ ...config, workerEnabled: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Mailbox" value={config.mailbox} onChange={(v) => setConfig({ ...config, mailbox: v })} />
            <label className="field"><span>Marcar leidos</span><select value={String(config.markAsRead)} onChange={(e) => setConfig({ ...config, markAsRead: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Lookback horas" type="number" value={String(config.lookbackHours)} onChange={(v) => setConfig({ ...config, lookbackHours: Number(v) || 24 })} />
            <Field label="Host" value={config.host} onChange={(v) => setConfig({ ...config, host: v })} />
            <Field label="Port" type="number" value={String(config.port)} onChange={(v) => setConfig({ ...config, port: Number(v) || 993 })} />
            <label className="field"><span>SSL</span><select value={String(config.useSsl)} onChange={(e) => setConfig({ ...config, useSsl: e.target.value === 'true' })}><option value="true">Si</option><option value="false">No</option></select></label>
            <Field label="Usuario" value={config.username} onChange={(v) => setConfig({ ...config, username: v })} />
            <Field label="Password / AppPassword" value={config.password || ''} onChange={(v) => setConfig({ ...config, password: v })} />
            <div className="form-actions">
              <button className="primary-button" onClick={() => void saveConfig()}>Guardar</button>
              <button className="icon-button secondary" onClick={() => void testReclamoCorreoConnection().then(() => setMessage('Conexion exitosa.')).catch((e) => setError(e.message))}>Probar conexion</button>
              <button className="icon-button secondary" onClick={() => void processReclamosNow().then(() => load())}>Procesar ahora</button>
              <button className="icon-button secondary" onClick={() => void recoveryReclamos(72).then(() => load())}>Modo recuperacion 72h</button>
            </div>
          </div>
          {status && <div className="inline-alert info">Ultima ejecucion: {status.ultimaEjecucionUtc || 'N/A'} | Encontrados: {status.correosEncontrados} | Procesados: {status.correosProcesados} {status.ultimoError ? `| Error: ${status.ultimoError}` : ''}</div>}
        </article>

        <article className="panel">
          <PanelTitle title="Patrones de correo" subtitle="Varias reglas por campo con prioridad." />
          <div className="form-grid">
            <Field label="Nombre" value={patronForm.nombre} onChange={(v) => setPatronForm({ ...patronForm, nombre: v })} />
            <Field label="Campo destino" value={patronForm.campoDestino} onChange={(v) => setPatronForm({ ...patronForm, campoDestino: v })} />
            <Field label="Prioridad" type="number" value={String(patronForm.prioridad)} onChange={(v) => setPatronForm({ ...patronForm, prioridad: Number(v) || 100 })} />
            <label className="field"><span>Fuente</span><select value={patronForm.fuente} onChange={(e) => setPatronForm({ ...patronForm, fuente: e.target.value as CorreoReclamoPatron['fuente'] })}><option>SUBJECT</option><option>BODY</option><option>SUBJECT_BODY</option></select></label>
            <label className="field"><span>Tipo regla</span><select value={patronForm.tipoRegla} onChange={(e) => setPatronForm({ ...patronForm, tipoRegla: e.target.value as CorreoReclamoPatron['tipoRegla'] })}><option>REGEX</option><option>CONTIENE</option><option>EMPIEZA_CON</option><option>TERMINA_CON</option></select></label>
            <Field label="Grupo regex" value={patronForm.grupoRegex || ''} onChange={(v) => setPatronForm({ ...patronForm, grupoRegex: v })} />
            <label className="wide-field"><span>Patron</span><textarea value={patronForm.patron} onChange={(e) => setPatronForm({ ...patronForm, patron: e.target.value })} /></label>
            <div className="form-actions"><button className="primary-button" onClick={() => void savePattern()}>Guardar patron</button></div>
          </div>
          <div className="result-panel">
            {patrones.map((p) => <div key={p.id} className="renewal-row"><strong>{p.nombre}</strong><span>{p.campoDestino} / {p.fuente} / {p.tipoRegla} / prioridad {p.prioridad}</span></div>)}
          </div>
        </article>
      </section>

      <article className="panel mt-panel">
        <PanelTitle title="Probador de patrones" subtitle="Prueba asunto/cuerpo y revisa plantilla/campos detectados." />
        <div className="form-grid">
          <Field label="Subject de prueba" value={subject} onChange={setSubject} />
          <label className="wide-field"><span>Body de prueba</span><textarea value={body} onChange={(e) => setBody(e.target.value)} /></label>
          <div className="form-actions"><button className="primary-button" onClick={() => void runTest()}>Probar extraccion</button></div>
        </div>
        {result && (
          <div className="inline-alert info">
            Plantilla: {result.plantillaNombre || 'N/A'} | Cumple: {result.plantillaCumple ? 'SI' : 'NO'} | Faltantes: {result.camposFaltantes.join(', ') || 'Ninguno'}
          </div>
        )}
      </article>
    </>
  )
}
