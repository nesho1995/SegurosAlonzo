import { getJson, postJson, sendJson } from './http'

export type EmpresaConfiguracion = {
  id: number
  nombreEmpresa: string
  telefonoEmpresa?: string
  logoUrl?: string
  colorPrimario?: string
  fechaActualizacion?: string
}

export type EnvioAutomaticoConfig = {
  autoEnviarReclamos: boolean
  autoEnviarRecordatoriosPago: boolean
  autoEnviarRecordatoriosPoliza: boolean
  diasAntesVencimientoCuota: string
  diasDespuesCuotaVencida: string
  diasAntesVencimientoPoliza: string
  diasEntreRecordatoriosReclamo: number
  maxRecordatoriosReclamo: number
  plantillaPagoProximo: string
  plantillaPagoVencido: string
  plantillaPolizaPorVencer: string
  plantillaPolizaVencida: string
  plantillaReclamo: string
}

export type WhatsAppConfig = {
  enabled: boolean
  graphVersion: string
  phoneNumberId: string
  accessToken: string
  accessTokenMasked: string
  templateName: string
  reclamoInitialTemplateName: string
  reclamoReminderTemplateName: string
  reclamoCompleteTemplateName: string
  languageCode: string
  adminWhatsAppNumber: string
  webhookVerifyToken: string
  webhookVerifyTokenMasked: string
}

export type SmtpConfig = {
  enabled: boolean
  host: string
  port: number
  useSsl: boolean
  username: string
  password?: string
  passwordMasked?: string
  fromAddress: string
  fromName: string
}

export function getEmpresaConfiguracion() {
  return getJson<EmpresaConfiguracion>('/api/configuracion/empresa')
}

export function updateEmpresaConfiguracion(config: EmpresaConfiguracion) {
  return sendJson('/api/configuracion/empresa', 'PUT', config)
}

export function getEnvioAutomaticoConfig() {
  return getJson<EnvioAutomaticoConfig>('/api/configuracion/envios')
}

export function updateEnvioAutomaticoConfig(config: EnvioAutomaticoConfig) {
  return sendJson('/api/configuracion/envios', 'PUT', config)
}

export function getWhatsAppConfig() {
  return getJson<WhatsAppConfig>('/api/configuracion/whatsapp')
}

export function updateWhatsAppConfig(config: WhatsAppConfig) {
  return sendJson('/api/configuracion/whatsapp', 'PUT', config)
}

export function probarWhatsApp(telefono: string, mensaje: string) {
  return postJson<{ ok: boolean; response: string }>('/api/configuracion/whatsapp/probar', { telefono, mensaje })
}

export function getSmtpConfig() {
  return getJson<SmtpConfig>('/api/configuracion/smtp')
}

export function updateSmtpConfig(config: SmtpConfig) {
  return sendJson('/api/configuracion/smtp', 'PUT', config)
}

export function probarSmtp() {
  return postJson<{ ok: boolean; response: string }>('/api/configuracion/smtp/probar', {})
}

export async function uploadEmpresaLogo(file: File) {
  const form = new FormData()
  form.append('archivo', file)
  const response = await fetch('/api/configuracion/empresa/logo', { method: 'POST', credentials: 'include', body: form })
  if (!response.ok) {
    const data = await response.json().catch(() => ({ error: 'No se pudo subir el logo.' }))
    throw new Error(data.error || 'No se pudo subir el logo.')
  }
  return await response.json() as { logoUrl: string }
}
