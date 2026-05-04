export type CatalogoItem = {
  id: number
  tipoCatalogo: string
  codigo: string
  nombre: string
  descripcion?: string
  activo: boolean
  orden: number
  esDefault: boolean
  pendienteRevision: boolean
  fechaCreacion?: string
  fechaActualizacion?: string
}
