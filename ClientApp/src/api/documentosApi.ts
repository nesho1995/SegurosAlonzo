import { ApiError, getJson, sendJson } from './http'
import type { DocumentItem, EntityType } from '../types/documentos'

export function getDocumentos(entidadTipo: EntityType, entidadId: number) {
  return getJson<{ items: DocumentItem[] }>(`/api/documentos/${entidadTipo}/${entidadId}`)
}

export async function uploadDocumento(entidadTipo: EntityType, entidadId: number, tipoDocumento: string, file: File, observacion?: string) {
  const body = new FormData()
  body.append('archivo', file)
  body.append('entidadTipo', entidadTipo)
  body.append('entidadId', String(entidadId))
  body.append('tipoDocumento', tipoDocumento || 'General')
  if (observacion?.trim()) body.append('observacion', observacion.trim())

  const response = await fetch('/api/documentos/upload', {
    method: 'POST',
    credentials: 'include',
    body,
  })

  if (response.status === 401 || response.redirected) {
    window.dispatchEvent(new CustomEvent('app:unauthorized'))
    throw new ApiError('No has iniciado sesion.', 401)
  }

  if (!response.ok) {
    if (response.status === 403) window.dispatchEvent(new CustomEvent('app:forbidden'))
    throw new ApiError(await readDocumentError(response, 'No se pudo subir el documento.'), response.status)
  }

  return (await response.json()) as DocumentItem
}

export function updateDocumentoObservacion(id: number, observacion: string) {
  return sendJson(`/api/documentos/${id}/observacion`, 'PUT', { observacion })
}

export async function deleteDocumento(id: number) {
  const response = await fetch(`/api/documentos/${id}`, {
    method: 'DELETE',
    credentials: 'include',
  })

  if (response.status === 401 || response.redirected) {
    window.dispatchEvent(new CustomEvent('app:unauthorized'))
    throw new ApiError('No has iniciado sesion.', 401)
  }

  if (!response.ok) {
    if (response.status === 403) window.dispatchEvent(new CustomEvent('app:forbidden'))
    throw new ApiError(await readDocumentError(response, 'No se pudo eliminar el documento.'), response.status)
  }
}

async function readDocumentError(response: Response, fallback: string) {
  try {
    const json = await response.json() as { error?: string; message?: string }
    return json.error || json.message || fallback
  } catch {
    return fallback
  }
}
