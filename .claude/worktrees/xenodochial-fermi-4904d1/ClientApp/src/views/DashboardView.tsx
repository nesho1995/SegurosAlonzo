import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle2, CreditCard, FileText, ReceiptText, Scale, Send, ShieldCheck, Users, Wrench } from 'lucide-react'
import { getDashboard } from '../api/dashboardApi'
import { StatusPill } from '../components/Badge'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import { Metric } from '../components/StatCard'
import type { DashboardResponse } from '../types/dashboard'
import { compactMeta, dateFmt, moneySafe } from '../utils/formatters'

export function DashboardView() {
  const [data, setData] = useState<DashboardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [filters, setFilters] = useState({ desde: '', hasta: '', aseguradora: '', ciudad: '' })

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await getDashboard(filters))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    getDashboard(filters)
      .then((json) => {
        if (alive) setData(json)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [filters])

  const criticalAlerts = useMemo(() => {
    if (!data) return 0
    return data.model.polizasVencidas + data.model.reclamosErrores + data.model.recordatoriosErrores + data.model.automatizacionesErrores + data.pagos.vencidas
  }, [data])

  if (loading) return <LoadingCard text="Cargando panel operativo..." />
  if (error || !data) return <ErrorCard text={error} />

  const { model, pagos, talleres } = data

  return (
    <>
      <PageHeader
        eyebrow="Panel ejecutivo"
        title="Operacion de correduria"
        description="Resumen ejecutivo para dar seguimiento a cartera, pagos, reclamos y tareas pendientes."
        onRefresh={load}
        action={(
          <div className="dashboard-filters">
            <input type="date" value={filters.desde} onChange={(event) => setFilters({ ...filters, desde: event.target.value })} />
            <input type="date" value={filters.hasta} onChange={(event) => setFilters({ ...filters, hasta: event.target.value })} />
            <select value={filters.aseguradora} onChange={(event) => setFilters({ ...filters, aseguradora: event.target.value })}>
              <option value="">Aseguradora</option>
              {data.filtros.aseguradoras.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
            <select value={filters.ciudad} onChange={(event) => setFilters({ ...filters, ciudad: event.target.value })}>
              <option value="">Ciudad</option>
              {data.filtros.ciudades.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </div>
        )}
      />

      <section className="metric-grid">
        <Metric title="Clientes activos" value={model.clientesActivos} hint={`${model.totalClientes} registrados`} tone="blue" icon={Users} />
        <Metric title="Polizas activas" value={model.polizasActivas} hint={`${model.totalPolizas} en cartera`} tone="green" icon={ShieldCheck} />
        <Metric title="Pagos vencidos" value={pagos.vencidas} hint={moneySafe(pagos.montoVencido)} tone="red" icon={CreditCard} />
        <Metric title="Vencen 30 dias" value={model.polizasPorVencer30} hint={`${model.polizasPorVencer15} en 15 dias`} tone="amber" icon={CalendarClock} />
        <Metric title="Vencen 7 dias" value={model.polizasPorVencer7} hint="Atencion inmediata" tone="red" icon={CalendarClock} />
        <Metric title="Recordatorios" value={model.recordatoriosPendientes} hint={`${model.recordatoriosErrores} con error`} tone="amber" icon={Send} />
        <Metric title="Reclamos pendientes" value={model.reclamosPendientes} hint={`${model.reclamosTotal} totales`} tone="blue" icon={AlertTriangle} />
        <Metric title="Docs pendientes reclamo" value={model.reclamosConDocumentosPendientes || 0} hint={`Cerrados mes: ${model.reclamosCerradosMes || 0}`} tone="amber" icon={FileText} />
        <Metric title="Monto estimado reclamos" value={moneySafe(model.montoEstimadoReclamos || 0)} hint={`Aprobado: ${moneySafe(model.montoAprobadoReclamos || 0)}`} tone="slate" icon={Scale} />
        <Metric title="Monto pagado reclamos" value={moneySafe(model.montoPagadoReclamos || 0)} hint="Historial de pagos de reclamo" tone="green" icon={CheckCircle2} />
        <Metric title="Talleres detectados" value={talleres.detectadosPendientes} hint="Pendientes de revisar" tone={talleres.detectadosPendientes ? 'amber' : 'green'} icon={Wrench} />
        <Metric title="Reglas con errores" value={model.automatizacionesErrores} hint="Ejecuciones con error" tone={model.automatizacionesErrores ? 'red' : 'green'} icon={AlertTriangle} />
        <Metric title="Prima activa" value={moneySafe(model.primaTotalActiva)} hint="Cartera vigente" tone="slate" icon={CheckCircle2} />
        <Metric title="Gastos del mes" value={moneySafe(model.gastosMes)} hint="Operacion actual" tone="slate" icon={ReceiptText} />
        <Metric title="Datos por revisar" value={model.datosPendientesRevision} hint="Calidad de cartera" tone={model.datosPendientesRevision ? 'amber' : 'green'} icon={ShieldCheck} />
        <Metric title="Alertas criticas" value={criticalAlerts} hint="Vencidas, errores y pagos" tone="red" icon={AlertTriangle} />
      </section>

      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Renovaciones proximas" subtitle="Polizas activas que vencen pronto y necesitan seguimiento." />
          <div className="renewal-list">
            {model.proximasRenovaciones.length === 0 ? (
              <div className="empty">No hay renovaciones proximas.</div>
            ) : (
              model.proximasRenovaciones.map((item) => (
                <div className="renewal-row" key={item.id}>
                  <div>
                    <strong>{item.cliente}</strong>
                    <span>{compactMeta([item.aseguradora, item.ramo])}</span>
                  </div>
                  <div>
                    <strong>{item.numeroPoliza || 'Sin poliza'}</strong>
                    <span>{item.hasta ? dateFmt.format(new Date(item.hasta)) : 'Sin fecha'}</span>
                  </div>
                  <StatusPill text={`${item.diasRestantes} dias`} tone={item.diasRestantes <= 7 ? 'danger' : item.diasRestantes <= 15 ? 'warning' : 'info'} />
                </div>
              ))
            )}
          </div>
        </article>

        <article className="panel">
          <PanelTitle title="Siguientes prioridades" subtitle="Acciones recomendadas para mantener la correduria al dia." />
          <ol className="roadmap">
            <li><strong>Renovaciones</strong><span>Contactar clientes con vencimientos cercanos</span></li>
            <li><strong>Pagos</strong><span>Revisar cuotas vencidas y comprobantes</span></li>
            <li><strong>Calidad de datos</strong><span>Completar telefonos, correos y notas pendientes</span></li>
          </ol>
        </article>
      </section>
    </>
  )
}
