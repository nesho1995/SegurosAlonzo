import { useEffect, useState } from 'react'
import { Bot, Building2, ClipboardList, CreditCard, FileClock, LayoutDashboard, ReceiptText, Send, Settings, Upload, UserCog, Users, Wrench } from 'lucide-react'
import './App.css'
import { AuthProvider } from './components/AuthProvider'
import { useAuth } from './hooks/useAuth'
import { Layout } from './components/Layout'
import type { NavItem } from './components/Sidebar'
import type { View } from './types/common'
import { DashboardView } from './views/DashboardView'
import { ClientsView } from './views/ClientesView'
import { ReclamosView } from './views/ReclamosView'
import { RemindersView } from './views/RecordatoriosView'
import { PaymentsView } from './views/PagosView'
import { ExtractorView } from './views/ExtractorView'
import { WorkshopsView } from './views/TalleresView'
import { BulkHelpView } from './views/CargaMasivaView'
import { AuditoriaView } from './views/AuditoriaView'
import { LoginView } from './views/LoginView'
import { UsuariosView } from './views/UsuariosView'
import { CambiarPasswordView } from './views/CambiarPasswordView'
import { AccessDeniedView } from './views/AccessDeniedView'
import { LoadingCard } from './components/LoadingState'
import { AutomationView } from './views/AutomationView'
import { GastosView } from './views/GastosView'
import { ConfiguracionEmpresaView } from './views/ConfiguracionEmpresaView'
import { AutomatizacionEnviosView } from './views/AutomatizacionEnviosView'
import { CatalogosView } from './views/CatalogosView'
import { ReclamosConfiguracionView } from './views/ReclamosConfiguracionView'

const navItems = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, section: 'General' },
  { id: 'clientes', label: 'Clientes', icon: Users, section: 'Cartera y Cobranza' },
  { id: 'pagos', label: 'Pagos', icon: CreditCard, section: 'Cartera y Cobranza' },
  { id: 'recordatorios', label: 'Recordatorios', icon: Send, section: 'Cartera y Cobranza' },
  { id: 'gastos', label: 'Gastos', icon: ReceiptText, section: 'Cartera y Cobranza' },
  { id: 'reclamos', label: 'Reclamos', icon: ClipboardList, section: 'Reclamos' },
  { id: 'talleres', label: 'Talleres', icon: Wrench, section: 'Reclamos' },
  { id: 'reclamos-config', label: 'Reclamos config', icon: Settings, section: 'Reclamos' },
  { id: 'automatizaciones', label: 'Automatizaciones', icon: Bot, section: 'Operacion' },
  { id: 'envios-auto', label: 'Automatizacion de envios', icon: Send, section: 'Operacion' },
  { id: 'carga', label: 'Carga masiva', icon: Upload, section: 'Operacion' },
  { id: 'extractor', label: 'Extractor', icon: Settings, section: 'Operacion' },
  { id: 'auditoria', label: 'Auditoria', icon: FileClock, section: 'Administracion' },
  { id: 'usuarios', label: 'Usuarios', icon: UserCog, section: 'Administracion' },
  { id: 'catalogos', label: 'Catalogos', icon: Settings, section: 'Administracion' },
  { id: 'configuracion', label: 'Empresa', icon: Building2, section: 'Administracion' },
] satisfies NavItem[]

function App() {
  return (
    <AuthProvider>
      <AppRouter />
    </AuthProvider>
  )
}

function AppRouter() {
  const [view, setView] = useState<View>(() => viewFromPath(window.location.pathname))
  const { user, loading, hasPermission } = useAuth()

  function navigate(nextView: View) {
    setView(nextView)
    window.history.pushState(null, '', pathFromView(nextView))
  }

  useEffect(() => {
    const onForbidden = () => setView('access-denied')
    const onUnauthorized = () => setView('dashboard')
    window.addEventListener('app:forbidden', onForbidden)
    window.addEventListener('app:unauthorized', onUnauthorized)
    return () => {
      window.removeEventListener('app:forbidden', onForbidden)
      window.removeEventListener('app:unauthorized', onUnauthorized)
    }
  }, [])

  if (loading) return <LoadingCard text="Validando sesion..." />
  if (!user) return <LoginView />
  const canAccessView = (target: View) => {
    if (target === 'auditoria') return hasPermission('auditoria.ver')
    if (target === 'extractor') return hasPermission('configuracion.administrar')
    if (target === 'usuarios') return hasPermission('usuarios.administrar')
    if (target === 'automatizaciones') return hasPermission('automatizaciones.ver')
    if (target === 'gastos') return hasPermission('gastos.ver')
    if (target === 'configuracion') return hasPermission('configuracion.administrar')
    if (target === 'envios-auto') return hasPermission('configuracion.administrar')
    if (target === 'catalogos') return hasPermission('configuracion.administrar')
    if (target === 'reclamos-config') return hasPermission('configuracion.administrar')
    return true
  }

  const allowedNavItems = navItems.filter((item) => {
    return canAccessView(item.id)
  })

  return (
    <Layout navItems={allowedNavItems} view={view} onViewChange={navigate}>
      {!canAccessView(view) && <AccessDeniedView />}
      {canAccessView(view) && (
        <>
      {view === 'dashboard' && <DashboardView />}
      {view === 'clientes' && <ClientsView />}
      {view === 'reclamos' && <ReclamosView />}
      {view === 'recordatorios' && <RemindersView />}
      {view === 'pagos' && <PaymentsView />}
      {view === 'gastos' && <GastosView />}
      {view === 'automatizaciones' && <AutomationView />}
      {view === 'extractor' && <ExtractorView />}
      {view === 'talleres' && <WorkshopsView />}
      {view === 'carga' && <BulkHelpView />}
      {view === 'auditoria' && <AuditoriaView />}
      {view === 'usuarios' && <UsuariosView />}
      {view === 'configuracion' && <ConfiguracionEmpresaView />}
      {view === 'envios-auto' && <AutomatizacionEnviosView />}
      {view === 'catalogos' && <CatalogosView />}
      {view === 'reclamos-config' && <ReclamosConfiguracionView />}
      {view === 'password' && <CambiarPasswordView />}
      {view === 'access-denied' && <AccessDeniedView />}
        </>
      )}
    </Layout>
  )
}

function viewFromPath(path: string): View {
  const normalized = path.toLowerCase().replace(/^\/+/, '')
  if (normalized === 'login') return 'dashboard'
  if (normalized === 'access-denied') return 'access-denied'
  if (normalized === 'usuarios') return 'usuarios'
  if (normalized === 'cambiar-password') return 'password'
  const match = navItems.find((item) => item.id === normalized)
  return match?.id ?? 'dashboard'
}

function pathFromView(view: View) {
  if (view === 'dashboard') return '/'
  if (view === 'password') return '/cambiar-password'
  return `/${view}`
}

export default App
