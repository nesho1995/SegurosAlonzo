import { deleteJson, getJson, postJson, requestJson } from './http'
import type { UsersResponse } from '../types/auth'

export function getUsuarios() {
  return getJson<UsersResponse>('/api/usuarios')
}

export function createUsuario(input: { username: string; password: string; roleId: number }) {
  return postJson<{ id: number; message: string }>('/api/usuarios', input)
}

export function updateUsuario(id: number, input: { roleId: number; isActive: boolean }) {
  return requestJson<{ message: string }>(`/api/usuarios/${id}`, 'PUT', input)
}

export function resetUsuarioPassword(id: number, newPassword: string) {
  return postJson<{ message: string }>(`/api/usuarios/${id}/password`, { newPassword })
}

export function updateUsuarioPermissions(id: number, permissions: string[]) {
  return requestJson<{ message: string }>(`/api/usuarios/${id}/permissions`, 'PUT', { permissions })
}

export function deleteUsuario(id: number) {
  return deleteJson<void>(`/api/usuarios/${id}`)
}
