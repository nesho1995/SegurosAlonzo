import { getJson, postJson } from './http'
import type { PaymentMovement, PaymentsResponse } from '../types/pagos'
export function getPagos(params: URLSearchParams) { return getJson<PaymentsResponse>(`/api/pagos?${params}`) }
export function getPagosCuota(cuotaId: number) { return getJson<{ items: PaymentMovement[] }>(`/api/pagos/cuotas/${cuotaId}/pagos`) }
export function registrarPagoCuota(cuotaId: number, body: { monto: number; fechaPago?: string; metodoPago?: string; documentoId?: number; numeroRecibo?: string; referenciaBanco?: string; observaciones?: string }) {
  return postJson<{ pagoId: number }>(`/api/pagos/cuotas/${cuotaId}/registrar`, body)
}
