import { uploadJson } from './http'
import type { WorkshopImportRow } from '../types/talleres'
import type { CarteraImportPreview } from '../types/cargaMasiva'
export function previewTalleres(file: File) { return uploadJson<{ items: WorkshopImportRow[] }>('/api/talleres/preview', file) }
export function importarTalleres(file: File) { return uploadJson<{ importados: number; rechazados: number }>('/api/talleres/importar', file) }
export function previewCartera(file: File) { return uploadJson<CarteraImportPreview>('/api/carga-masiva/cartera/preview', file) }
export function importarCartera(file: File) { return uploadJson<{ clientes: number; polizas: number; polizasDuplicadas: number; filasImportadas: number; filasRechazadas: number; advertencias: number }>('/api/carga-masiva/cartera/importar', file) }
export async function descargarReporteCartera(file: File) {
  return downloadCarteraFile('/api/carga-masiva/cartera/reporte', file)
}
export async function descargarExcelLimpioCartera(file: File) {
  return downloadCarteraFile('/api/carga-masiva/cartera/excel-limpio', file)
}
async function downloadCarteraFile(url: string, file: File) {
  const body = new FormData()
  body.append('archivo', file)
  const response = await fetch(url, { method: 'POST', credentials: 'include', body })
  if (!response.ok) throw new Error('No se pudo generar el archivo.')
  return await response.blob()
}
