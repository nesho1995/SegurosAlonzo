import type { ComparativoDetalle, ComparativoItem, ComparativoListResponse } from '../types/comparativo'

const base = '/api/comparativos'

async function req<T>(url: string, opts?: RequestInit): Promise<T> {
  const r = await fetch(url, { credentials: 'include', ...opts })
  if (!r.ok) {
    const j = await r.json().catch(() => ({ error: r.statusText }))
    throw new Error(j.error ?? 'Error inesperado')
  }
  return r.json()
}

export const getComparativos = (page = 1, pageSize = 20, q?: string) => {
  const p = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (q) p.set('q', q)
  return req<ComparativoListResponse>(`${base}?${p}`)
}

export const getComparativoDetalle = (id: number) =>
  req<ComparativoDetalle>(`${base}/${id}`)

export const crearComparativo = (body: { cliente: string; vehiculo?: string; notas?: string }) =>
  req<{ id: number }>(base, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const actualizarComparativo = (
  id: number,
  body: { cliente: string; vehiculo?: string; notas?: string; estado?: string },
) =>
  req<{ ok: boolean }>(`${base}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const eliminarComparativo = (id: number) =>
  req<{ ok: boolean }>(`${base}/${id}`, { method: 'DELETE' })

export const subirPdf = (comparativoId: number, file: File): Promise<ComparativoItem> => {
  const fd = new FormData()
  fd.append('archivo', file)
  return req<ComparativoItem>(`${base}/${comparativoId}/pdf`, { method: 'POST', body: fd })
}

export const actualizarItem = (comparativoId: number, item: ComparativoItem) =>
  req<{ ok: boolean }>(`${base}/${comparativoId}/items/${item.id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(item),
  })

export const eliminarItem = (comparativoId: number, itemId: number) =>
  req<{ ok: boolean }>(`${base}/${comparativoId}/items/${itemId}`, { method: 'DELETE' })

export const reprocesarItem = (comparativoId: number, itemId: number) =>
  req<import('../types/comparativo').ComparativoItem>(
    `${base}/${comparativoId}/items/${itemId}/reprocesar`, { method: 'POST' })

export const urlExcelComparativo = (id: number) => `${base}/${id}/excel`
export const urlPdfComparativo   = (id: number) => `${base}/${id}/pdf-reporte`
