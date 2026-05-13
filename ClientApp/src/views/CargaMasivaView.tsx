import { useState } from 'react'
import { ChevronDown, Download, FileSpreadsheet, Maximize2, Search, Upload, X } from 'lucide-react'
import { descargarExcelLimpioCartera, descargarReporteCartera, importarCartera, importarReclamos, importarTalleres, previewCartera, previewReclamos, previewTalleres } from '../api/cargaMasivaApi'
import { StatusPill } from '../components/Badge'
import { DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Info, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import type { WorkshopImportRow } from '../types/talleres'
import type { CarteraImportPreview, ReclamoHistoricoImportPreview } from '../types/cargaMasiva'
import { validateExcelFile } from '../utils/validators'

export function BulkHelpView() {
  const [carteraFile, setCarteraFile] = useState<File | null>(null)
  const [carteraPreview, setCarteraPreview] = useState<CarteraImportPreview | null>(null)
  const [expandedPreview, setExpandedPreview] = useState(false)
  const [talleresFile, setTalleresFile] = useState<File | null>(null)
  const [talleresPreview, setTalleresPreview] = useState<WorkshopImportRow[]>([])
  const [reclamosFile, setReclamosFile] = useState<File | null>(null)
  const [reclamosPreview, setReclamosPreview] = useState<ReclamoHistoricoImportPreview | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const hasPreviewErrors = talleresPreview.some((row) => row.errores.length > 0)

  const importableRows = carteraPreview?.rows.filter((r) => r.errors.length === 0).length ?? 0
  const rejectedRows   = carteraPreview?.rows.filter((r) => r.errors.length > 0).length ?? 0
  const carteraRows    = carteraPreview?.rows.slice(0, 60) ?? []
  const reclamosRows   = reclamosPreview?.rows.slice(0, 80) ?? []

  async function previewCarteraFile() {
    const v = validateExcelFile(carteraFile); if (v) { setError(v); return }
    if (!carteraFile) return
    try { setCarteraPreview(await previewCartera(carteraFile)); setError(null) }
    catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function importCarteraFile() {
    const v = validateExcelFile(carteraFile); if (v) { setError(v); return }
    if (!carteraFile) return
    if (!carteraPreview) { setError('Primero genera el preview.'); return }
    try {
      const d = await importarCartera(carteraFile)
      setMessage(`Importadas ${d.filasImportadas} filas. ${d.filasRechazadas} requieren corrección. Clientes: ${d.clientes}. Pólizas nuevas: ${d.polizas}. Duplicadas: ${d.polizasDuplicadas}.`)
      setError(null)
    } catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function previewTalleresFile() {
    const v = validateExcelFile(talleresFile); if (v) { setError(v); return }
    if (!talleresFile) return
    try { const d = await previewTalleres(talleresFile); setTalleresPreview(d.items); setError(null) }
    catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function importTalleresFile() {
    const v = validateExcelFile(talleresFile); if (v) { setError(v); return }
    if (!talleresFile) return
    if (talleresPreview.length === 0) { setError('Primero genera el preview.'); return }
    if (hasPreviewErrors) { setError('Hay filas con errores. Corrígelas antes de importar.'); return }
    try { const d = await importarTalleres(talleresFile); setMessage(`Importados: ${d.importados}. Rechazados: ${d.rechazados}.`); setError(null) }
    catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function previewReclamosFile() {
    const v = validateExcelFile(reclamosFile); if (v) { setError(v); return }
    if (!reclamosFile) return
    try { setReclamosPreview(await previewReclamos(reclamosFile)); setError(null) }
    catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function importReclamosFile() {
    const v = validateExcelFile(reclamosFile); if (v) { setError(v); return }
    if (!reclamosFile) return
    if (!reclamosPreview) { setError('Primero genera el preview.'); return }
    if (reclamosPreview.importableCount === 0) { setError('No hay reclamos importables en el archivo.'); return }
    try {
      const d = await importarReclamos(reclamosFile)
      setMessage(`Reclamos importados: ${d.importados}. Duplicados: ${d.duplicados}. Rechazados: ${d.rechazados}.`)
      setError(null)
      setReclamosPreview(null)
    } catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  async function downloadCarteraBlob(fileName: string, getter: (f: File) => Promise<Blob>) {
    if (!carteraFile) return
    try {
      const blob = await getter(carteraFile)
      const url = URL.createObjectURL(blob)
      Object.assign(document.createElement('a'), { href: url, download: fileName }).click()
      URL.revokeObjectURL(url)
    } catch (err) { setError(err instanceof Error ? err.message : 'Error inesperado.') }
  }

  return (
    <>
      <PageHeader eyebrow="Ayuda" title="Carga masiva" description="Importa cartera y talleres desde Excel. Revisa el preview antes de confirmar." onRefresh={() => undefined} />

      {error   && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      {/* ── Plantillas ──────────────────────────────────────────── */}
      <details className="accordion-section">
        <summary>
          <span><strong>Plantillas Excel</strong><small>Descarga el formato correcto antes de preparar el archivo</small></span>
          <span className="accordion-actions"><ChevronDown size={18} /></span>
        </summary>
        <div className="accordion-body">
          <div className="download-grid">
            <a className="primary-button" href="/api/carga-masiva/plantilla-cartera"><Download size={16} />Cartera estándar</a>
            <a className="primary-button" href="/api/carga-masiva/plantilla-cartera-financiera"><Download size={16} />Cartera financiera</a>
            <a className="primary-button" href="/api/talleres/plantilla"><Download size={16} />Talleres</a>
          </div>
          <div className="info-grid bulk-info-grid" style={{ marginTop: 12 }}>
            <Info label="Cuotas" value="CUOTA 1 … CUOTA N lleva el monto de cada cuota. Las fechas parten de VIGENCIA + 1 mes por cuota." />
            <Info label="Financiera" value="CLIENTE es el asegurado; FINANCIERA/CLIENTE FINANCIERA es el banco o financiera contratante." />
            <Info label="Fechas" value="dd/mm/aaaa o yyyy-mm-dd." />
          </div>
        </div>
      </details>

      {/* ── Cartera ─────────────────────────────────────────────── */}
      <details className="accordion-section" open>
        <summary>
          <span>
            <strong>Cargar cartera</strong>
            <small>
              {carteraPreview
                ? <>{importableRows} importables · {rejectedRows} con error · {carteraPreview.warningCount} advertencias</>
                : carteraFile ? carteraFile.name : 'Selecciona un archivo .xlsx'}
            </small>
          </span>
          <span className="accordion-actions"><ChevronDown size={18} /></span>
        </summary>
        <div className="accordion-body">
          <div className="bulk-file-row" style={{ padding: '8px 0 12px' }}>
            <label className="bulk-file-btn">
              <FileSpreadsheet size={16} />
              <span>Seleccionar archivo</span>
              <input type="file" accept=".xlsx" onChange={(e) => { setCarteraFile(e.target.files?.[0] || null); setCarteraPreview(null); setMessage(null); setError(null) }} />
            </label>
            {carteraFile && <span className="bulk-file-name">{carteraFile.name}</span>}
            <button className="icon-button secondary" onClick={previewCarteraFile} disabled={!carteraFile}><Search size={16} />Preview</button>
            <button className="icon-button secondary" onClick={() => setExpandedPreview(true)} disabled={!carteraPreview}><Maximize2 size={16} />Ampliar</button>
            <button className="icon-button secondary" onClick={() => downloadCarteraBlob('reporte_carga.xlsx', descargarReporteCartera)} disabled={!carteraFile}><Download size={16} />Reporte</button>
            <button className="icon-button secondary" onClick={() => downloadCarteraBlob('cartera_limpia.xlsx', descargarExcelLimpioCartera)} disabled={!carteraFile}><Download size={16} />Excel limpio</button>
            <button className="primary-button" onClick={importCarteraFile} disabled={!carteraPreview || importableRows === 0}><Upload size={16} />Importar válidas ({importableRows})</button>
          </div>
          {carteraPreview && (
            <div className={rejectedRows > 0 ? 'inline-alert warning' : 'inline-alert success'} style={{ marginBottom: 8 }}>
              {importableRows} importables · {rejectedRows} rechazadas · {carteraPreview.errorCount} errores · {carteraPreview.warningCount} advertencias
            </div>
          )}
          <CarteraPreviewTable rows={carteraRows} />
        </div>
      </details>

      <details className="accordion-section">
        <summary>
          <span>
            <strong>Cargar reclamos activos</strong>
            <small>{reclamosPreview ? `${reclamosPreview.importableCount} importables · ${reclamosPreview.duplicateCount} duplicados · ${reclamosPreview.errorCount} errores` : reclamosFile ? reclamosFile.name : 'Formato CREFISA / Prestadito de reclamos historicos'}</small>
          </span>
          <span className="accordion-actions"><ChevronDown size={18} /></span>
        </summary>
        <div className="accordion-body">
          <div className="bulk-file-row" style={{ padding: '8px 0 12px' }}>
            <label className="bulk-file-btn">
              <FileSpreadsheet size={16} />
              <span>Seleccionar archivo</span>
              <input type="file" accept=".xlsx" onChange={(e) => { setReclamosFile(e.target.files?.[0] || null); setReclamosPreview(null); setMessage(null); setError(null) }} />
            </label>
            {reclamosFile && <span className="bulk-file-name">{reclamosFile.name}</span>}
            <button className="icon-button secondary" onClick={previewReclamosFile} disabled={!reclamosFile}><Search size={16} />Preview</button>
            <button className="primary-button" onClick={importReclamosFile} disabled={!reclamosPreview || reclamosPreview.importableCount === 0}><Upload size={16} />Importar ({reclamosPreview?.importableCount ?? 0})</button>
          </div>
          {reclamosPreview && <div className={reclamosPreview.errorCount > 0 ? 'inline-alert warning' : 'inline-alert success'} style={{ marginBottom: 8 }}>{reclamosPreview.totalRows} filas · {reclamosPreview.importableCount} importables · {reclamosPreview.duplicateCount} duplicados · {reclamosPreview.errorCount} errores</div>}
          <ReclamosPreviewTable rows={reclamosRows} />
        </div>
      </details>

      {/* ── Talleres ─────────────────────────────────────────────── */}
      <details className="accordion-section">
        <summary>
          <span>
            <strong>Cargar talleres</strong>
            <small>{talleresFile ? `${talleresFile.name}${talleresPreview.length ? ` · ${talleresPreview.length} filas` : ''}` : 'Selecciona un archivo .xlsx'}</small>
          </span>
          <span className="accordion-actions"><ChevronDown size={18} /></span>
        </summary>
        <div className="accordion-body">
          <div className="bulk-file-row" style={{ padding: '8px 0 12px' }}>
            <label className="bulk-file-btn">
              <FileSpreadsheet size={16} />
              <span>Seleccionar archivo</span>
              <input type="file" accept=".xlsx" onChange={(e) => { setTalleresFile(e.target.files?.[0] || null); setTalleresPreview([]); setMessage(null); setError(null) }} />
            </label>
            {talleresFile && <span className="bulk-file-name">{talleresFile.name}</span>}
            <button className="icon-button secondary" onClick={previewTalleresFile} disabled={!talleresFile}><Search size={16} />Preview</button>
            <button className="primary-button" onClick={importTalleresFile} disabled={talleresPreview.length === 0 || hasPreviewErrors}><Upload size={16} />Importar</button>
          </div>
          {hasPreviewErrors && <div className="inline-alert danger" style={{ marginBottom: 8 }}>Corrige las filas marcadas antes de importar.</div>}
          {talleresPreview.length === 0
            ? <div className="empty-state">Sube un archivo y usa Preview para revisar las filas antes de importar.</div>
            : <DataTable headers={['Fila', 'Taller', 'Ciudad', 'Resultado']} rows={talleresPreview.map((row) => [row.fila, row.taller.nombre, row.taller.ciudad, row.errores.length ? <StatusPill text={row.errores.join(', ')} tone="danger" /> : <StatusPill text="Lista" tone="success" />])} />}
        </div>
      </details>

      {/* ── Modal vista ampliada ─────────────────────────────────── */}
      {expandedPreview && carteraPreview && (
        <div className="preview-modal" role="dialog" aria-modal="true" onClick={(e) => e.target === e.currentTarget && setExpandedPreview(false)}>
          {/* Botón cerrar flotante — siempre visible */}
          <button className="preview-modal-close" onClick={() => setExpandedPreview(false)} aria-label="Cerrar">
            <X size={16} />Cerrar
          </button>
          <div className="preview-modal-content">
            <div className="preview-modal-head">
              <PanelTitle
                title="Vista ampliada — cartera"
                subtitle={`${importableRows} importables · ${rejectedRows} rechazadas · ${carteraPreview.errorCount} errores · ${carteraPreview.warningCount} advertencias`}
              />
            </div>
            <CarteraPreviewTable rows={carteraPreview.rows} expanded />
          </div>
        </div>
      )}
    </>
  )
}

const nf = (v: string) => v.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^A-Z0-9]/gi, '').toUpperCase()

function CarteraPreviewTable({ rows, expanded = false }: { rows: CarteraImportPreview['rows']; expanded?: boolean }) {
  if (rows.length === 0)
    return <div className={expanded ? 'large-preview-table' : 'preview-table'}><div className="empty-state">Sin filas. Selecciona un archivo y genera el preview.</div></div>

  return (
    <div className={expanded ? 'large-preview-table' : 'preview-table'}>
      <DataTable
        headers={['#', 'Estado', 'Cliente', 'WhatsApp', '✓', 'Correos', 'Póliza', 'Aseg.', 'Vigencia→Hasta', 'Cuotas', 'Errores', 'Advertencias']}
        rows={rows.map((row) => {
          const get = (f: string) => row.values.find((v) => nf(v.field) === nf(f))
          const nc = parseInt(get('CUOTAS')?.clean ?? '0', 10) || 0
          const montos = nc > 0
            ? Array.from({ length: nc }, (_, i) => {
                const val = get(`CUOTA ${i + 1}`)?.clean
                return val ? `C${i + 1}:${val}` : null
              }).filter(Boolean).join(' · ')
            : null

          return [
            row.rowNumber,
            row.estado === 'ERROR'              ? <StatusPill text="Error"       tone="danger"  />
            : row.estado === 'CON_ADVERTENCIAS' ? <StatusPill text="Advertencia" tone="warning" />
            :                                     <StatusPill text="Válida"      tone="success" />,
            <Cell top={get('NOMBRE')?.original} bot={get('NOMBRE')?.clean} />,
            row.phoneNormalization.principal || <span className="muted-text">—</span>,
            row.phoneNormalization.whatsappReady ? <StatusPill text="✓" tone="success" /> : <StatusPill text="✗" tone="warning" />,
            <span className={row.emailNormalization.valid.length ? '' : 'muted-text'}>
              {row.emailNormalization.valid.join(', ') || '—'}
            </span>,
            <Cell top={get('POLIZA')?.original} bot={get('POLIZA')?.clean} />,
            get('COMPANIA SEGUROS')?.clean || <span className="muted-text">—</span>,
            <Cell top={get('VIGENCIA')?.clean || '—'} bot={get('HASTA')?.clean || '—'} />,
            montos
              ? <div className="clean-cell"><span>{nc} cuotas</span><strong>{montos}</strong></div>
              : <span className="muted-text">—</span>,
            <IssueList issues={row.errors} empty="—" />,
            <IssueList issues={row.warnings} empty="—" />,
          ]
        })}
      />
    </div>
  )
}

function ReclamosPreviewTable({ rows }: { rows: ReclamoHistoricoImportPreview['rows'] }) {
  if (rows.length === 0)
    return <div className="preview-table"><div className="empty-state">Sin filas. Selecciona un archivo y genera el preview.</div></div>

  return (
    <div className="preview-table">
      <DataTable
        headers={['Fila', 'Estado', 'Conductor', 'Poliza', 'Reclamo', 'Vehiculo', 'Celular', 'Documentos', 'Alertas']}
        rows={rows.map((row) => {
          const docs = Object.entries(row.documentosRecibidos).filter(([, ok]) => ok).map(([name]) => name)
          return [
            row.rowNumber,
            row.errors.length > 0 ? <StatusPill text="Error" tone="danger" /> : row.duplicado ? <StatusPill text="Duplicado" tone="warning" /> : <StatusPill text="Importable" tone="success" />,
            <Cell top={row.conductor || 'Sin conductor'} bot={row.cliente} />,
            row.poliza || <span className="muted-text">—</span>,
            row.reclamo || <span className="muted-text">Sin reclamo</span>,
            <Cell top={row.vehiculo || '—'} bot={row.placa || row.observaciones} />,
            row.celular || <span className="muted-text">—</span>,
            docs.length ? docs.join(', ') : <span className="muted-text">Pendientes</span>,
            <>
              <IssueList issues={row.errors} empty="" />
              <IssueList issues={row.warnings} empty="" />
            </>,
          ]
        })}
      />
    </div>
  )
}

function Cell({ top, bot }: { top?: string; bot?: string }) {
  return (
    <div className="clean-cell">
      <span>{top || '—'}</span>
      {bot && bot !== top && <strong>{bot}</strong>}
    </div>
  )
}

function IssueList({ issues, empty }: { issues: Array<{field:string;message:string}>; empty: string }) {
  if (issues.length === 0) return <span className="muted-text">{empty}</span>
  return (
    <div className="issue-list">
      {issues.map((e, i) => <span key={`${e.field}-${i}`}><strong>{e.field}:</strong> {e.message}</span>)}
    </div>
  )
}
