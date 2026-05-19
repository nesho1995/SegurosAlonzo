export type ReporteReclamoItem = {
  id: number
  reclamo?: string
  poliza?: string
  placa?: string
  conductor?: string
  celular?: string
  asegurado?: string
  estado: string
  ciudadDetectada?: string
  descripcion?: string
  fechaCreacion: string
  fechaUltimoRecordatorio?: string
  cantidadRecordatorios: number
  documentosPendientes: number
  documentosRecibidos: number
  eventosPeriodo: number
  ultimoMovimientoFecha?: string
  ultimoMovimientoAccion?: string
  ultimoMovimientoDescripcion?: string
  ultimoMovimientoUsuario?: string
}

export type ReporteReclamosResumen = {
  total: number
  conPendientes: number
  sinMovimientoPeriodo: number
  conTelefono: number
  sinTelefono: number
}

export type ReporteReclamosResponse = {
  items: ReporteReclamoItem[]
  total: number
  resumen: ReporteReclamosResumen
}
