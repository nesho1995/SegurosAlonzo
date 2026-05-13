import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Save, WandSparkles } from 'lucide-react'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { getEnvioAutomaticoConfig, type EnvioAutomaticoConfig, updateEnvioAutomaticoConfig } from '../api/configuracionApi'
import { useAuth } from '../hooks/useAuth'

const ejemplo = {
  cliente: 'Carlos Mendoza',
  poliza: 'PZ-2026-0019',
  fecha_vencimiento: '30/05/2026',
  monto: 'L 2,350.00',
  aseguradora: 'Seguros Centro',
  reclamo: 'RC-99871',
  dias: '7',
}

export function AutomatizacionEnviosView() {
  const { hasPermission } = useAuth()
  const canEdit = hasPermission('configuracion.administrar')
  const [config, setConfig] = useState<EnvioAutomaticoConfig | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState('')

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setConfig(await getEnvioAutomaticoConfig())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    getEnvioAutomaticoConfig()
      .then((data) => {
        if (alive) setConfig(data)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })
    return () => {
      alive = false
    }
  }, [])

  async function save() {
    if (!canEdit || !config) return
    setSaving(true)
    setError(null)
    setMessage('')
    try {
      await updateEnvioAutomaticoConfig(config)
      setMessage('Configuracion guardada.')
      window.dispatchEvent(new CustomEvent('app:envios-updated'))
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar la configuracion.')
    } finally {
      setSaving(false)
    }
  }

  const previews = useMemo(() => {
    if (!config) return []
    return [
      { title: 'Pago proximo', value: renderTemplate(config.plantillaPagoProximo, ejemplo) },
      { title: 'Pago vencido', value: renderTemplate(config.plantillaPagoVencido, ejemplo) },
      { title: 'Poliza por vencer', value: renderTemplate(config.plantillaPolizaPorVencer, ejemplo) },
      { title: 'Poliza vencida', value: renderTemplate(config.plantillaPolizaVencida, ejemplo) },
      { title: 'Reclamo', value: renderTemplate(config.plantillaReclamo, ejemplo) },
    ]
  }, [config])

  if (loading || !config) return <LoadingCard text="Cargando automatizacion de envios..." />

  return (
    <>
      <PageHeader
        eyebrow="Configuracion"
        title="Automatizacion de envios"
        description="Activa o desactiva envios automaticos para reclamos y recordatorios con reglas y plantillas."
        onRefresh={load}
      />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      <div className="inline-alert warning">
        <AlertTriangle size={16} />
        <span>Al activar esta opcion, el sistema enviara WhatsApp automaticamente segun las reglas configuradas.</span>
      </div>
      <section className="content-grid">
        <article className="panel form-panel">
          <PanelTitle title="Activacion por tipo" subtitle="Por defecto todo queda apagado hasta que lo actives." />
          <div className="form-grid">
            <label className="check-field"><input type="checkbox" checked={config.autoEnviarReclamos} onChange={(event) => setConfig({ ...config, autoEnviarReclamos: event.target.checked })} />Enviar reclamos automaticamente</label>
            <label className="check-field"><input type="checkbox" checked={config.autoEnviarRecordatoriosPago} onChange={(event) => setConfig({ ...config, autoEnviarRecordatoriosPago: event.target.checked })} />Enviar recordatorios de pago automaticamente</label>
            <label className="check-field"><input type="checkbox" checked={config.autoEnviarRecordatoriosPoliza} onChange={(event) => setConfig({ ...config, autoEnviarRecordatoriosPoliza: event.target.checked })} />Enviar recordatorios de poliza automaticamente</label>
          </div>

          <PanelTitle title="Reglas de dias" subtitle="Separa los dias con coma. Ejemplo: 7,3,1" />
          <div className="form-grid">
            <Field label="Dias antes de vencimiento de cuota" value={config.diasAntesVencimientoCuota} onChange={(value) => setConfig({ ...config, diasAntesVencimientoCuota: value })} />
            <Field label="Dias despues de cuota vencida" value={config.diasDespuesCuotaVencida} onChange={(value) => setConfig({ ...config, diasDespuesCuotaVencida: value })} />
            <Field label="Dias antes de vencimiento de poliza" value={config.diasAntesVencimientoPoliza} onChange={(value) => setConfig({ ...config, diasAntesVencimientoPoliza: value })} />
            <NumberField label="Dias entre recordatorios de reclamo" value={config.diasEntreRecordatoriosReclamo} min={1} max={365} onChange={(value) => setConfig({ ...config, diasEntreRecordatoriosReclamo: value })} />
            <NumberField label="Maximo recordatorios por reclamo" value={config.maxRecordatoriosReclamo} min={1} max={50} onChange={(value) => setConfig({ ...config, maxRecordatoriosReclamo: value })} />
          </div>

          <PanelTitle title="Plantillas" subtitle="Puedes usar: {cliente}, {poliza}, {fecha_vencimiento}, {monto}, {aseguradora}, {reclamo}, {dias}." />
          <div className="form-grid">
            <TextTemplate label="Plantilla pago proximo" value={config.plantillaPagoProximo} onChange={(value) => setConfig({ ...config, plantillaPagoProximo: value })} />
            <TextTemplate label="Plantilla pago vencido" value={config.plantillaPagoVencido} onChange={(value) => setConfig({ ...config, plantillaPagoVencido: value })} />
            <TextTemplate label="Plantilla poliza por vencer" value={config.plantillaPolizaPorVencer} onChange={(value) => setConfig({ ...config, plantillaPolizaPorVencer: value })} />
            <TextTemplate label="Plantilla poliza vencida" value={config.plantillaPolizaVencida} onChange={(value) => setConfig({ ...config, plantillaPolizaVencida: value })} />
            <TextTemplate label="Plantilla reclamo" value={config.plantillaReclamo} onChange={(value) => setConfig({ ...config, plantillaReclamo: value })} />
          </div>
          {canEdit && <div className="form-actions"><button className="primary-button" disabled={saving} onClick={() => void save()}><Save size={16} />{saving ? 'Guardando...' : 'Guardar configuracion'}</button></div>}
        </article>
        <article className="panel">
          <PanelTitle title="Probar plantilla" subtitle="Vista previa rapida con datos de ejemplo." />
          <div className="stack-list">
            {previews.map((item) => (
              <div className="state-card" key={item.title}>
                <strong><WandSparkles size={14} /> {item.title}</strong>
                <p>{item.value}</p>
              </div>
            ))}
          </div>
        </article>
      </section>
    </>
  )
}

function renderTemplate(template: string, values: Record<string, string>) {
  return template.replace(/\{([^}]+)\}/g, (_, key: string) => values[key] ?? `{${key}}`)
}

function TextTemplate({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="wide-field">
      <span>{label}</span>
      <textarea value={value} rows={4} onChange={(event) => onChange(event.target.value)} />
    </label>
  )
}

function NumberField({ label, value, min, max, onChange }: { label: string; value: number; min: number; max: number; onChange: (value: number) => void }) {
  return (
    <label className="field">
      <span>{label}</span>
      <input
        type="number"
        min={min}
        max={max}
        value={Number.isFinite(value) ? value : min}
        onChange={(event) => {
          const parsed = Number(event.target.value)
          onChange(Number.isFinite(parsed) ? Math.min(max, Math.max(min, parsed)) : min)
        }}
      />
    </label>
  )
}
