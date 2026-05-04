export type AutomationCondition = {
  id?: number
  automatizacionId?: number
  campo: string
  operador: string
  valor?: string
}

export type AutomationAction = {
  id?: number
  automatizacionId?: number
  tipoAccion: string
  parametrosJson?: string
}

export type AutomationRule = {
  id: number
  nombre: string
  activo: boolean
  tipoEvento: string
  empresaId?: number
  fechaCreacion: string
  condiciones: AutomationCondition[]
  acciones: AutomationAction[]
}

export type AutomationLog = {
  id: number
  automatizacionId: number
  automatizacion: string
  entidadTipo: string
  entidadId?: number
  resultado: string
  mensaje: string
  fecha: string
}

export type AutomationRequest = {
  nombre: string
  activo: boolean
  tipoEvento: string
  empresaId?: number
  condiciones: AutomationCondition[]
  acciones: AutomationAction[]
}

export type AutomationResponse = {
  items: AutomationRule[]
  logs: AutomationLog[]
}

export type AutomationTestResult = {
  reglasEvaluadas: number
  reglasCoincidentes: number
  mensajes: string[]
}
