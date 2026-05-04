export type EntityType = 'CLIENTE' | 'POLIZA' | 'CUOTA' | 'PAGO' | 'RECLAMO'

export type DocumentItem = {
  id: number
  entidadTipo: EntityType
  entidadId: number
  nombreArchivoOriginal: string
  tipoDocumento: string
  fechaSubida: string
  subidoPorUsuarioId?: number
  usuario: string
  tamanoBytes: number
  mimeType: string
  extension: string
  activo: boolean
  verUrl: string
  descargarUrl: string
  downloadUrl: string
}
