export type AuthUser = {
  usuarioId?: string
  username: string
  roles: string[]
  permisos: string[]
}

export type Role = {
  id: number
  name: string
}

export type UserAdmin = {
  id: number
  username: string
  roleId: number
  roleName: string
  isActive: boolean
  customPermissions?: string[]
}

export type UsersResponse = {
  usuarios: UserAdmin[]
  roles: Role[]
  permisosDisponibles: string[]
}

export type LoginRequest = {
  username: string
  password: string
}

export type ChangePasswordRequest = {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}
