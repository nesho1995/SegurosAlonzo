import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, FileText, Save, Search, Wrench } from 'lucide-react'
import { getExtractorConfig, saveExtractorConfig, testExtractor } from '../api/extractorApi'
import { StatusPill } from '../components/Badge'
import { DataTable } from '../components/DataTable'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import type { ExtractorConfig, ExtractorResult } from '../types/extractor'
import { extractorEmailExample } from '../types/extractor'
import { compactMeta } from '../utils/formatters'

export function ExtractorView() {
  const [config, setConfig] = useState<ExtractorConfig | null>(null)
  const [test, setTest] = useState({ remitente: '', asunto: '', cuerpo: '' })
  const [result, setResult] = useState<ExtractorResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setConfig(await getExtractorConfig())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  async function save() {
    if (!config) return
    setError(null)
    setMessage(null)
    try {
      await saveExtractorConfig(config)
      setMessage('Configuracion guardada.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function runTest() {
    setError(null)
    setResult(null)
    try {
      setResult(await testExtractor(test))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  useEffect(() => {
    let alive = true
    getExtractorConfig()
      .then((json) => {
        if (alive) setConfig(json)
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
  }, [])

  if (loading) return <LoadingCard text="Cargando configuracion..." />

  return (
    <>
      <PageHeader
        eyebrow="Reclamos"
        title="Configuracion del extractor"
        description="Define como se identifican correos de reclamos y prueba la extraccion sin afectar el flujo actual."
        onRefresh={load}
      />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      {config && (
        <>
          <section className="content-grid">
            <article className="panel">
              <PanelTitle title="Reglas de lectura" subtitle="Usa listas separadas por coma. Relaciona cada palabra clave con la aseguradora correspondiente." />
              <div className="form-grid">
                <Field label="Remitentes permitidos" value={config.remitentesPermitidos || ''} onChange={(value) => setConfig({ ...config, remitentesPermitidos: value })} />
                <Field label="Palabras del asunto" value={config.palabrasClaveAsunto || ''} onChange={(value) => setConfig({ ...config, palabrasClaveAsunto: value })} />
                <label className="wide-field">
                  <span>Reglas de aseguradora</span>
                  <textarea value={config.aseguradorasReglas || ''} onChange={(event) => setConfig({ ...config, aseguradorasReglas: event.target.value })} placeholder="ficohsa: Ficohsa Seguros" />
                </label>
                <Field label="Campos obligatorios" value={config.camposObligatorios || ''} onChange={(value) => setConfig({ ...config, camposObligatorios: value })} />
                <label className="wide-field">
                  <span>Plantilla WhatsApp</span>
                  <textarea value={config.plantillaWhatsApp || ''} onChange={(event) => setConfig({ ...config, plantillaWhatsApp: event.target.value })} />
                </label>
                <div className="form-actions">
                  <button className="primary-button" onClick={save}><Save size={18} />Guardar reglas</button>
                </div>
              </div>
            </article>

            <article className="panel">
              <PanelTitle title="Probar extraccion" subtitle="Carga un ejemplo o pega un correo real. La prueba no guarda reclamos ni envia WhatsApp." />
              <div className="inline-alert info">Puedes usar el ejemplo para validar campos, confianza y taller sugerido antes de guardar reglas.</div>
              <div className="form-grid">
                <Field label="Remitente" value={test.remitente} onChange={(value) => setTest({ ...test, remitente: value })} />
                <Field label="Asunto" value={test.asunto} onChange={(value) => setTest({ ...test, asunto: value })} />
                <label className="wide-field">
                  <span>Cuerpo del correo</span>
                  <textarea value={test.cuerpo} onChange={(event) => setTest({ ...test, cuerpo: event.target.value })} />
                </label>
                <div className="form-actions">
                  <button className="icon-button secondary" onClick={() => setTest(extractorEmailExample)}><FileText size={18} />Usar ejemplo</button>
                  <button className="primary-button" onClick={runTest}><Search size={18} />Probar</button>
                </div>
              </div>
            </article>
          </section>
          {result && (
            <article className="panel mt-panel">
              <PanelTitle title="Resultado de prueba" subtitle="Campos detectados, faltantes, confianza y mensaje generado." />
              <ExtractorResultPanel result={result} />
            </article>
          )}
        </>
      )}
    </>
  )
}

export function ExtractorResultPanel({ result }: { result: ExtractorResult }) {
  const confidenceTone = result.confianza >= 80 ? 'success' : result.confianza >= 50 ? 'warning' : 'danger'

  return (
    <div className="result-panel">
      <div className="result-summary">
        <StatusPill text={`Confianza ${result.confianza}%`} tone={confidenceTone} />
        <span>{result.camposFaltantes.length ? 'Requiere revision antes de automatizar.' : 'Listo para revision operativa.'}</span>
      </div>
      {result.camposFaltantes.length > 0 && (
        <div className="inline-alert danger">
          Faltan datos para completar el reclamo: {result.camposFaltantes.join(', ')}. Ajusta el correo o las reglas antes de usarlo en produccion.
        </div>
      )}
      <div className="mini-grid">
        <Metric title="Confianza" value={`${result.confianza}%`} hint="Segun campos obligatorios" tone={result.confianza >= 80 ? 'green' : result.confianza >= 50 ? 'amber' : 'red'} icon={CheckCircle2} />
        <Metric title="Faltantes" value={result.camposFaltantes.length} hint={result.camposFaltantes.join(', ') || 'Completo'} tone={result.camposFaltantes.length ? 'amber' : 'green'} icon={AlertTriangle} />
        <Metric title="Talleres" value={result.talleresSugeridos.length} hint="Sugerencias encontradas" tone="blue" icon={Wrench} />
      </div>
      <DataTable
        headers={['Campo', 'Valor']}
        rows={Object.entries(result.camposDetectados).map(([key, value]) => [fieldLabel(key), value || 'No detectado'])}
      />
      <div className="message-preview">
        <strong>Mensaje sugerido</strong>
        <p>{result.mensaje || 'Sin mensaje generado.'}</p>
      </div>
      {result.tallerDetectado && (
        <div className="message-preview">
          <strong>Taller candidato detectado</strong>
          <p>{compactMeta([result.tallerDetectado.nombre, result.tallerDetectado.ciudad, result.tallerDetectado.aseguradora])}</p>
        </div>
      )}
    </div>
  )
}

function fieldLabel(value: string) {
  const labels: Record<string, string> = {
    cliente: 'Cliente',
    nombre_cliente: 'Cliente',
    aseguradora: 'Aseguradora',
    poliza: 'Poliza',
    telefono: 'Telefono',
    placa: 'Placa',
    taller: 'Taller',
    ciudad: 'Ciudad',
    fecha: 'Fecha',
  }
  return labels[value] ?? value.replaceAll('_', ' ')
}

