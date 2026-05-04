import { getJson } from './http'
import type { DashboardResponse } from '../types/dashboard'
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
