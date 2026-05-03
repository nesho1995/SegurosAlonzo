import { useEffect, useState } from 'react'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, Legend,
} from 'recharts'
import { getDashboardGraficos } from '../api/dashboardApi'
import type { DashboardGraficos } from '../types/dashboard'

const COLORS = ['#0f5f59', '#22a89a', '#f59e0b', '#ef4444', '#6366f1', '#14b8a6', '#f97316', '#8b5cf6']

const lempira = (v: number) =>
  `L ${new Intl.NumberFormat('es-HN', { minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(v)}`

const shortLempira = (v: number) => {
  if (v >= 1_000_000) return `L ${(v / 1_000_000).toFixed(1)}M`
  if (v >= 1_000)     return `L ${(v / 1_000).toFixed(0)}K`
  return `L ${v}`
}

function ChartPanel({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <article className="panel chart-panel">
      <div className="chart-panel-head">
        <strong>{title}</strong>
        {subtitle && <span>{subtitle}</span>}
      </div>
      {children}
    </article>
  )
}

export function DashboardCharts() {
  const [data, setData] = useState<DashboardGraficos | null>(null)

  useEffect(() => {
    getDashboardGraficos().then(setData).catch(() => null)
  }, [])

  if (!data) return null

  const { primaMensual, porAseguradora, porEstado, cuotasMensuales } = data

  return (
    <section className="charts-grid">

      {/* ── Prima por mes ────────────────────────────────────────── */}
      <ChartPanel title="Prima emitida por mes" subtitle="Últimos 12 meses">
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={primaMensual} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5eeec" />
            <XAxis dataKey="mesLabel" tick={{ fontSize: 11 }} tickFormatter={(v) => v.split(' ')[0]} />
            <YAxis tick={{ fontSize: 11 }} tickFormatter={shortLempira} width={56} />
            <Tooltip formatter={(v) => [lempira(Number(v ?? 0)), 'Prima']} labelFormatter={(l) => l} />
            <Bar dataKey="prima" fill="#0f5f59" radius={[3, 3, 0, 0]} maxBarSize={32} />
          </BarChart>
        </ResponsiveContainer>
      </ChartPanel>

      {/* ── Distribución por aseguradora ─────────────────────────── */}
      <ChartPanel title="Distribución por aseguradora" subtitle="Prima activa">
        <ResponsiveContainer width="100%" height={220}>
          <PieChart>
            <Pie
              data={porAseguradora}
              dataKey="prima"
              nameKey="aseguradora"
              cx="50%"
              cy="50%"
              innerRadius={50}
              outerRadius={85}
              paddingAngle={2}
            >
              {porAseguradora.map((_, i) => (
                <Cell key={i} fill={COLORS[i % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip formatter={(v) => lempira(Number(v ?? 0))} />
            <Legend
              iconType="circle"
              iconSize={8}
              wrapperStyle={{ fontSize: 11 }}
            />
          </PieChart>
        </ResponsiveContainer>
      </ChartPanel>

      {/* ── Estado de pólizas ────────────────────────────────────── */}
      <ChartPanel title="Estado de cartera" subtitle="Pólizas activas por situación">
        <ResponsiveContainer width="100%" height={220}>
          <PieChart>
            <Pie
              data={porEstado}
              dataKey="total"
              nameKey="estado"
              cx="50%"
              cy="50%"
              innerRadius={50}
              outerRadius={85}
              paddingAngle={2}
            >
              {porEstado.map((entry, i) => {
                const color =
                  entry.estado === 'Activa'       ? '#22c55e'
                  : entry.estado === 'Vence 7 días' ? '#ef4444'
                  : entry.estado === 'Vence 30 días'? '#f59e0b'
                  : entry.estado === 'En mora'      ? '#f97316'
                  :                                   '#6b7280'
                return <Cell key={i} fill={color} />
              })}
            </Pie>
            <Tooltip formatter={(v) => [`${v ?? 0} pólizas`, '']} />
            <Legend
              iconType="circle"
              iconSize={8}
              wrapperStyle={{ fontSize: 11 }}
            />
          </PieChart>
        </ResponsiveContainer>
      </ChartPanel>

      {/* ── Cuotas por mes ───────────────────────────────────────── */}
      <ChartPanel title="Cuotas por mes" subtitle="Pagadas / Pendientes / Vencidas">
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={cuotasMensuales} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5eeec" />
            <XAxis dataKey="mesLabel" tick={{ fontSize: 11 }} tickFormatter={(v) => v.split(' ')[0]} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} width={32} />
            <Tooltip />
            <Bar dataKey="pagadas"    name="Pagadas"    fill="#22c55e" radius={[3, 3, 0, 0]} maxBarSize={18} stackId="a" />
            <Bar dataKey="pendientes" name="Pendientes" fill="#f59e0b" radius={[0, 0, 0, 0]} maxBarSize={18} stackId="a" />
            <Bar dataKey="vencidas"   name="Vencidas"   fill="#ef4444" radius={[0, 0, 0, 0]} maxBarSize={18} stackId="a" />
            <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 11 }} />
          </BarChart>
        </ResponsiveContainer>
      </ChartPanel>

    </section>
  )
}
