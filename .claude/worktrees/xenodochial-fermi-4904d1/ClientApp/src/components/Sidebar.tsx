import type { LucideIcon } from 'lucide-react'
import { KeyRound, LogOut, ShieldCheck } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { getEmpresaConfiguracion, type EmpresaConfiguracion } from '../api/configuracionApi'
import type { View } from '../types/common'
export type NavItem = { id: View; label: string; icon: LucideIcon; section?: string }
export function Sidebar({ navItems, view, onViewChange }: { navItems: NavItem[]; view: View; onViewChange: (view: View) => void }) {
  const { user, signOut } = useAuth()
  const [empresa, setEmpresa] = useState<EmpresaConfiguracion | null>(null)
  const [logoVersion, setLogoVersion] = useState(0)

  useEffect(() => {
    let alive = true
    const load = async () => {
      try {
        const data = await getEmpresaConfiguracion()
        if (alive) setEmpresa(data)
      } catch {
        // keep fallback brand
      }
    }

    const onBrandUpdated = () => {
      setLogoVersion((v) => v + 1)
      void load()
    }

    void load()
    window.addEventListener('app:empresa-updated', onBrandUpdated)
    return () => {
      alive = false
      window.removeEventListener('app:empresa-updated', onBrandUpdated)
    }
  }, [])

  return (
    <aside className="sidebar">
      <div className="brand">
        {empresa?.logoUrl ? <img src={`${empresa.logoUrl}?v=${logoVersion}`} alt="Logo empresa" style={{ width: 24, height: 24, objectFit: 'contain' }} /> : <ShieldCheck size={24} />}
        <div>
          <strong>{empresa?.nombreEmpresa || 'Correduria CRM'}</strong>
          <span>Gestion de seguros</span>
        </div>
      </div>
      <nav className="nav-list" aria-label="Modulos">
        {Array.from(new Set(navItems.map((item) => item.section || 'General'))).map((section) => (
          <div className="nav-group" key={section}>
            <div className="nav-group-title">{section}</div>
            {navItems
              .filter((item) => (item.section || 'General') === section)
              .map((item) => {
                const Icon = item.icon
                return (
                  <button className={view === item.id ? 'active' : ''} key={item.id} onClick={() => onViewChange(item.id)}>
                    <Icon size={18} />
                    <span>{item.label}</span>
                  </button>
                )
              })}
          </div>
        ))}
      </nav>
      <div className="session-box">
        <strong>{user?.username}</strong>
        <span>{user?.roles.join(', ')}</span>
        <button onClick={() => onViewChange('password')}><KeyRound size={16} />Cambiar clave</button>
        <button onClick={() => void signOut()}><LogOut size={16} />Salir</button>
      </div>
    </aside>
  )
}
