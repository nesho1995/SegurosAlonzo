import { getJson, sendJson } from './http'

export type ComisionLote = { id: number; aseguradora?: string; archivoNombre: string; fechaCarga: string; estado: string }
export type ComisionDetalle = {
  id: number
  loteId: number
  clienteDetectado?: string
  polizaDetectada?: string
  aseguradoraDetectada?: string
  primaDetectada?: number
  porcentajeDetectado?: number
  comisionDetectada?: number
  comisionEsperada?: number
  diferencia?: number
  estado: string
  observaciones?: string
  revisado: boolean
}

export function getComisiones(loteId?: number) {
  return getJson<{ lotes: ComisionLote[]; detalles: ComisionDetalle[] }>(`/api/comisiones${loteId ? `?loteId=${loteId}` : ''}`)
}

export async function previewComisiones(file: File) {
  const body = new FormData()
  body.append('archivo', file)
  const response = await fetch('/api/comisiones/preview', { method: 'POST', credentials: 'include', body })
  if (!response.ok) throw new Error('No se pudo revisar el archivo.')
  return await response.json() as { items: ComisionDetalle[] }
}

export async function importarComisiones(file: File) {
  const body = new FormData()
  body.append('archivo', file)
  const response = await fetch('/api/comisiones/importar', { method: 'POST', credentials: 'include', body })
  if (!response.ok) throw new Error('No se pudo cargar el reporte.')
  return await response.json() as { loteId: number }
}

export function marcarComisionRevisada(id: number) {
  return sendJson(`/api/comisiones/detalle/${id}/revisado`, 'PATCH', {})
}
