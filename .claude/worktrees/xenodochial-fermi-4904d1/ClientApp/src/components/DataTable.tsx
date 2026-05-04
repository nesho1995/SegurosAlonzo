import type { ReactNode } from 'react'

function columnClass(header: string) {
  const text = header.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
  if (text.includes('accion')) return 'col-acciones'
  if (text.includes('nombre') || text.includes('cliente') || text.includes('taller') || text.includes('reclamo')) return 'col-nombre'
  if (text.includes('direccion') || text.includes('observacion') || text.includes('descripcion') || text.includes('detalle')) return 'col-direccion'
  if (text.includes('email') || text.includes('correo') || text.includes('ruta')) return 'col-largo'
  if (text.includes('ciudad')) return 'col-ciudad'
  return ''
}

export function DataTable({ headers, rows }: { headers: string[]; rows: Array<Array<ReactNode>> }) {
  if (rows.length === 0) return <div className="empty">No hay datos para mostrar.</div>
  return (<div className="table-responsive table-wrap"><table className="data-table"><thead><tr>{headers.map((header) => <th className={columnClass(header)} key={header}>{header}</th>)}</tr></thead><tbody>{rows.map((row, rowIndex) => (<tr key={rowIndex}>{row.map((cell, cellIndex) => <td className={columnClass(headers[cellIndex])} data-label={headers[cellIndex]} key={cellIndex}>{cell}</td>)}</tr>))}</tbody></table></div>)
}
export function CellTitle({ title, subtitle }: { title: string; subtitle?: string }) { return (<div className="cell-title"><strong>{title}</strong>{subtitle && <span>{subtitle}</span>}</div>) }
