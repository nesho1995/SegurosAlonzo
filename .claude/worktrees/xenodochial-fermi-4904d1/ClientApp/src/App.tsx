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
import { ConfiguracionEmpresaView } from './views/ConfiguracionEmpresaView'
import { AutomatizacionEnviosView } from './views/AutomatizacionEnviosView'
import { CatalogosView } from './views/CatalogosView'
import { ReclamosConfiguracionView } from './views/ReclamosConfiguracionView'
import { GastosView } from './views/GastosView'
import { WhatsAppConfigView } from './views/WhatsAppConfigView'

const navItems = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, section: 'Operacion' },
  { id: 'clientes', label: 'Clientes', icon: Users, section: 'Operacion' },
  { id: 'reclamos', label: 'Reclamos', icon: ClipboardList, section: 'Operacion' },
  { id: 'pagos', label: 'Pagos', icon: CreditCard, section: 'Operacion' },
  { id: 'gastos', label: 'Gastos', icon: ReceiptText, section: 'Operacion' },
  { id: 'recordatorios', label: 'Recordatorios', icon: Send, section: 'Operacion' },
  { id: 'configuracion', label: 'Empresa', icon: Building2, section: 'Administracion' },
  { id: 'usuarios', label: 'Usuarios', icon: UserCog, section: 'Administracion' },
  { id: 'whatsapp-config', label: 'WhatsApp', icon: Send, section: 'Administracion' },
  { id: 'catalogos', label: 'Catalogos', icon: Settings, section: 'Administracion' },
  { id: 'carga', label: 'Carga masiva', icon: Upload, section: 'Administracion' },
  { id: 'talleres', label: 'Talleres', icon: Wrench, section: 'Administracion' },
  { id: 'reclamos-config', label: 'Reclamos config', icon: Settings, section: 'Administracion' },
  { id: 'envios-auto', label: 'Automatizacion envios', icon: Send, section: 'Administracion' },
  { id: 'extractor', label: 'Configuracion', icon: Settings, section: 'Administracion' },
  { id: 'automatizaciones', label: 'Automatizaciones', icon: Bot, section: 'Administracion' },
  { id: 'auditoria', label: 'Auditoria', icon: FileClock, section: 'Auditoria' },
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
    if (target === 'configuracion') return hasPermission('configuracion.administrar')
    if (target === 'envios-auto') return hasPermission('configuracion.administrar')
    if (target === 'whatsapp-config') return hasPermission('configuracion.administrar')
    if (target === 'catalogos') return hasPermission('configuracion.administrar')
    if (target === 'reclamos-config') return hasPermission('configuracion.administrar')
    if (target === 'gastos') return hasPermission('gastos.ver')
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
      {view === 'whatsapp-config' && <WhatsAppConfigView />}
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
