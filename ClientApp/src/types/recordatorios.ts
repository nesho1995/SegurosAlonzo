export type Reminder = { id: number; tipo: string; cliente: string; telefono?: string; numeroPoliza?: string; aseguradora?: string; ramo?: string; fechaObjetivo?: string; asunto: string; estado: string }
export type ReminderResponse = { items: Reminder[]; total: number; stats: { pendientes: number; enviados: number; errores: number; descartados: number } }
