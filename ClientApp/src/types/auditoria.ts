export type AuditLog = {
  id: number
  usuarioId?: number
  usuario?: string
  accion: string
  entidadTipo: string
  entidadId?: number
  descripcion: string
  fecha: string
  ip?: string
}

export type AuditResponse = {
  items: AuditLog[]
  total: number
  pagina: number
  pageSize: number
  totalPaginas: number
}
