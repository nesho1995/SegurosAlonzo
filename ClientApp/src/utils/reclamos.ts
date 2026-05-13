const documentLabels: Record<string, string> = {
  FOTO_RECLAMO: 'Fotos del reclamo',
  LICENCIA: 'Licencia del conductor',
  TARJETA_CIRCULACION: 'Tarjeta de circulacion',
  COTIZACION_TALLER: 'Cotizacion de taller',
  INFORME_TALLER: 'Informe de taller',
  FINIQUITO: 'Finiquito',
  AVISO_ACCIDENTE: 'Aviso de accidente',
  PAGO_DEDUCIBLE: 'Pago de deducible',
  PAGO_RSA: 'Pago de RSA (restitucion de suma asegurada)',
}

export function reclamoDocumentLabel(value?: string) {
  if (!value) return 'Documento'
  const normalized = value.trim().toUpperCase().replaceAll(' ', '_')
  return documentLabels[normalized] || value.replaceAll('_', ' ').toLowerCase().replace(/^\w/, (ch) => ch.toUpperCase())
}

export function reclamoDocumentNote(value?: string) {
  const normalized = (value || '').toLowerCase()
  if (!normalized.includes('aviso de accidente')) return ''
  return 'Puede compartir este aviso al numero de servicio al cliente 89659690 para gestionar firma y sello, y agilizar el tramite con la aseguradora.'
}
