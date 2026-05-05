import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle2, CreditCard, FileText, ReceiptText, Scale, Send, ShieldCheck, Users, Wrench } from 'lucide-react'
import { getDashboard, getTareasHoy } from '../api/dashboardApi'
import type { TareasHoy } from '../api/dashboardApi'
import { DashboardCharts } from '../components/DashboardCharts'
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
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [tareas, setTareas] = useState<TareasHoy | null>(null)

  useEffect(() => {
    let alive = true
    setLoading(true)
    setError(null)
    getDashboard(filters)
      .then((json)  => { if (alive) { setData(json); setLastUpdated(new Date()) } })
      .catch((err)  => { if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.') })
      .finally(()   => { if (alive) setLoading(false) })
    return () => { alive = false }
  }, [filters])

  function load() {
    // Fuerza recarga tocando los filtros con el mismo valor → dispara el effect
    setFilters(f => ({ ...f }))
  }

  useEffect(() => {
    let alive = true
    getTareasHoy()
      .then((json) => { if (alive) setTareas(json) })
      .catch(() => {})
    return () => { alive = false }
  }, [])

  const criticalAlerts = useMemo(() => {
    if (!data) return 0
    return data.model.polizasVencidas + data.model.reclamosErrores + data.model.recordatoriosErrores + data.model.automatizacionesErrores + data.pagos.vencidas
  }, [data])

  if (loading) return <LoadingCard text="Cargando panel operativo..." />
  if (error || !data) return <ErrorCard text={error} />

  const { model, pagos, talleres } = data

  // Alertas urgentes: solo las que requieren acción hoy
  const urgencias = [
    model.polizasPorVencer7  > 0 && { tipo: 'red',   texto: `${model.polizasPorVencer7} póliza${model.polizasPorVencer7 > 1 ? 's' : ''} vence${model.polizasPorVencer7 > 1 ? 'n' : ''} en 7 días` },
    pagos.vencidas           > 0 && { tipo: 'red',   texto: `${pagos.vencidas} cuota${pagos.vencidas > 1 ? 's' : ''} vencida${pagos.vencidas > 1 ? 's' : ''}` },
    model.reclamosPendientes > 0 && { tipo: 'amber', texto: `${model.reclamosPendientes} reclamo${model.reclamosPendientes > 1 ? 's' : ''} pendiente${model.reclamosPendientes > 1 ? 's' : ''}` },
    model.datosPendientesRevision > 0 && { tipo: 'amber', texto: `${model.datosPendientesRevision} registro${model.datosPendientesRevision > 1 ? 's' : ''} con datos incompletos` },
    talleres.detectadosPendientes > 0 && { tipo: 'amber', texto: `${talleres.detectadosPendientes} taller${talleres.detectadosPendientes > 1 ? 'es' : ''} detectado${talleres.detectadosPendientes > 1 ? 's' : ''} sin revisar` },
  ].filter(Boolean) as { tipo: string; texto: string }[]

  return (
    <>
      <PageHeader
        eyebrow="Panel ejecutivo"
        title="Operacion de correduria"
        description={lastUpdated ? `Actualizado a las ${lastUpdated.toLocaleTimeString('es-HN', { hour: '2-digit', minute: '2-digit' })}` : 'Resumen ejecutivo para dar seguimiento a cartera, pagos, reclamos y tareas pendientes.'}
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

      {urgencias.length > 0 && (
        <div className="alert-banner">
          <strong className="alert-banner-title">⚡ Acciones requeridas</strong>
          <div className="alert-banner-items">
            {urgencias.map((u, i) => (
              <span key={i} className={`alert-chip alert-chip-${u.tipo}`}>{u.texto}</span>
            ))}
          </div>
        </div>
      )}

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

      <DashboardCharts />

      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Renovaciones proximas" subtitle={model.proximasRenovaciones.length >= 8 ? 'Mostrando las primeras 8 — puede haber mas. Usa los filtros para acotar.' : 'Polizas activas que vencen pronto y necesitan seguimiento.'} />
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
          <PanelTitle title="Tareas de hoy" subtitle="Vencimientos inminentes, cuotas vencidas y reclamos con documentos pendientes." />
          {!tareas ? (
            <div className="empty">Cargando tareas...</div>
          ) : (
            <div className="tareas-grid">
              {tareas.polizasVencen.length > 0 && (
                <div className="tarea-section">
                  <strong className="tarea-section-title">⏰ Pólizas que vencen hoy / mañana ({tareas.polizasVencen.length})</strong>
                  {tareas.polizasVencen.map((t) => (
                    <div className="tarea-row" key={t.id}>
                      <span>{t.cliente}</span>
                      <span className="tarea-meta">{t.numeroPoliza || '—'} · {t.aseguradora || '—'}</span>
                      <StatusPill text={t.diasRestantes === 0 ? 'Hoy' : `${t.diasRestantes}d`} tone={t.diasRestantes === 0 ? 'danger' : 'warning'} />
                    </div>
                  ))}
                </div>
              )}
              {tareas.cuotasVencidas.length > 0 && (
                <div className="tarea-section">
                  <strong className="tarea-section-title">💳 Cuotas vencidas sin pagar ({tareas.cuotasVencidas.length})</strong>
                  {tareas.cuotasVencidas.map((t) => (
                    <div className="tarea-row" key={t.id}>
                      <span>{t.cliente}</span>
                      <span className="tarea-meta">Cuota {t.numeroCuota} · {moneySafe(t.monto)}</span>
                      <StatusPill text={`${t.diasVencida}d`} tone="danger" />
                    </div>
                  ))}
                </div>
              )}
              {tareas.reclamosPendientes.length > 0 && (
                <div className="tarea-section">
                  <strong className="tarea-section-title">📋 Reclamos con docs pendientes ({tareas.reclamosPendientes.length})</strong>
                  {tareas.reclamosPendientes.map((t) => (
                    <div className="tarea-row" key={t.id}>
                      <span>{t.asegurado || t.cliente || `#${t.id}`}</span>
                      <span className="tarea-meta">{t.aseguradora || '—'}</span>
                      <StatusPill text="Docs. pendientes" tone="warning" />
                    </div>
                  ))}
                </div>
              )}
              {tareas.polizasVencen.length === 0 && tareas.cuotasVencidas.length === 0 && tareas.reclamosPendientes.length === 0 && (
                <div className="empty">✅ Sin tareas urgentes para hoy.</div>
              )}
            </div>
          )}
        </article>
      </section>
    </>
  )
}
