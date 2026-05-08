import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, ExternalLink, Info, Save, Send, ToggleLeft, ToggleRight } from 'lucide-react'
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
  reclamoInitialTemplateName: '',
  reclamoReminderTemplateName: '',
  reclamoCompleteTemplateName: '',
  languageCode: 'es',
  adminWhatsAppNumber: '',
  webhookVerifyToken: '',
  webhookVerifyTokenMasked: '',
}

export function WhatsAppConfigView() {
  const [config, setConfig] = useState<WhatsAppConfig>(emptyConfig)
  const [testPhone, setTestPhone] = useState('')
  const [testMessage, setTestMessage] = useState('Hola, esta es una prueba de WhatsApp desde el sistema.')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const data = await getWhatsAppConfig()
      setConfig({ ...emptyConfig, ...data, accessToken: '', webhookVerifyToken: '' })
      setTestPhone(data.adminWhatsAppNumber || '')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar la configuración de WhatsApp.')
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
      setMessage('Configuración guardada correctamente.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar.')
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
        setError(result?.response || 'La prueba no pudo enviarse. Revisa el token y el Phone Number ID.')
      } else {
        setMessage('✓ Mensaje de prueba enviado correctamente.')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo enviar la prueba.')
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  // URL del webhook para mostrar al usuario
  const webhookUrl = `${window.location.origin}/api/webhook/whatsapp`

  return (
    <>
      <PageHeader
        eyebrow="Configuración"
        title="WhatsApp Business API"
        description="Conecta el sistema con tu número de WhatsApp Business para enviar notificaciones automáticas a clientes."
        onRefresh={load}
      />
      {loading && <LoadingCard text="Cargando configuración..." />}
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      {!loading && (
        <section className="content-grid">

          {/* ── Panel 1: Credenciales ── */}
          <article className="panel">
            <PanelTitle
              title="Credenciales de Meta"
              subtitle="Obtenlas en Meta Business Manager → WhatsApp → API Setup"
            />

            <div className="inline-alert warning">
              <AlertTriangle size={15} />
              Activa la producción solo cuando el token, Phone Number ID y número administrador estén verificados.
            </div>

            <div className="form-grid">
              <label className="check-field wide-field">
                <input
                  type="checkbox"
                  checked={config.enabled}
                  onChange={e => setConfig({ ...config, enabled: e.target.checked })}
                />
                {config.enabled ? <ToggleRight size={18} /> : <ToggleLeft size={18} />}
                <span>{config.enabled ? 'WhatsApp activo — los mensajes se envían' : 'WhatsApp inactivo — los mensajes no se envían'}</span>
              </label>

              <label className="field">
                <span>Phone Number ID</span>
                <input
                  placeholder="Ej: 107364953583422"
                  value={config.phoneNumberId}
                  onChange={e => setConfig({ ...config, phoneNumberId: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Access Token (token permanente recomendado)</span>
                <input
                  type="password"
                  placeholder={config.accessTokenMasked || 'Pega el token de acceso'}
                  value={config.accessToken}
                  onChange={e => setConfig({ ...config, accessToken: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Graph API Version</span>
                <input
                  value={config.graphVersion}
                  onChange={e => setConfig({ ...config, graphVersion: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Idioma del template</span>
                <input
                  placeholder="es"
                  value={config.languageCode}
                  onChange={e => setConfig({ ...config, languageCode: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Número administrador (recibe alertas internas)</span>
                <input
                  placeholder="Ej: 50499887766"
                  value={config.adminWhatsAppNumber}
                  onChange={e => setConfig({ ...config, adminWhatsAppNumber: e.target.value })}
                />
              </label>
            </div>
          </article>

          {/* ── Panel 2: Plantilla ── */}
          <article className="panel">
            <PanelTitle
              title="Plantilla de mensajes"
              subtitle="Meta exige plantillas aprobadas para mensajes que el sistema inicia (renovaciones, pagos, reclamos)."
            />

            <div className="inline-alert info">
              <Info size={15} />
              <div>
                <strong>¿Por qué necesitas una plantilla?</strong><br />
                WhatsApp solo permite enviar texto libre cuando el cliente te escribió primero (ventana de 24h).
                Para notificaciones automáticas (recordatorios de pago, vencimiento de póliza, etc.) <strong>siempre necesitas una plantilla aprobada</strong>.
              </div>
            </div>

            <div className="form-grid">
              <label className="field wide-field">
                <span>Plantilla generica en Meta</span>
                <input
                  placeholder="Opcional, para pruebas o avisos generales"
                  value={config.templateName}
                  onChange={e => setConfig({ ...config, templateName: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Plantilla reclamo inicial</span>
                <input
                  placeholder="reclamo_documentos_inicial_es"
                  value={config.reclamoInitialTemplateName}
                  onChange={e => setConfig({ ...config, reclamoInitialTemplateName: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Plantilla recordatorio documentos</span>
                <input
                  placeholder="reclamo_recordatorio_documentos_es"
                  value={config.reclamoReminderTemplateName}
                  onChange={e => setConfig({ ...config, reclamoReminderTemplateName: e.target.value })}
                />
              </label>

              <label className="field">
                <span>Plantilla documentos completos</span>
                <input
                  placeholder="reclamo_documentos_completos_es"
                  value={config.reclamoCompleteTemplateName}
                  onChange={e => setConfig({ ...config, reclamoCompleteTemplateName: e.target.value })}
                />
              </label>

              <div className="inline-alert info wide-field">
                <Info size={15} />
                <div>
                  <strong>Cómo crear la plantilla:</strong>
                  <ol style={{ margin: '6px 0 0 16px', padding: 0 }}>
                    <li>Ve a <strong>Meta Business Manager → WhatsApp → Manage Templates</strong></li>
                    <li>Crea plantillas de tipo <strong>Utility</strong></li>
                    <li>Usa las plantillas aprobadas para reclamo inicial, recordatorio y documentos completos</li>
                    <li>Espera aprobación (24-48h)</li>
                    <li>Copia los nombres exactos aquí arriba</li>
                  </ol>
                  <br />
                  Si dejas este campo vacío, el sistema intenta enviar texto libre (solo funciona si el cliente te escribió antes).
                </div>
              </div>

              {config.templateName && (
                <div className="inline-alert success wide-field">
                  <CheckCircle2 size={15} />
                  El sistema usará la plantilla generica <strong>"{config.templateName}"</strong> cuando no aplique una plantilla especifica.
                </div>
              )}
            </div>
          </article>

          {/* ── Panel 3: Webhook ── */}
          <article className="panel">
            <PanelTitle
              title="Webhook (callbacks de Meta)"
              subtitle="Meta llama a este URL para informarte cuando un mensaje fue entregado, leído o falló, y para recibir mensajes de clientes."
            />

            <div className="form-grid">
              <div className="field wide-field">
                <span className="field-label">URL del webhook (configura este en Meta)</span>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <code style={{ flex: 1, padding: '6px 10px', background: '#f4f4f5', borderRadius: 6, fontSize: 13, wordBreak: 'break-all' }}>
                    {webhookUrl}
                  </code>
                  <button
                    className="icon-button secondary"
                    title="Abrir"
                    onClick={() => window.open('https://developers.facebook.com/apps', '_blank')}
                  >
                    <ExternalLink size={14} />
                  </button>
                </div>
                <span className="field-hint">Copia este URL en Meta Business Manager → WhatsApp → Configuration → Webhook URL</span>
              </div>

              <label className="field wide-field">
                <span>Token de verificación del webhook</span>
                <input
                  type="password"
                  placeholder={config.webhookVerifyTokenMasked || 'Crea un token secreto (cualquier texto, ej: mi_token_secreto_2024)'}
                  value={config.webhookVerifyToken}
                  onChange={e => setConfig({ ...config, webhookVerifyToken: e.target.value })}
                />
                <span className="field-hint">
                  Ponle el mismo valor en Meta Business Manager → Webhook Verify Token. El sistema lo usará para verificar que Meta es quien llama.
                </span>
              </label>

              <div className="inline-alert info wide-field">
                <Info size={15} />
                <div>
                  <strong>Pasos para activar el webhook:</strong>
                  <ol style={{ margin: '6px 0 0 16px', padding: 0 }}>
                    <li>El sistema ya tiene la URL activa en <code>/api/webhook/whatsapp</code></li>
                    <li>Guarda un token de verificación aquí arriba</li>
                    <li>Ve a Meta Business Manager → <strong>WhatsApp → Configuration → Webhook</strong></li>
                    <li>Pega la URL y el mismo token</li>
                    <li>Selecciona suscribirte a: <strong>messages</strong> y <strong>message_status_updates</strong></li>
                    <li>Haz clic en <strong>Verify and Save</strong></li>
                  </ol>
                  <br />
                  El webhook es opcional pero recomendado: permite saber si los mensajes llegaron o fallaron.
                </div>
              </div>
            </div>
          </article>

          {/* ── Panel 4: Guardar ── */}
          <div style={{ gridColumn: '1 / -1' }}>
            <button className="primary-button" disabled={saving} onClick={() => void save()}>
              <Save size={16} />
              {saving ? 'Guardando...' : 'Guardar configuración'}
            </button>
          </div>

          {/* ── Panel 5: Prueba ── */}
          <aside className="panel">
            <PanelTitle
              title="Prueba manual"
              subtitle="Envía un mensaje real para verificar que la conexión funciona."
            />
            <div className="form-grid single-column">
              <label className="field">
                <span>Teléfono de prueba (con código de país)</span>
                <input
                  placeholder="Ej: 50499887766"
                  value={testPhone}
                  onChange={e => setTestPhone(e.target.value)}
                />
              </label>
              <label className="wide-field">
                <span>Mensaje de prueba</span>
                <textarea
                  value={testMessage}
                  onChange={e => setTestMessage(e.target.value)}
                />
              </label>

              {config.templateName && (
                <div className="inline-alert info wide-field">
                  <Info size={14} />
                  La prueba usará la plantilla <strong>"{config.templateName}"</strong>. El mensaje de arriba será el parámetro {'{{1}}'}.
                </div>
              )}

              <button
                className="primary-button wide-field"
                disabled={saving || !config.enabled}
                onClick={() => void sendTest()}
              >
                <Send size={16} />
                Enviar prueba
              </button>
              {!config.enabled && (
                <div className="inline-alert info">
                  Activa WhatsApp arriba para poder enviar la prueba.
                </div>
              )}
            </div>
          </aside>

        </section>
      )}
    </>
  )
}
