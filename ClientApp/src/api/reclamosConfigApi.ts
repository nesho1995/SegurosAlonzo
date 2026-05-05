import { getJson, postJson, requestJson, sendJson } from './http'
import type { CorreoReclamoPatron, CorreoReclamoPlantilla, CorreoRevisionItem, ProbarPatronesResult, ReclamoCorreoConfig, ReclamoWorkerEstado } from '../types/reclamosConfig'

export function getReclamoCorreoConfig() { return getJson<ReclamoCorreoConfig>('/api/reclamos-config/correo') }
export function saveReclamoCorreoConfig(config: ReclamoCorreoConfig) { return sendJson('/api/reclamos-config/correo', 'PUT', config) }
export function testReclamoCorreoConnection() { return postJson('/api/reclamos-config/probar-conexion', {}) }
export function processReclamosNow() { return postJson<ReclamoWorkerEstado>('/api/reclamos-config/procesar-ahora', {}) }
export function recoveryReclamos(hours = 72) { return postJson<ReclamoWorkerEstado>(`/api/reclamos-config/modo-recuperacion?horas=${hours}`, {}) }
export function getReclamoWorkerStatus() { return getJson<ReclamoWorkerEstado>('/api/reclamos-config/worker-estado') }
export function getCorreoRevision(estado = 'TODOS') { return getJson<{ items: CorreoRevisionItem[] }>(`/api/reclamos-config/correo-bandeja?estado=${encodeURIComponent(estado)}`) }

export function getCorreoPatrones() { return getJson<{ items: CorreoReclamoPatron[] }>('/api/reclamos-config/patrones') }
export function saveCorreoPatron(model: CorreoReclamoPatron) { return requestJson<{ id: number }>('/api/reclamos-config/patrones', 'PUT', model) }
export function getCorreoPlantillas() { return getJson<{ items: CorreoReclamoPlantilla[] }>('/api/reclamos-config/plantillas') }
export function saveCorreoPlantilla(model: CorreoReclamoPlantilla) { return requestJson<{ id: number }>('/api/reclamos-config/plantillas', 'PUT', model) }
export function setPlantillaReglas(plantillaId: number, patronIds: number[]) { return sendJson(`/api/reclamos-config/plantillas/${plantillaId}/reglas`, 'PUT', patronIds) }
export function getPlantillaReglas(plantillaId: number) { return getJson<{ items: { plantillaId: number; patronId: number }[] }>(`/api/reclamos-config/plantillas/${plantillaId}/reglas`) }
export function testCorreoPatrones(subject: string, body: string, plantillaId?: number) {
  return postJson<ProbarPatronesResult>('/api/reclamos-config/probar-patrones', { subject, body, plantillaId })
}
