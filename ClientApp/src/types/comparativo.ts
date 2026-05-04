export interface Comparativo {
  id: number
  cliente: string
  vehiculo?: string
  notas?: string
  estado: 'borrador' | 'listo'
  creadoEn: string
  empresaId: number
  usuarioId: number
}

export interface ComparativoItem {
  id: number
  comparativoId: number
  aseguradora: string
  nombreArchivo?: string

  primaAnual?: number
  primaMensual?: number
  primaContado?: number
  descuentoContado?: number
  descuentoEsPorcentaje: boolean
  recargoFinanciamiento?: number
  recargoEsPorcentaje: boolean
  primaFinanciada?: number
  formaPago?: string

  sumaAsegurada?: number
  deducibleColision?: number
  deducibleColisionEsPorcentaje: boolean
  deducibleRobo?: number
  deducibleRoboEsPorcentaje: boolean

  vigenciaDesde?: string
  vigenciaHasta?: string

  coberturas: string[]
  exclusiones: string[]

  score?: number
  posicion?: number
  ahorroContado?: number

  creadoEn: string
}

export interface ComparativoDetalle {
  comparativo: Comparativo
  items: ComparativoItem[]
}

export interface ComparativoListResponse {
  items: Comparativo[]
  total: number
}
