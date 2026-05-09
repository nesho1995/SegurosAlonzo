export interface ConversacionListItem {
  id: number
  telefono: string
  nombreContacto: string | null
  clienteId: number | null
  nombreCliente: string | null
  reclamoId: number | null
  numeroReclamo: string | null
  agenteAsignadoId: number | null
  agenteNombre: string | null
  estado: 'abierta' | 'en_espera' | 'resuelta'
  ultimaActividad: string
  noLeidos: number
  ultimoMensaje: string | null
  ultimoDireccion: 'entrante' | 'saliente' | null
  ultimoTipoContenido: string | null
}

export interface ConversacionDetalle {
  id: number
  telefono: string
  nombreContacto: string | null
  clienteId: number | null
  nombreCliente: string | null
  reclamoId: number | null
  numeroReclamo: string | null
  conductorReclamo: string | null
  agenteAsignadoId: number | null
  agenteNombre: string | null
  estado: string
  ultimaActividad: string
  noLeidos: number
  creadoEn: string
}

export interface MensajeDto {
  id: number
  direccion: 'entrante' | 'saliente'
  tipoContenido: string
  contenido: string | null
  mediaId: string | null
  mediaTipoMime: string | null
  mediaNombre: string | null
  estado: string
  nombreUsuario: string | null
  creadoEn: string
}

export interface AgenteSummary {
  id: number
  username: string
  roleName: string | null
}

export interface ReclamoLinkOption {
  id: number
  referencia: string
  cliente: string
  poliza: string | null
  placa: string | null
  celular: string | null
  fechaNotificacion: string | null
  estado: string
  telefonoCoincide: boolean
}

export interface ClaimPendingDocument {
  id: number
  reclamoId: number
  documento: string
  recibido: boolean
  fechaRecibido?: string
}

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error((body as { error?: string }).error ?? `HTTP ${res.status}`)
  }
  return res.json()
}

export async function getConversaciones(params: {
  estado?: string
  buscar?: string
  limit?: number
  offset?: number
}): Promise<{ items: ConversacionListItem[]; total: number; totalNoLeidos: number }> {
  const qs = new URLSearchParams()
  if (params.estado) qs.set('estado', params.estado)
  if (params.buscar) qs.set('buscar', params.buscar)
  if (params.limit != null) qs.set('limit', String(params.limit))
  if (params.offset != null) qs.set('offset', String(params.offset))
  return apiFetch(`/api/whatsapp/bandeja?${qs}`)
}

export async function getMensajes(
  conversacionId: number,
  params: { limit?: number; offset?: number } = {}
): Promise<{ conversacion: ConversacionDetalle; items: MensajeDto[]; total: number }> {
  const qs = new URLSearchParams()
  if (params.limit != null) qs.set('limit', String(params.limit))
  if (params.offset != null) qs.set('offset', String(params.offset))
  return apiFetch(`/api/whatsapp/bandeja/${conversacionId}/mensajes?${qs}`)
}

export async function responder(conversacionId: number, mensaje: string): Promise<void> {
  await apiFetch(`/api/whatsapp/bandeja/${conversacionId}/responder`, {
    method: 'POST',
    body: JSON.stringify({ mensaje }),
  })
}

export async function marcarLeido(conversacionId: number): Promise<void> {
  await apiFetch(`/api/whatsapp/bandeja/${conversacionId}/marcar-leido`, { method: 'POST' })
}

export async function cambiarEstado(conversacionId: number, estado: string): Promise<void> {
  await apiFetch(`/api/whatsapp/bandeja/${conversacionId}/estado`, {
    method: 'PUT',
    body: JSON.stringify({ estado }),
  })
}

export async function asociarCliente(conversacionId: number, clienteId: number): Promise<void> {
  await apiFetch(`/api/whatsapp/bandeja/${conversacionId}/asociar-cliente`, {
    method: 'POST',
    body: JSON.stringify({ clienteId }),
  })
}

export async function asociarReclamo(
  conversacionId: number,
  reclamoId: number | null
): Promise<{ conversacion: ConversacionDetalle }> {
  return apiFetch(`/api/whatsapp/bandeja/${conversacionId}/asociar-reclamo`, {
    method: 'POST',
    body: JSON.stringify({ reclamoId }),
  })
}

export async function asignarAgente(conversacionId: number, agenteId: number | null): Promise<void> {
  await apiFetch(`/api/whatsapp/bandeja/${conversacionId}/asignar-agente`, {
    method: 'POST',
    body: JSON.stringify({ agenteId }),
  })
}

export async function getAgentes(): Promise<AgenteSummary[]> {
  return apiFetch('/api/whatsapp/bandeja/agentes')
}

export async function buscarReclamos(params: {
  buscar?: string
  telefono?: string
}): Promise<ReclamoLinkOption[]> {
  const qs = new URLSearchParams()
  if (params.buscar) qs.set('buscar', params.buscar)
  if (params.telefono) qs.set('telefono', params.telefono)
  return apiFetch(`/api/whatsapp/bandeja/reclamos?${qs}`)
}

export async function getDocumentosReclamo(
  reclamoId: number
): Promise<{ items: ClaimPendingDocument[]; pendientes: number }> {
  return apiFetch(`/api/reclamos/${reclamoId}/documentos-pendientes`)
}

export async function guardarDocumentoMensaje(
  mensajeId: number,
  reclamoId: number,
  reclamoDocumentoId: number
): Promise<{ checklist: ClaimPendingDocument[]; completo: boolean }> {
  return apiFetch(`/api/whatsapp/bandeja/mensajes/${mensajeId}/guardar-documento`, {
    method: 'POST',
    body: JSON.stringify({ reclamoId, reclamoDocumentoId }),
  })
}

export function mediaUrl(mediaId: string, forceDownload = false): string {
  return `/api/whatsapp/media/${mediaId}${forceDownload ? '?download=true' : ''}`
}
