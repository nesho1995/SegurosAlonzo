import type { PaymentStats } from './pagos'
export type Renewal = { id: number; cliente: string; aseguradora?: string; numeroPoliza?: string; ramo?: string; hasta?: string; primaTotal?: number; diasRestantes: number; estadoPago: string }
export type DashboardModel = { totalClientes: number; clientesActivos: number; totalPolizas: number; polizasActivas: number; polizasPorVencer30: number; polizasPorVencer15: number; polizasPorVencer7: number; polizasVencidas: number; primaTotalActiva: number; reclamosTotal: number; reclamosPendientes: number; reclamosCompletos: number; reclamosErrores: number; reclamosConDocumentosPendientes?: number; reclamosCerradosMes?: number; montoEstimadoReclamos?: number; montoAprobadoReclamos?: number; montoPagadoReclamos?: number; recordatoriosPendientes: number; recordatoriosErrores: number; automatizacionesErrores: number; gastosMes: number; datosPendientesRevision: number; proximasRenovaciones: Renewal[] }
export type WorkshopStats = { detectadosPendientes: number }
export type DashboardFilters = { aseguradoras: string[]; ciudades: string[] }
export type DashboardResponse = { model: DashboardModel; pagos: PaymentStats & { montoVencido: number }; talleres: WorkshopStats; filtros: DashboardFilters }

// Gráficos
export type PrimaMensual    = { mes: string; mesLabel: string; prima: number; polizas: number }
export type DistAseguradora = { aseguradora: string; polizas: number; prima: number }
export type DistEstado      = { estado: string; total: number }
export type CuotaMensual    = { mes: string; mesLabel: string; pagadas: number; pendientes: number; vencidas: number; montoPagado: number; montoPendiente: number }
export type DashboardGraficos = {
  primaMensual:    PrimaMensual[]
  porAseguradora:  DistAseguradora[]
  porEstado:       DistEstado[]
  cuotasMensuales: CuotaMensual[]
}
