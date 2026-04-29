import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { KeyRound, Save, Trash2, UserPlus } from 'lucide-react'
import { createUsuario, deleteUsuario, getUsuarios, resetUsuarioPassword, updateUsuario, updateUsuarioPermissions } from '../api/usuariosApi'
import { CellTitle, DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import type { Role, UserAdmin } from '../types/auth'

export function UsuariosView() {
  const [usuarios, setUsuarios] = useState<UserAdmin[]>([])
  const [roles, setRoles] = useState<Role[]>([])
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [roleId, setRoleId] = useState(0)
  const [resetPassword, setResetPassword] = useState('')
  const [permissionsCatalog, setPermissionsCatalog] = useState<string[]>([])
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null)
  const [permissionDraft, setPermissionDraft] = useState<string[]>([])
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const data = await getUsuarios()
      setUsuarios(data.usuarios)
      setRoles(data.roles)
      setPermissionsCatalog(data.permisosDisponibles || [])
      setRoleId((current) => current || data.roles[0]?.id || 0)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar usuarios.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    getUsuarios()
      .then((data) => {
        if (!alive) return
        setUsuarios(data.usuarios)
        setRoles(data.roles)
        setPermissionsCatalog(data.permisosDisponibles || [])
        setRoleId((current) => current || data.roles[0]?.id || 0)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'No se pudieron cargar usuarios.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [])

  async function create(event: FormEvent) {
    event.preventDefault()
    setMessage(null)
    setError(null)
    try {
      await createUsuario({ username, password, roleId })
      setUsername('')
      setPassword('')
      setMessage('Usuario creado.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el usuario.')
    }
  }

  async function saveUser(user: UserAdmin) {
    setMessage(null)
    setError(null)
    try {
      await updateUsuario(user.id, { roleId: user.roleId, isActive: user.isActive })
      setMessage('Usuario actualizado.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar el usuario.')
    }
  }

  async function resetUserPassword(user: UserAdmin) {
    if (!resetPassword.trim()) {
      setError('Ingresa una nueva contrasena temporal.')
      return
    }
    setMessage(null)
    setError(null)
    try {
      await resetUsuarioPassword(user.id, resetPassword)
      setResetPassword('')
      setMessage(`Contrasena actualizada para ${user.username}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar la contrasena.')
    }
  }

  async function savePermissions(user: UserAdmin) {
    setMessage(null)
    setError(null)
    try {
      await updateUsuarioPermissions(user.id, permissionDraft)
      setMessage(`Permisos actualizados para ${user.username}.`)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron guardar permisos.')
    }
  }

  async function removeUser(user: UserAdmin) {
    if (!window.confirm(`Confirmas eliminar el usuario ${user.username}?`)) return
    setMessage(null)
    setError(null)
    try {
      await deleteUsuario(user.id)
      setMessage(`Usuario ${user.username} eliminado.`)
      if (selectedUserId === user.id) {
        setSelectedUserId(null)
        setPermissionDraft([])
      }
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar el usuario.')
    }
  }

  return (
    <>
      <PageHeader eyebrow="Administracion" title="Usuarios y roles" description="Gestiona accesos del equipo con permisos auditados." onRefresh={load} />
      {message && <div className="inline-alert success">{message}</div>}
      {error && <ErrorCard text={error} />}
      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Usuarios" subtitle="Edita rol, activa o inactiva cuentas y define claves temporales." />
          {loading ? <LoadingCard text="Cargando usuarios..." /> : (
            <DataTable
              headers={['Usuario', 'Rol', 'Estado', 'Acciones']}
              rows={usuarios.map((user) => [
                <CellTitle title={user.username} subtitle={`ID ${user.id}`} />,
                <select value={user.roleId} onChange={(event) => setUsuarios((items) => items.map((item) => item.id === user.id ? { ...item, roleId: Number(event.target.value) } : item))}>
                  {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
                </select>,
                <label className="check-field"><input type="checkbox" checked={user.isActive} onChange={(event) => setUsuarios((items) => items.map((item) => item.id === user.id ? { ...item, isActive: event.target.checked } : item))} />Activo</label>,
                <div className="table-actions">
                  <button className="icon-button secondary" onClick={() => saveUser(user)}><Save size={16} />Guardar</button>
                  <button className="icon-button secondary" onClick={() => resetUserPassword(user)}><KeyRound size={16} />Clave</button>
                  <button
                    className="icon-button secondary"
                    onClick={() => {
                      setSelectedUserId(user.id)
                      setPermissionDraft(user.customPermissions || [])
                    }}
                  >
                    Permisos
                  </button>
                  <button className="icon-button danger-button" onClick={() => void removeUser(user)}><Trash2 size={16} />Eliminar</button>
                </div>,
              ])}
            />
          )}
        </article>
        <aside className="panel">
          <PanelTitle title="Nuevo usuario" subtitle="Crea usuarios con contrasena temporal segura." />
          <form className="form-grid single-column" onSubmit={create}>
            <label className="field"><span>Usuario</span><input value={username} onChange={(event) => setUsername(event.target.value)} /></label>
            <label className="field"><span>Contrasena temporal</span><input type="password" value={password} onChange={(event) => setPassword(event.target.value)} /></label>
            <label className="field"><span>Rol</span><select value={roleId} onChange={(event) => setRoleId(Number(event.target.value))}>{roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}</select></label>
            <button className="primary-button wide-field" type="submit"><UserPlus size={16} />Crear usuario</button>
          </form>
          <div className="documents-panel">
            <label className="field compact-field"><span>Nueva clave para reinicio</span><input type="password" value={resetPassword} onChange={(event) => setResetPassword(event.target.value)} /></label>
          </div>
        </aside>
      </section>
      {selectedUserId && (
        <article className="panel mt-panel">
          <PanelTitle
            title="Permisos personalizados por usuario"
            subtitle="Si guardas aqui, este usuario usara estos permisos en lugar de los del rol."
          />
          <div className="form-grid">
            {permissionsCatalog.map((permission) => (
              <label className="check-field" key={permission}>
                <input
                  type="checkbox"
                  checked={permissionDraft.includes(permission)}
                  onChange={(event) => {
                    if (event.target.checked) setPermissionDraft((items) => [...items, permission])
                    else setPermissionDraft((items) => items.filter((item) => item !== permission))
                  }}
                />
                {permission}
              </label>
            ))}
            <div className="form-actions wide-field">
              <button
                className="primary-button"
                onClick={() => {
                  const target = usuarios.find((item) => item.id === selectedUserId)
                  if (target) void savePermissions(target)
                }}
              >
                <Save size={16} />Guardar permisos
              </button>
            </div>
          </div>
        </article>
      )}
    </>
  )
}
