export type NotificacionInterna = {
  id: number
  tipo: string
  titulo: string
  mensaje: string
  entidadTipo?: string
  entidadId?: number
  leida: boolean
  fechaCreacion: string
}
export type NotificacionesResponse = { items: NotificacionInterna[]; unread: number }
