export const money = new Intl.NumberFormat('es-HN', { style: 'currency', currency: 'HNL', maximumFractionDigits: 2 })
export const dateFmt = new Intl.DateTimeFormat('es-HN', { day: '2-digit', month: '2-digit', year: 'numeric' })
export function compactMeta(values: Array<string | undefined>) { const text = values.filter(Boolean).join(' / '); return text || 'Sin detalle' }
export function toDateInput(value?: string) { if (!value) return ''; return value.slice(0, 10) }
export function moneySafe(value?: number | null) {
  return typeof value === 'number' && Number.isFinite(value) ? money.format(value) : money.format(0)
}
