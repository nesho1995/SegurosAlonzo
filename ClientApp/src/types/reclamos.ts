export type ClaimItem = {
  id: number
  asunto?: string
  aseguradora?: string
  asegurado?: string
  poliza?: string
  placa?: string
  reclamo?: string
  conductor?: string
  celular?: string
  fechaNotificacion?: string
  lugarAccidente?: string
  estado: string
  estadoReclamo?: string
  tipoReclamo?: string
  numeroReclamo?: string
  montoEstimado?: number
  montoAprobado?: number
  montoPagado?: number
  documentosPendientes?: number
  ciudadDetectada?: string
  tallerSugeridoId?: number
  tallerAsignadoId?: number
  motivoSugerenciaTaller?: string
  correoAseguradoraPrincipal?: string
  correoAseguradoraCopia?: string
  respuestaAseguradora?: string
  fechaRespuestaAseguradora?: string
  aseguradoraAprobado?: boolean
  fechaCreacion: string
}

export type ClaimsResponse = {
  items: ClaimItem[]
  total: number
}

export type ClaimChecklistItem = {
  id: number
  tipoReclamo: string
  tipoDocumento: string
  requerido: boolean
  activo: boolean
}
