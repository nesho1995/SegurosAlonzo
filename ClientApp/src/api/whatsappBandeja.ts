export interface ConversacionListItem {
  id: number
  telefono: string
  nombreContacto: string | null
  clienteId: number | null
  nombreCliente: string | null
  estado: 'abierta' | 'en_espera' | 'resuelta'
  ultimaActividad: string
  noLeidos: number
  ultimoMensaje: string | null
  ultimoDireccion: 'entrante' | 'saliente' | null
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

export interface ConversacionDetalle {
  id: number
  telefono: string
  nombreContacto: string | null
  clienteId: number | null
  estado: string
  ultimaActividad: string
  noLeidos: number
}

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    const msg = (body as { error?: string }).error ?? `HTTP ${res.status}`
    throw new Error(msg)
  }
  return res.json()
}

export async function getConversaciones(params: {
  estado?: string
  buscar?: string
  limit?: number
  offset?: number
}): Promise<{ items: ConversacionListItem[]; total: number }> {
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
