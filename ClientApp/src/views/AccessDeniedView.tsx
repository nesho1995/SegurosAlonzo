import { LockKeyhole } from 'lucide-react'
import { PageHeader } from '../components/Topbar'

export function AccessDeniedView() {
  return (
    <>
      <PageHeader eyebrow="Permisos" title="Acceso denegado" description="Tu usuario no tiene permiso para realizar esta accion." onRefresh={() => {}} />
      <section className="state-card access-card">
        <LockKeyhole size={44} />
        <strong>No tienes acceso a esta seccion</strong>
        <p>Si necesitas trabajar aqui, solicita a un administrador que revise tu rol y permisos.</p>
      </section>
    </>
  )
}
