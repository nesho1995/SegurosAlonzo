import { getJson } from './http'
import type { DashboardResponse, DashboardGraficos } from '../types/dashboard'

export type DashboardFilters = { desde?: string; hasta?: string; aseguradora?: string; ciudad?: string }

export function getDashboard(filters: DashboardFilters = {}) {
  const query = new URLSearchParams()
  if (filters.desde) query.set('desde', filters.desde)
  if (filters.hasta) query.set('hasta', filters.hasta)
  if (filters.aseguradora) query.set('aseguradora', filters.aseguradora)
  if (filters.ciudad) query.set('ciudad', filters.ciudad)
  const qs = query.toString()
  return getJson<DashboardResponse>(`/api/dashboard${qs ? `?${qs}` : ''}`)
}

export function getDashboardGraficos() {
  return getJson<DashboardGraficos>('/api/dashboard/graficos')
}

export type TareaPoliza = { id: number; cliente: string; telefono?: string; numeroPoliza?: string; aseguradora?: string; ramo?: string; hasta: string; diasRestantes: number }
export type TareaCuota = { id: number; cliente: string; telefono?: string; numeroPoliza?: string; aseguradora?: string; numeroCuota: number; fechaVencimiento: string; monto: number; diasVencida: number }
export type TareaReclamo = { id: number; cliente?: string; aseguradora?: string; asegurado?: string; estado: string; fechaCreacion: string }
export type TareasHoy = { polizasVencen: TareaPoliza[]; cuotasVencidas: TareaCuota[]; reclamosPendientes: TareaReclamo[] }

export function getTareasHoy() {
  return getJson<TareasHoy>('/api/dashboard/tareas')
}

export type BusquedaResult = {
  clientes: Array<{ id: number; nombre: string; telefono: string; polizasActivas: number }>
  polizas: Array<{ id: number; codigo: string; cliente: string; aseguradora: string; hasta: string; activo: boolean; estado: string }>
}

export function buscar(q: string) {
  return getJson<BusquedaResult>(`/api/busqueda?q=${encodeURIComponent(q)}`)
}
