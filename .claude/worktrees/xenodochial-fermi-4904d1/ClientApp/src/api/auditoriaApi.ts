import { getJson } from './http'
import type { AuditResponse } from '../types/auditoria'

export function getAuditoria(params: URLSearchParams) {
  return getJson<AuditResponse>(`/api/auditoria?${params}`)
}
