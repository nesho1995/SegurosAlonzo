import { deleteJson, getJson, postJson, sendJson } from './http'

export type Gasto = {
  id: number
  fecha: string
  categoria: string
  descripcion: string
  proveedor?: string
  monto: number
  moneda: string
  metodoPago?: string
  referencia?: string
  documentoId?: number
  estado: string
  activo: boolean
}

export type GastoResponse = {
  items: Gasto[]
  total: number
  totalRango: number
  resumen: { totalMes: number; porCategoria: Array<{ categoria: string; total: number }> }
}

export function getGastos(params: URLSearchParams) {
  return getJson<GastoResponse>(`/api/gastos?${params}`)
}

export function createGasto(gasto: Gasto) {
  return postJson<{ id: number }>('/api/gastos', gasto)
}

export function updateGasto(gasto: Gasto) {
  return sendJson(`/api/gastos/${gasto.id}`, 'PUT', gasto)
}

export function deleteGasto(gastoId: number) {
  return deleteJson(`/api/gastos/${gastoId}`)
}
