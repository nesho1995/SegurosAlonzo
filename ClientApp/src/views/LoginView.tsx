import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ShieldCheck } from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { getEmpresaConfiguracion, type EmpresaConfiguracion } from '../api/configuracionApi'

export function LoginView() {
  const { signIn } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice] = useState<string | null>(() => {
    try {
      const sessionNotice = window.sessionStorage.getItem('app:auth-message')
      if (sessionNotice) window.sessionStorage.removeItem('app:auth-message')
      return sessionNotice
    } catch {
      return null
    }
  })
  const [loading, setLoading] = useState(false)
  const [empresa, setEmpresa] = useState<EmpresaConfiguracion | null>(null)

  useEffect(() => {
    let alive = true
    getEmpresaConfiguracion()
      .then((data) => { if (alive) setEmpresa(data) })
      .catch(() => { if (alive) setEmpresa(null) })
    return () => { alive = false }
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await signIn(username, password)
      window.history.replaceState(null, '', '/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo iniciar sesion.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="auth-screen">
      <form className="auth-card" onSubmit={submit}>
        <div className="auth-mark">
          {empresa?.logoUrl ? <img src={`${empresa.logoUrl}?v=${empresa.fechaActualizacion || 'actual'}`} alt={empresa.nombreEmpresa} /> : <ShieldCheck size={32} />}
        </div>
        <span className="eyebrow">{empresa?.nombreEmpresa || 'Correduria CRM'}</span>
        <h1>Inicio de sesion</h1>
        <p>Accede con tu usuario para gestionar cartera, reclamos, documentos y auditoria.</p>
        {notice && <div className="inline-alert warning">{notice}</div>}
        {error && <div className="inline-alert danger">{error}</div>}
        <label className="field">
          <span>Usuario</span>
          <input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" />
        </label>
        <label className="field">
          <span>Contrasena</span>
          <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" />
        </label>
        <button className="primary-button" type="submit" disabled={loading}>{loading ? 'Ingresando...' : 'Ingresar'}</button>
      </form>
    </main>
  )
}
