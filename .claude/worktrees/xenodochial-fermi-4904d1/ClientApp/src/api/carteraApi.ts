import { getJson, postJson, sendJson } from './http'
import type { Client, ClientDetailResponse, ClientListResponse, Policy, PolicyInstallment } from '../types/cartera'
export function getClientes(params: URLSearchParams) { return getJson<ClientListResponse>(`/api/cartera/clientes?${params}`) }
export function getClienteDetalle(id: number) { return getJson<ClientDetailResponse>(`/api/cartera/clientes/${id}`) }
export function getPolizaCuotas(polizaId: number) { return getJson<{ items: PolicyInstallment[] }>(`/api/cartera/polizas/${polizaId}/cuotas`) }
export function updatePolizaCuotaMonto(cuotaId: number, monto: number) { return sendJson(`/api/cartera/cuotas/${cuotaId}/monto`, 'PATCH', { monto }) }
export function createCliente(client: Client) { return postJson<{ id: number }>('/api/CarteraMutaciones/NuevoCliente', client) }
export function updateCliente(client: Client) { return sendJson(`/api/CarteraMutaciones/GuardarCliente/${client.id}`, 'PUT', client) }
export function updatePoliza(policy: Policy) { return sendJson(`/api/CarteraMutaciones/GuardarPoliza/${policy.id}`, 'PUT', policy) }
export function createPoliza(clienteId: number, policy: Policy) { return postJson(`/api/CarteraMutaciones/NuevaPoliza/${clienteId}`, policy) }
export function cambiarEstadoPoliza(policy: Policy) { return sendJson(`/api/CarteraMutaciones/CambiarEstadoPoliza/${policy.id}`, 'PATCH', { activo: !policy.activo }) }
export function cambiarEstadoCliente(client: Client) { return sendJson(`/api/CarteraMutaciones/CambiarEstadoCliente/${client.id}`, 'PATCH', { activo: !client.activo }) }
export function marcarClienteRevisado(clienteId: number) { return sendJson(`/api/CarteraMutaciones/MarcarClienteRevisado/${clienteId}`, 'PATCH', {}) }
export function reanalizarPolizaHistorica(polizaId: number) { return sendJson(`/api/CarteraMutaciones/ReanalizarPolizaHistorica/${polizaId}`, 'PATCH', {}) }
