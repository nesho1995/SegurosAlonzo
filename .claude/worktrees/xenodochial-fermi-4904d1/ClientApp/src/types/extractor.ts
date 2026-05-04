import type { DetectedWorkshop, WorkshopSuggestion } from './talleres'
export type ExtractorConfig = { remitentesPermitidos: string; palabrasClaveAsunto: string; aseguradorasReglas: string; camposObligatorios: string; plantillaWhatsApp: string }
export type ExtractorTestRequest = { remitente: string; asunto: string; cuerpo: string }
export type ExtractorResult = { textoOriginal: string; camposDetectados: Record<string, string>; camposFaltantes: string[]; confianza: number; mensaje: string; talleresSugeridos: WorkshopSuggestion[]; tallerDetectado?: DetectedWorkshop }
export const extractorEmailExample: ExtractorTestRequest = { remitente: 'reclamos@aseguradora.com', asunto: 'Notificacion de reclamo auto Ficohsa', cuerpo: 'Nombre: Juan Perez\nTelefono: 99999999\nLugar: Tegucigalpa\nFecha: 27/04/2026\nPoliza: AUTO-12345\nPlaca: HAA1234\nTaller: Taller Central\nDescripcion: Colision leve en boulevard principal.' }
