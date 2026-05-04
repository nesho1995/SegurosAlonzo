import { useEffect, useState } from 'react'
import { Save, Upload } from 'lucide-react'
import { getEmpresaConfiguracion, updateEmpresaConfiguracion, uploadEmpresaLogo, type EmpresaConfiguracion } from '../api/configuracionApi'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import { useAuth } from '../hooks/useAuth'

export function ConfiguracionEmpresaView() {
  const { hasPermission } = useAuth()
  const canEdit = hasPermission('configuracion.administrar')
  const [config, setConfig] = useState<EmpresaConfiguracion | null>(null)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [logoVersion, setLogoVersion] = useState(0)

  async function load() {
    setConfig(await getEmpresaConfiguracion())
  }

  useEffect(() => {
    let alive = true
    getEmpresaConfiguracion()
      .then((data) => { if (alive) setConfig(data) })
      .catch((err) => { if (alive) setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion.') })
    return () => { alive = false }
  }, [])

  async function save() {
    if (!canEdit) return
    if (!config) return
    await updateEmpresaConfiguracion(config)
    setMessage('Configuracion guardada.')
    window.dispatchEvent(new CustomEvent('app:empresa-updated'))
    await load()
  }

  async function upload(file?: File) {
    if (!canEdit) return
    if (!file) return
    await uploadEmpresaLogo(file)
    setMessage('Logo actualizado.')
    setLogoVersion((value) => value + 1)
    window.dispatchEvent(new CustomEvent('app:empresa-updated'))
    await load()
  }

  if (!config) return <LoadingCard text="Cargando configuracion..." />

  return (
    <>
      <PageHeader eyebrow="Configuracion" title="Empresa" description="Personaliza el nombre, logo y color principal para trabajar con la identidad de la correduria." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      <section className="content-grid">
        <article className="panel form-panel">
          <PanelTitle title="Identidad visual" subtitle="Estos datos aparecen en el encabezado y el menu principal." />
          <div className="form-grid">
            <Field label="Nombre de empresa" value={config.nombreEmpresa} onChange={(value) => setConfig({ ...config, nombreEmpresa: value })} />
            <Field label="Telefono empresa" value={config.telefonoEmpresa || ''} onChange={(value) => setConfig({ ...config, telefonoEmpresa: value })} />
            <Field label="Color principal" value={config.colorPrimario || ''} onChange={(value) => setConfig({ ...config, colorPrimario: value })} />
            {canEdit && <label className="wide-field">
              <span>Logo</span>
              <input type="file" accept=".png,.jpg,.jpeg,.webp" onChange={(event) => void upload(event.target.files?.[0])} />
            </label>}
            <div className="form-actions">
              {canEdit && <button className="primary-button" onClick={() => void save()}><Save size={18} />Guardar</button>}
            </div>
          </div>
        </article>
        <article className="panel">
          <PanelTitle title="Vista previa" subtitle="Asi se vera la marca en la aplicacion." />
          <div className="brand-preview" style={{ borderColor: config.colorPrimario || '#2563eb' }}>
            {config.logoUrl ? <img src={`${config.logoUrl}?v=${logoVersion}`} alt="Logo de empresa" /> : <Upload size={42} />}
            <strong>{config.nombreEmpresa}</strong>
          </div>
        </article>
      </section>
    </>
  )
}
