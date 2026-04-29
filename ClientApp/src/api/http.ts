export class ApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function readError(response: Response, fallback: string) {
  try {
    const json = await response.json() as { error?: string; message?: string }
    return json.error || json.message || fallback
  } catch {
    const text = await response.text()
    return text || fallback
  }
}

function emitAuthEvent(status: number) {
  if (status === 401) {
    try {
      window.sessionStorage.setItem('app:auth-message', 'Tu sesion expiro. Inicia sesion nuevamente.')
    } catch {
      // ignore sessionStorage access issues
    }
    window.dispatchEvent(new CustomEvent('app:unauthorized'))
  }
  if (status === 403) window.dispatchEvent(new CustomEvent('app:forbidden'))
}

export async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { credentials: 'include' })
  if (response.status === 401 || response.redirected) {
    emitAuthEvent(401)
    throw new ApiError('No has iniciado sesion.', 401)
  }
  if (!response.ok) {
    emitAuthEvent(response.status)
    throw new ApiError(await readError(response, `No se pudo cargar la informacion (${response.status}).`), response.status)
  }
  return (await response.json()) as T
}
export async function requestJson<T = unknown>(url: string, method: 'POST' | 'PUT' | 'PATCH', body: unknown): Promise<T | null> {
  const response = await fetch(url, { method, credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  if (response.status === 401 || response.redirected) {
    emitAuthEvent(401)
    throw new ApiError('No has iniciado sesion.', 401)
  }
  if (!response.ok) {
    emitAuthEvent(response.status)
    throw new ApiError(await readError(response, `No se pudo guardar (${response.status}).`), response.status)
  }
  if (response.status === 204) return null
  return (await response.json()) as T
}
export async function sendJson(url: string, method: 'PUT' | 'PATCH', body: unknown): Promise<void> { await requestJson(url, method, body) }
export async function postJson<T = unknown>(url: string, body: unknown): Promise<T | null> { return requestJson<T>(url, 'POST', body) }
export async function deleteJson<T = unknown>(url: string): Promise<T | null> {
  const response = await fetch(url, { method: 'DELETE', credentials: 'include' })
  if (response.status === 401 || response.redirected) {
    emitAuthEvent(401)
    throw new ApiError('No has iniciado sesion.', 401)
  }
  if (!response.ok) {
    emitAuthEvent(response.status)
    throw new ApiError(await readError(response, `No se pudo eliminar (${response.status}).`), response.status)
  }
  if (response.status === 204) return null
  return (await response.json()) as T
}
export async function uploadJson<T = unknown>(url: string, file: File): Promise<T> {
  const body = new FormData(); body.append('archivo', file)
  const response = await fetch(url, { method: 'POST', credentials: 'include', body })
  if (response.status === 401 || response.redirected) {
    emitAuthEvent(401)
    throw new ApiError('No has iniciado sesion.', 401)
  }
  if (!response.ok) {
    emitAuthEvent(response.status)
    throw new ApiError(await readError(response, `No se pudo procesar el archivo (${response.status}).`), response.status)
  }
  return (await response.json()) as T
}
