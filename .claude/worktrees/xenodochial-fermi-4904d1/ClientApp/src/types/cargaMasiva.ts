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
