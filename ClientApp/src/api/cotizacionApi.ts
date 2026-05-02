import { getJson, postJson, requestJson, deleteJson } from './http'
import type {
  CotizacionListResponse,
  CotizacionDetalle,
  Cotizacion,
  CotizacionItem,
  CotizacionCobertura,
  CotizacionExclusion,
  CotizacionAnalisis,
} from '../types/cotizacion'

// ─── Listado ──────────────────────────────────────────────────────────────────

export function getCotizaciones(
  estado?: string,
  buscar?: string,
  pagina = 1,
  pageSize = 25
) {
  const q = new URLSearchParams()
  if (estado && estado !== 'TODOS') q.set('estado', estado)
  if (buscar) q.set('buscar', buscar)
  q.set('pagina', String(pagina))
  q.set('pageSize', String(pageSize))
  return getJson<CotizacionListResponse>(`/api/cotizaciones?${q}`)
}

// ─── Detalle ──────────────────────────────────────────────────────────────────

export function getCotizacionDetalle(id: number) {
  return getJson<CotizacionDetalle>(`/api/cotizaciones/${id}`)
}

// ─── CRUD Cotización ──────────────────────────────────────────────────────────

export function crearCotizacion(body: Partial<Cotizacion>) {
  return postJson<{ id: number }>('/api/cotizaciones', body)
}

export function actualizarCotizacion(id: number, body: Partial<Cotizacion>) {
  return requestJson('/api/cotizaciones/' + id, 'PUT', body)
}

export function eliminarCotizacion(id: number) {
  return deleteJson(`/api/cotizaciones/${id}`)
}

// ─── Items ────────────────────────────────────────────────────────────────────

export function agregarItem(cotizacionId: number, body: Partial<CotizacionItem>) {
  return postJson<{ id: number }>(`/api/cotizaciones/${cotizacionId}/items`, body)
}

export function actualizarItem(cotizacionId: number, itemId: number, body: Partial<CotizacionItem>) {
  return requestJson(`/api/cotizaciones/${cotizacionId}/items/${itemId}`, 'PUT', body)
}

export function eliminarItem(cotizacionId: number, itemId: number) {
  return deleteJson(`/api/cotizaciones/${cotizacionId}/items/${itemId}`)
}

// ─── Coberturas / Exclusiones ────────────────────────────────────────────────

export function guardarCoberturas(
  cotizacionId: number,
  itemId: number,
  coberturas: Partial<CotizacionCobertura>[]
) {
  return postJson(`/api/cotizaciones/${cotizacionId}/items/${itemId}/coberturas`, coberturas)
}

export function guardarExclusiones(
  cotizacionId: number,
  itemId: number,
  exclusiones: Partial<CotizacionExclusion>[]
) {
  return postJson(`/api/cotizaciones/${cotizacionId}/items/${itemId}/exclusiones`, exclusiones)
}

// ─── Ranking ──────────────────────────────────────────────────────────────────

export function recalcularRanking(cotizacionId: number) {
  return postJson(`/api/cotizaciones/${cotizacionId}/recalcular`, {})
}

// ─── Análisis ─────────────────────────────────────────────────────────────────

export function guardarAnalisis(cotizacionId: number, body: Partial<CotizacionAnalisis>) {
  return postJson<{ id: number }>(`/api/cotizaciones/${cotizacionId}/analisis`, body)
}

// ─── Exportes ────────────────────────────────────────────────────────────────

export function urlExcelCotizacion(id: number) {
  return `/api/cotizaciones/${id}/excel`
}

export function urlPdfCotizacion(id: number) {
  return `/api/cotizaciones/${id}/pdf`
}
