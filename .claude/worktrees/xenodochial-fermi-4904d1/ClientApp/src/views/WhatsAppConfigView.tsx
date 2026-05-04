import { useEffect, useState } from 'react'
import { Save, Send, ToggleLeft, ToggleRight } from 'lucide-react'
import { getWhatsAppConfig, probarWhatsApp, updateWhatsAppConfig } from '../api/configuracionApi'
import type { WhatsAppConfig } from '../api/configuracionApi'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'

const emptyConfig: WhatsAppConfig = {
  enabled: false,
  graphVersion: 'v18.0',
  phoneNumberId: '',
  accessToken: '',
  accessTokenMasked: '',
  templateName: '',
  languageCode: 'es',
  adminWhatsAppNumber: '',
}

export function WhatsAppConfigView() {
  const [config, setConfig] = useState<WhatsAppConfig>(emptyConfig)
  const [testPhone, setTestPhone] = useState('')
  const [testMessage, setTestMessage] = useState('Prueba de WhatsApp desde Seguros Alonzo.')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const data = await getWhatsAppConfig()
      setConfig({ ...emptyConfig, ...data, accessToken: '' })
      setTestPhone(data.adminWhatsAppNumber || '')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion de WhatsApp.')
    } finally {
      setLoading(false)
    }
  }

  async function save() {
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      await updateWhatsAppConfig(config)
      setMessage('Configuracion de WhatsApp guardada.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar WhatsApp.')
    } finally {
      setSaving(false)
    }
  }

  async function sendTest() {
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      const result = await probarWhatsApp(testPhone, testMessage)
      if (!result?.ok) {
        setError(result?.response || 'La prueba no pudo enviarse.')
      } else {
        setMessage('Mensaje de prueba enviado.')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo enviar la prueba.')
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => {
    let alive = true
    getWhatsAppConfig()
      .then((data) => {
        if (!alive) return
        setConfig({ ...emptyConfig, ...data, accessToken: '' })
        setTestPhone(data.adminWhatsAppNumber || '')
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'No se pudo cargar la configuracion de WhatsApp.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [])

  return (
    <>
      <PageHeader
        eyebrow="Administracion"
        title="WhatsApp produccion"
        description="Controla los envios reales sin tocar codigo: activacion, credenciales, numero administrador y prueba manual."
        onRefresh={load}
      />
      {loading && <LoadingCard text="Cargando WhatsApp..." />}
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      {!loading && (
        <section className="content-grid">
          <article className="panel">
            <PanelTitle
              title="Credenciales de Meta"
              subtitle={config.enabled ? 'Los envios reales estan activos.' : 'Los envios reales estan apagados.'}
            />
            <div className="inline-alert warning">
              Activa produccion solo cuando el token, Phone Number ID y numero administrador ya esten verificados.
            </div>
            <div className="form-grid">
              <label className="check-field wide-field">
                <input
                  type="checkbox"
                  checked={config.enabled}
                  onChange={(event) => setConfig({ ...config, enabled: event.target.checked })}
                />
                {config.enabled ? <ToggleRight size={18} /> : <ToggleLeft size={18} />}
                WhatsApp produccion activo
              </label>
              <label className="field">
                <span>Graph version</span>
                <input value={config.graphVersion} onChange={(event) => setConfig({ ...config, graphVersion: event.target.value })} />
              </label>
              <label className="field">
                <span>Phone Number ID</span>
                <input value={config.phoneNumberId} onChange={(event) => setConfig({ ...config, phoneNumberId: event.target.value })} />
              </label>
              <label className="field">
                <span>Access token</span>
                <input
                  type="password"
                  placeholder={config.accessTokenMasked || 'Pega un token nuevo'}
                  value={config.accessToken}
                  onChange={(event) => setConfig({ ...config, accessToken: event.target.value })}
                />
              </label>
              <label className="field">
                <span>Idioma</span>
                <input value={config.languageCode} onChange={(event) => setConfig({ ...config, languageCode: event.target.value })} />
              </label>
              <label className="field">
                <span>Plantilla default</span>
                <input value={config.templateName} onChange={(event) => setConfig({ ...config, templateName: event.target.value })} />
              </label>
              <label className="field">
                <span>Numero administrador</span>
                <input value={config.adminWhatsAppNumber} onChange={(event) => setConfig({ ...config, adminWhatsAppNumber: event.target.value })} />
              </label>
              <div className="form-actions wide-field">
                <button className="primary-button" disabled={saving} onClick={() => void save()}>
                  <Save size={16} />Guardar WhatsApp
                </button>
              </div>
            </div>
          </article>
          <aside className="panel">
            <PanelTitle title="Prueba manual" subtitle="Envia un mensaje real al numero indicado usando la configuracion actual." />
            <div className="form-grid single-column">
              <label className="field">
                <span>Telefono de prueba</span>
                <input value={testPhone} onChange={(event) => setTestPhone(event.target.value)} />
              </label>
              <label className="wide-field">
                <span>Mensaje</span>
                <textarea value={testMessage} onChange={(event) => setTestMessage(event.target.value)} />
              </label>
              <button className="primary-button wide-field" disabled={saving || !config.enabled} onClick={() => void sendTest()}>
                <Send size={16} />Enviar prueba
              </button>
              {!config.enabled && <div className="inline-alert info">La prueba queda bloqueada mientras produccion este apagado.</div>}
            </div>
          </aside>
        </section>
      )}
    </>
  )
}
