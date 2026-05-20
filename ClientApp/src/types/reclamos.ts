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
  descripcion?: string
  ciudadDetectada?: string
  tallerSugeridoId?: number
  tallerAsignadoId?: number
  motivoSugerenciaTaller?: string
  correoAseguradoraPrincipal?: string
  correoAseguradoraCopia?: string
  respuestaAseguradora?: string
  fechaRespuestaAseguradora?: string
  aseguradoraAprobado?: boolean
  montoDeducible?: number
  montoRsa?: number
  monedaPagosFinales?: string
  estadoDeducible?: string
  estadoRsa?: string
  fechaSolicitudDeducible?: string
  fechaSolicitudRsa?: string
  estadoCotizaciones?: string
  cotizacionesNota?: string
  casoEspecial?: boolean
  casoEspecialNota?: string
  estadoSeguimiento?: string
  fechaUltimaRevision?: string
  usuarioUltimaRevisionId?: number
  pagosPendientes?: number
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

export type ClaimInsuranceResponse = {
  id: number
  reclamoId: number
  origen: string
  remitente?: string
  asunto?: string
  respuesta: string
  aprobado: boolean
  requiereRsa: boolean
  requiereDeducible: boolean
  montoRsa?: number
  montoDeducible?: number
  solicitaMasDocumentos: boolean
  aprobadoSinPagosFinales: boolean
  acciones?: string
  usuarioId?: number
  creadoEn: string
}
