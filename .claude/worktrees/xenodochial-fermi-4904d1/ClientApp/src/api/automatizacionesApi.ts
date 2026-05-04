import { deleteJson, getJson, postJson, requestJson } from './http'
import type { AutomationRequest, AutomationResponse, AutomationTestResult } from '../types/automatizaciones'

export function getAutomatizaciones() {
  return getJson<AutomationResponse>('/api/automatizaciones')
}

export function createAutomatizacion(input: AutomationRequest) {
  return postJson<{ id: number; message: string }>('/api/automatizaciones', input)
}

export function updateAutomatizacion(id: number, input: AutomationRequest) {
  return requestJson<{ message: string }>(`/api/automatizaciones/${id}`, 'PUT', input)
}

export function deleteAutomatizacion(id: number) {
  return deleteJson<{ message: string }>(`/api/automatizaciones/${id}`)
}

export function testAutomatizacion(input: { tipoEvento: string; entidadTipo: string; entidadId?: number; datos: Record<string, string | number | boolean | null> }) {
  return postJson<AutomationTestResult>('/api/automatizaciones/probar', input)
}
