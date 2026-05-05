export type ReclamoCorreoConfig = {
  emailEnabled: boolean
  workerEnabled: boolean
  mailbox: string
  markAsRead: boolean
  lookbackHours: number
  host: string
  port: number
  useSsl: boolean
  username: string
  password?: string
  passwordMasked?: string
}

export type ReclamoWorkerEstado = {
  ultimaEjecucionUtc?: string
  ultimoError?: string
  correosEncontrados: number
  reclamosValidos: number
  correosProcesados: number
  correosIgnorados: number
  correosDuplicados: number
  correosConError: number
  detalles: CorreoProcesamientoDetalle[]
}

export type CorreoProcesamientoDetalle = {
  subject: string
  messageId: string
  estado: string
  motivo: string
  reclamoId?: number
}

export type CorreoReclamoPatron = {
  id: number
  nombre: string
  activo: boolean
  prioridad: number
  campoDestino: string
  fuente: 'SUBJECT' | 'BODY' | 'SUBJECT_BODY'
  tipoRegla: 'REGEX' | 'CONTIENE' | 'EMPIEZA_CON' | 'TERMINA_CON'
  patron: string
  grupoRegex?: string
  requerido: boolean
  normalizarTexto: boolean
  descripcion?: string
}

export type CorreoReclamoPlantilla = {
  id: number
  nombre: string
  activa: boolean
  prioridad: number
  descripcion?: string
}

export type ProbarPatronesResult = {
  plantillaId?: number
  plantillaNombre: string
  plantillaCumple: boolean
  camposDetectados: Record<string, string>
  camposFaltantes: string[]
  reglaQueDetectoPorCampo: Record<string, string>
}
