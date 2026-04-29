import { getJson, postJson, sendJson, uploadJson } from './http'
import type { DetectedWorkshop, Workshop, WorkshopImportRow } from '../types/talleres'
export function getTalleres(params: URLSearchParams) { return getJson<{ items: Workshop[] }>(`/api/talleres?${params}`) }
export function getTalleresDetectados() { return getJson<{ items: DetectedWorkshop[] }>('/api/talleres/detectados') }
export function saveTaller(taller: Workshop) { if (taller.id) return sendJson(`/api/talleres/${taller.id}`, 'PUT', taller); return postJson('/api/talleres', taller) }
export function cambiarEstadoTaller(taller: Workshop) { return sendJson(`/api/talleres/${taller.id}/estado`, 'PATCH', { activo: !taller.activo }) }
export function resolverTallerDetectado(id: number, action: 'aprobar' | 'descartar') { return postJson(`/api/talleres/detectados/${id}/${action}`, {}) }
export function previewTalleres(file: File) { return uploadJson<{ items: WorkshopImportRow[] }>('/api/talleres/preview', file) }
export function importarTalleres(file: File) { return uploadJson<{ importados: number; rechazados: number; errores: WorkshopImportRow[] }>('/api/talleres/importar', file) }
