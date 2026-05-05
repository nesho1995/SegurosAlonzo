import { useEffect, useState } from 'react'
import { ErrorBoundary } from './components/ErrorBoundary'
import { Bot, Building2, ClipboardList, CreditCard, FileClock, LayoutDashboard, Mail, MessageSquare, ReceiptText, Send, Settings, Upload, UserCog, Users, Wrench } from 'lucide-react'
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
import { WhatsAppBandejaView } from './views/WhatsAppBandejaView'
import { CorreoConfigView } from './views/CorreoConfigView'
import { GlobalSearch } from './components/GlobalSearch'
import { ToastHost } from './components/ToastHost'

const navItems = [
  // Inicio
  { id: 'dashboard',       label: 'Inicio',            icon: LayoutDashboard, section: 'Inicio' },
  // Cartera
  { id: 'clientes',        label: 'Clientes y Cartera', icon: Users,           section: 'Cartera' },
  { id: 'pagos',           label: 'Cobros y Cuotas',   icon: CreditCard,      section: 'Cartera' },
  { id: 'gastos',          label: 'Gastos',             icon: ReceiptText,     section: 'Cartera' },
  { id: 'carga',           label: 'Carga masiva',       icon: Upload,          section: 'Cartera' },
  // Operaciones
  { id: 'reclamos',          label: 'Reclamos',           icon: ClipboardList,   section: 'Operaciones' },
  { id: 'whatsapp-bandeja',  label: 'Bandeja WhatsApp',   icon: MessageSquare,   section: 'Operaciones' },
  { id: 'recordatorios',     label: 'Recordatorios',      icon: Send,            section: 'Operaciones' },
  { id: 'talleres',          label: 'Talleres',           icon: Wrench,          section: 'Operaciones' },
  // Automatizacion
  { id: 'automatizaciones',label: 'Automatizaciones',   icon: Bot,             section: 'Automatizacion' },
  { id: 'envios-auto',     label: 'Envios automaticos', icon: Send,            section: 'Automatizacion' },
  // Configuracion
  { id: 'configuracion',   label: 'Empresa',            icon: Building2,       section: 'Configuracion' },
  { id: 'correo-config',   label: 'Correo',             icon: Mail,            section: 'Configuracion' },
  { id: 'whatsapp-config', label: 'WhatsApp',           icon: Send,            section: 'Configuracion' },
  { id: 'usuarios',        label: 'Usuarios',           icon: UserCog,         section: 'Configuracion' },
  { id: 'catalogos',       label: 'Catalogos',          icon: Settings,        section: 'Configuracion' },
  { id: 'reclamos-config', label: 'Reglas de reclamos',  icon: Settings,        section: 'Configuracion' },
  { id: 'extractor',       label: 'Extractor de correo', icon: Settings,        section: 'Configuracion' },
  // Auditoria
  { id: 'auditoria',       label: 'Auditoria',          icon: FileClock,       section: 'Auditoria' },
] satisfies NavItem[]

function App() {
  return (
    <AuthProvider>
      <AppRouter />
      <ToastHost />
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
    const onForbidden    = () => setView('access-denied')
    const onUnauthorized = () => setView('dashboard')
    const onPopState     = () => setView(viewFromPath(window.location.pathname))
    window.addEventListener('app:forbidden', onForbidden)
    window.addEventListener('app:unauthorized', onUnauthorized)
    window.addEventListener('popstate', onPopState)
    return () => {
      window.removeEventListener('app:forbidden', onForbidden)
      window.removeEventListener('app:unauthorized', onUnauthorized)
      window.removeEventListener('popstate', onPopState)
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
    if (target === 'correo-config') return hasPermission('configuracion.administrar')
    if (target === 'whatsapp-config') return hasPermission('configuracion.administrar')
    if (target === 'catalogos') return hasPermission('configuracion.administrar')
    if (target === 'reclamos-config') return hasPermission('configuracion.administrar')
    if (target === 'gastos') return hasPermission('gastos.ver')
    if (target === 'whatsapp-bandeja') return hasPermission('whatsapp.bandeja')
    return true
  }

  const allowedNavItems = navItems.filter((item) => {
    return canAccessView(item.id)
  })

  return (
    <Layout navItems={allowedNavItems} view={view} onViewChange={navigate}>
      <GlobalSearch />
      {!canAccessView(view) && <AccessDeniedView />}
      {canAccessView(view) && (
        <ErrorBoundary key={view}>
          {view === 'dashboard'      && <DashboardView />}
          {view === 'clientes'       && <ClientsView />}
          {view === 'reclamos'       && <ReclamosView />}
          {view === 'recordatorios'  && <RemindersView />}
          {view === 'pagos'          && <PaymentsView />}
          {view === 'gastos'         && <GastosView />}
          {view === 'automatizaciones' && <AutomationView />}
          {view === 'extractor'      && <ExtractorView />}
          {view === 'talleres'       && <WorkshopsView />}
          {view === 'carga'          && <BulkHelpView />}
          {view === 'auditoria'      && <AuditoriaView />}
          {view === 'usuarios'       && <UsuariosView />}
          {view === 'configuracion'  && <ConfiguracionEmpresaView />}
          {view === 'envios-auto'    && <AutomatizacionEnviosView />}
          {view === 'correo-config'  && <CorreoConfigView />}
          {view === 'whatsapp-config'   && <WhatsAppConfigView />}
          {view === 'whatsapp-bandeja'  && <WhatsAppBandejaView />}
          {view === 'catalogos'      && <CatalogosView />}
          {view === 'reclamos-config' && <ReclamosConfiguracionView />}
          {view === 'password'       && <CambiarPasswordView />}
          {view === 'access-denied'  && <AccessDeniedView />}
        </ErrorBoundary>
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
  if (normalized === 'whatsapp-bandeja') return 'whatsapp-bandeja'
  const match = navItems.find((item) => item.id === normalized)
  return match?.id ?? 'dashboard'
}

function pathFromView(view: View) {
  if (view === 'dashboard') return '/'
  if (view === 'password') return '/cambiar-password'
  return `/${view}`
}

export default App
