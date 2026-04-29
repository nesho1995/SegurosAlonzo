import { getJson, postJson } from './http'
import type { ReminderResponse } from '../types/recordatorios'
export function getRecordatorios(params: URLSearchParams) { return getJson<ReminderResponse>(`/api/recordatorios?${params}`) }
export function generarRecordatorios() { return postJson<{ creados: number }>('/api/recordatorios/generar', {}) }
export function enviarRecordatorio(id: number) { return postJson<{ ok: boolean; response: string }>(`/api/recordatorios/${id}/enviar`, {}) }
export function descartarRecordatorio(id: number) { return postJson(`/api/recordatorios/${id}/descartar`, {}) }
