export type ImportFieldValue = { field: string; original: string; clean: string }
export type ImportIssue = { field: string; message: string }
export type NormalizedPhone = { e164: string; whatsAppCloud: string; whatsappReady: boolean; country: string }
export type PhoneNormalization = {
  original: string
  valid: NormalizedPhone[]
  invalid: string[]
  notes: string[]
  principal: string
  secondary: string
  extras: string[]
  principalWhatsApp: string
  whatsappReady: boolean
}
export type EmailNormalization = {
  original: string
  valid: string[]
  invalid: string[]
  suggestions: string[]
  principal: string
  extras: string[]
}
export type CarteraImportRow = {
  rowNumber: number
  values: ImportFieldValue[]
  errors: ImportIssue[]
  warnings: ImportIssue[]
  phoneNormalization: PhoneNormalization
  emailNormalization: EmailNormalization
  estado: 'VALIDA' | 'CON_ADVERTENCIAS' | 'ERROR'
}
export type CarteraImportPreview = {
  rows: CarteraImportRow[]
  totalRows: number
  errorCount: number
  warningCount: number
  hasCriticalErrors: boolean
}

export type ReclamoHistoricoImportRow = {
  rowNumber: number
  conductor: string
  cliente: string
  poliza: string
  reclamo: string
  vehiculo: string
  placa: string
  celular: string
  observaciones: string
  fechaNotificacion?: string
  duplicado: boolean
  documentosRecibidos: Record<string, boolean>
  errors: ImportIssue[]
  warnings: ImportIssue[]
}

export type ReclamoHistoricoImportPreview = {
  rows: ReclamoHistoricoImportRow[]
  totalRows: number
  errorCount: number
  warningCount: number
  importableCount: number
  duplicateCount: number
}
