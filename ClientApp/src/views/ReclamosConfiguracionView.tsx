import { useEffect, useState } from 'react'
import { Field, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { ErrorCard } from '../components/ErrorAlert'
import { LoadingCard } from '../components/LoadingState'
import { getCorreoPatrones, saveCorreoPatron, testCorreoPatrones } from '../api/reclamosConfigApi'
import type { CorreoReclamoPatron, ProbarPatronesResult } from '../types/reclamosConfig'

const emptyPatron: CorreoReclamoPatron = {
  id: 0, nombre: '', activo: true, prioridad: 100, campoDestino: 'NumeroReclamo', fuente: 'SUBJECT_BODY', tipoRegla: 'REGEX', patron: '', grupoRegex: '', requerido: false, normalizarTexto: true
}

export function ReclamosConfiguracionView() {
  const [patrones, setPatrones] = useState<CorreoReclamoPatron[] | null>(null)
  const [patronForm, setPatronForm] = useState<CorreoReclamoPatron>(emptyPatron)
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [result, setResult] = useState<ProbarPatronesResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    setError(null)
    const pats = await getCorreoPatrones()
    setPatrones(pats.items)
  }

  useEffect(() => {
    let alive = true
    getCorreoPatrones()
      .then((pats) => {
        if (!alive) return
        setPatrones(pats.items)
      })
      .catch((err) => {
        if (!alive) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion.')
      })
    return () => { alive = false }
  }, [])

  async function savePattern() {
    await saveCorreoPatron(patronForm)
    setMessage('Patron guardado.')
    setPatronForm(emptyPatron)
    await load()
  }

  async function runTest() {
    setResult(await testCorreoPatrones(subject, body) as ProbarPatronesResult)
  }

  if (!patrones) return <LoadingCard text="Cargando configuracion de reclamos..." />

  return (
    <>
      <PageHeader eyebrow="Admin" title="Reglas de reclamos" description="Configura patrones de extraccion y prueba asuntos/cuerpos sin tocar codigo." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      <section className="content-grid">
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
