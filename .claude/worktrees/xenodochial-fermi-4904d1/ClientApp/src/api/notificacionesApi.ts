import { getJson, postJson } from './http'
import type { NotificacionesResponse } from '../types/notificaciones'
export function getNotificaciones() { return getJson<NotificacionesResponse>('/api/notificaciones') }
export function marcarNotificacionLeida(id: number) { return postJson<{ message: string }>(`/api/notificaciones/${id}/leida`, {}) }
