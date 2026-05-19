import { getJson } from './http'
import type { ReporteReclamosResponse } from '../types/reportes'

export function getReporteReclamos(params: URLSearchParams) {
  return getJson<ReporteReclamosResponse>(`/api/reportes/reclamos?${params}`)
}
