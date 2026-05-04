import { getJson, postJson, sendJson } from './http'
import type { CatalogoItem } from '../types/catalogos'

export function getCatalogoTipos() {
  return getJson<{ tipos: string[] }>('/api/catalogos')
}

export function getCatalogoByTipo(tipo: string, incluirInactivos = true) {
  return getJson<{ items: CatalogoItem[] }>(`/api/catalogos/${encodeURIComponent(tipo)}?incluirInactivos=${incluirInactivos}`)
}

export function saveCatalogoItem(item: Partial<CatalogoItem> & { tipoCatalogo: string; nombre: string }) {
  return postJson<{ id: number }>('/api/catalogos', item)
}

export function setCatalogoActivo(id: number, activo: boolean) {
  return sendJson(`/api/catalogos/${id}/activo`, 'PATCH', { activo })
}

export function mergeCatalogos(sourceId: number, targetId: number) {
  return postJson('/api/catalogos/merge', { sourceId, targetId })
}
