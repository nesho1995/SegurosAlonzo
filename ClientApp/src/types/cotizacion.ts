// ─── Cotización principal ────────────────────────────────────────────────────

export interface Cotizacion {
  id: number
  clienteId?: number | null
  clienteNombre: string
  ramo: string
  fechaInicio?: string | null
  /** BORRADOR | REVISION | ENVIADA | ACEPTADA | RECHAZADA */
  estado: string
  notas?: string | null
  creadoPor?: string | null
  fechaCreacion: string
  activo: boolean
}

// ─── Resumen para listado ────────────────────────────────────────────────────

export interface CotizacionResumen {
  id: number
  clienteId?: number | null
  clienteNombre: string
  ramo: string
  fechaInicio?: string | null
  estado: string
  notas?: string | null
  totalItems: number
  mejorPrima?: number | null
  creadoPor?: string | null
  fechaCreacion: string
}

// ─── Item / opción por aseguradora ──────────────────────────────────────────

export interface CotizacionItem {
  id: number
  cotizacionId: number
  aseguradora: string
  plan?: string | null
  primaAnual?: number | null
  primaMensual?: number | null
  /** MENSUAL | TRIMESTRAL | SEMESTRAL | ANUAL */
  frecuenciaPago: string
  sumaAsegurada?: number | null
  deducible?: number | null
  vigenciaMeses?: number | null
  notas?: string | null
  rankingPuntos?: number | null
  rankingPosicion?: number | null
  recomendado: boolean
  activo: boolean
  coberturas: CotizacionCobertura[]
  exclusiones: CotizacionExclusion[]
  archivos: CotizacionArchivo[]
}

// ─── Coberturas ──────────────────────────────────────────────────────────────

export interface CotizacionCobertura {
  id: number
  itemId: number
  nombre: string
  limite?: string | null
  aplica: boolean
}

// ─── Exclusiones ─────────────────────────────────────────────────────────────

export interface CotizacionExclusion {
  id: number
  itemId: number
  descripcion: string
}

// ─── Archivos ────────────────────────────────────────────────────────────────

export interface CotizacionArchivo {
  id: number
  itemId: number
  nombreArchivo: string
  rutaArchivo: string
  tipoMime?: string | null
  extraido: boolean
  fechaSubida: string
}

// ─── Análisis ────────────────────────────────────────────────────────────────

export interface CotizacionAnalisis {
  id: number
  cotizacionId: number
  analisisTexto?: string | null
  ventajasJson?: string | null
  desventajasJson?: string | null
  recomendacion?: string | null
  creadoPor?: string | null
  fechaCreacion: string
}

// ─── Detalle completo ────────────────────────────────────────────────────────

export interface CotizacionDetalle {
  cotizacion: Cotizacion
  items: CotizacionItem[]
  analisis?: CotizacionAnalisis | null
  clienteTelefono?: string | null
}

// ─── Respuesta de listado ────────────────────────────────────────────────────

export interface CotizacionListResponse {
  items: CotizacionResumen[]
  total: number
  pagina: number
  pageSize: number
  totalPaginas: number
}

// ─── Formularios ─────────────────────────────────────────────────────────────

export type NuevaCotizacion = Pick<Cotizacion, 'clienteId' | 'clienteNombre' | 'ramo' | 'fechaInicio' | 'notas'>

export type NuevoCotizacionItem = Omit<CotizacionItem, 'id' | 'cotizacionId' | 'rankingPuntos' | 'rankingPosicion' | 'recomendado' | 'activo' | 'coberturas' | 'exclusiones' | 'archivos'>

export const ESTADOS_COTIZACION = ['BORRADOR', 'REVISION', 'ENVIADA', 'ACEPTADA', 'RECHAZADA'] as const

export const FRECUENCIAS_PAGO = ['MENSUAL', 'TRIMESTRAL', 'SEMESTRAL', 'ANUAL'] as const
