export type PaymentStats = { pendientes: number; vencidas: number; parciales: number; pagadas: number; montoPendiente: number }
export type Payment = { id: number; cliente: string; telefono?: string; numeroPoliza?: string; aseguradora?: string; ramo?: string; numeroCuota: number; fechaVencimiento: string; monto: number; montoPagado: number; metodoPago?: string; estado: string; documentoId?: number; numeroRecibo?: string; referenciaBanco?: string }
export type PaymentMovement = { id: number; cuotaId: number; monto: number; fechaPago: string; metodoPago: string; documentoId?: number; numeroRecibo?: string; referenciaBanco?: string; observaciones?: string }
export type PaymentPolicyAlert = { polizaId: number; numeroPoliza?: string; cliente: string; cuotas?: number; primaTotal?: number }
export type PaymentsResponse = { items: Payment[]; total: number; stats: PaymentStats; alertas?: PaymentPolicyAlert[] }
