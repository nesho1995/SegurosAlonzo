import { useState } from 'react'
import type { FormEvent } from 'react'
import { changePassword } from '../api/authApi'
import { PageHeader } from '../components/Topbar'

export function CambiarPasswordView() {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setMessage(null)
    setError(null)
    setSaving(true)
    try {
      const response = await changePassword({ currentPassword, newPassword, confirmPassword })
      setMessage(response?.message || 'Contrasena actualizada.')
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar la contrasena.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <PageHeader eyebrow="Seguridad" title="Cambiar contrasena" description="Actualiza tu clave de acceso de forma segura." onRefresh={() => {}} />
      <form className="panel form-grid" onSubmit={submit}>
        {message && <div className="inline-alert success wide-field">{message}</div>}
        {error && <div className="inline-alert danger wide-field">{error}</div>}
        <label className="field">
          <span>Contrasena actual</span>
          <input type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} autoComplete="current-password" />
        </label>
        <label className="field">
          <span>Nueva contrasena</span>
          <input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} autoComplete="new-password" />
        </label>
        <label className="field">
          <span>Confirmar contrasena</span>
          <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" />
        </label>
        <div className="form-actions">
          <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Guardando...' : 'Guardar cambio'}</button>
        </div>
      </form>
    </>
  )
}
