import { getJson, postJson, requestJson } from './http'
import type { ClaimChecklistItem, ClaimsResponse } from '../types/reclamos'

export function getReclamos(params: URLSearchParams) {
  return getJson<ClaimsResponse>(`/api/reclamos?${params}`)
}

export function getReclamoChecklist(id: number, tipoReclamo?: string) {
  const query = tipoReclamo ? `?tipoReclamo=${encodeURIComponent(tipoReclamo)}` : ''
  return getJson<{ tipoReclamo: string; requisitos: ClaimChecklistItem[]; documentosPendientes: number }>(`/api/reclamos/${id}/checklist${query}`)
}

export function getReclamoDocumentosPendientes(id: number) {
  return getJson<{ items: ClaimPendingDocument[]; pendientes: number }>(`/api/reclamos/${id}/documentos-pendientes`)
}

export function updateReclamoDocumento(id: number, documentoId: number, recibido: boolean) {
  return requestJson(`/api/reclamos/${id}/documentos/${documentoId}`, 'PUT', { recibido })
}

export function solicitarDocumentosReclamo(id: number) {
  return postJson(`/api/reclamos/${id}/solicitar-documentos`, {})
}

export function marcarDocumentosCompletosReclamo(id: number) {
  return postJson<{ ok: boolean; response: string }>(`/api/reclamos/${id}/marcar-documentos-completos`, {})
}

export function enviarWhatsAppReclamo(id: number) {
  return postJson<{ ok: boolean; response: string }>(`/api/reclamos/${id}/enviar-whatsapp`, {})
}

export function enviarRecordatorioReclamo(id: number) {
  return postJson<{ ok: boolean; response: string }>(`/api/reclamos/${id}/enviar-recordatorio`, {})
}

export function enviarDocumentosAseguradora(id: number, correoAseguradora: string) {
  return postJson<{ ok: boolean; response: string }>(`/api/reclamos/${id}/enviar-aseguradora`, { correoAseguradora })
}

export type ClaimPendingDocument = {
  id: number
  reclamoId: number
  documento: string
  recibido: boolean
  fechaRecibido?: string
}
