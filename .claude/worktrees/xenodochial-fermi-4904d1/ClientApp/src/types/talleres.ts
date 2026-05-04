export type Workshop = {
  id: number
  nombre: string
  ciudad: string
  zona?: string
  direccion?: string
  telefono?: string
  whatsApp?: string
  email?: string
  contacto?: string
  aseguradora: string
  ramo?: string
  activo: boolean
  esPreferido?: boolean
  ordenPrioridad?: number
  observaciones?: string
  aseguradorasAceptadas: string[]
  ramosAtendidos: string[]
}
export type WorkshopSuggestion = Workshop & { criterio: string }
export type DetectedWorkshop = { id: number; nombre: string; ciudad?: string; aseguradora?: string; ramo?: string; telefono?: string; direccion?: string; textoOrigen: string; estado: string }
export type WorkshopImportRow = { fila: number; taller: Workshop; errores: string[] }
