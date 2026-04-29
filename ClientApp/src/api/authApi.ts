import { getJson, postJson } from './http'
import type { AuthUser, ChangePasswordRequest, LoginRequest } from '../types/auth'

export function getCurrentUser() {
  return getJson<AuthUser>('/api/auth/me')
}

export function login(request: LoginRequest) {
  return postJson<AuthUser>('/api/auth/login', request)
}

export function logout() {
  return postJson<{ message: string }>('/api/auth/logout', {})
}

export function changePassword(request: ChangePasswordRequest) {
  return postJson<{ message: string }>('/api/auth/cambiar-password', request)
}
