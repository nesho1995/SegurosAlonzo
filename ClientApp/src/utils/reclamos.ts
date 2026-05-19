const documentLabels: Record<string, string> = {
  FOTO_RECLAMO: 'Fotos del reclamo',
  LICENCIA: 'Licencia del conductor',
  TARJETA_CIRCULACION: 'Tarjeta de circulacion',
  COTIZACION_TALLER: 'Cotizacion de taller',
  INFORME_TALLER: 'Informe de taller',
  FINIQUITO: 'Finiquito',
  AVISO_ACCIDENTE: 'Aviso de accidente',
  PAGO_DEDUCIBLE: 'Comprobante de pago de deducible',
  COMPROBANTE_DE_DEDUCIBLE: 'Comprobante de pago de deducible',
  PAGO_RSA: 'Comprobante de pago de RSA',
  COMPROBANTE_DE_RSA: 'Comprobante de pago de RSA',
  PAGO_COASEGURO: 'Comprobante de pago de deducible',
  COMPROBANTE_DE_COASEGURO: 'Comprobante de pago de deducible',
}

export function reclamoDocumentLabel(value?: string) {
  if (!value) return 'Documento'
  const normalized = value.trim().toUpperCase().replaceAll(' ', '_')
  return documentLabels[normalized] || value.replaceAll('_', ' ').toLowerCase().replace(/^\w/, (ch) => ch.toUpperCase())
}
