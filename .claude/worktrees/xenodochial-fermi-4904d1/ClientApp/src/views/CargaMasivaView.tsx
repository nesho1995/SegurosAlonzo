import { useState } from 'react'
import { Download, Maximize2, Search, Upload, X } from 'lucide-react'
import { descargarExcelLimpioCartera, descargarReporteCartera, importarCartera, importarTalleres, previewCartera, previewTalleres } from '../api/cargaMasivaApi'
import { StatusPill } from '../components/Badge'
import { DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Info, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import type { WorkshopImportRow } from '../types/talleres'
import type { CarteraImportPreview } from '../types/cargaMasiva'
import { validateExcelFile } from '../utils/validators'

export function BulkHelpView() {
  const [file, setFile] = useState<File | null>(null)
  const [carteraFile, setCarteraFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<WorkshopImportRow[]>([])
  const [carteraPreview, setCarteraPreview] = useState<CarteraImportPreview | null>(null)
  const [expandedPreview, setExpandedPreview] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const hasPreviewErrors = preview.some((row) => row.errores.length > 0)

  async function previewFile() {
    const selected = file
    const validation = validateExcelFile(selected)
    if (validation) {
      setError(validation)
      return
    }
    if (!selected) return
    try {
      const data = await previewTalleres(selected)
      setPreview(data.items)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function importFile() {
    const selected = file
    const validation = validateExcelFile(selected)
    if (validation) {
      setError(validation)
      return
    }
    if (!selected) return
    if (preview.length === 0) {
      setError('Primero genera el preview para revisar las filas.')
      return
    }
    if (hasPreviewErrors) {
      setError('Hay filas con errores. Corrigelas antes de importar.')
      return
    }
    try {
      const data = await importarTalleres(selected)
      setMessage(`Importados: ${data.importados}. Rechazados: ${data.rechazados}.`)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function previewCarteraFile() {
    const selected = carteraFile
    const validation = validateExcelFile(selected)
    if (validation) {
      setError(validation)
      return
    }
    if (!selected) return
    try {
      setCarteraPreview(await previewCartera(selected))
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function importCarteraFile() {
    const selected = carteraFile
    const validation = validateExcelFile(selected)
    if (validation) {
      setError(validation)
      return
    }
    if (!selected) return
    if (!carteraPreview) {
      setError('Primero genera el preview de cartera.')
      return
    }
    try {
      const data = await importarCartera(selected)
      setMessage(`Se importaron ${data.filasImportadas} filas. ${data.filasRechazadas} filas requieren correccion. Clientes: ${data.clientes}. Polizas nuevas: ${data.polizas}. Duplicadas omitidas: ${data.polizasDuplicadas}.`)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function downloadCarteraReport() {
    await downloadCarteraBlob('reporte_carga_cartera.xlsx', descargarReporteCartera)
  }

  async function downloadCleanCartera() {
    await downloadCarteraBlob('cartera_limpia.xlsx', descargarExcelLimpioCartera)
  }

  async function downloadCarteraBlob(fileName: string, getter: (file: File) => Promise<Blob>) {
    if (!carteraFile) return
    try {
      const blob = await getter(carteraFile)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      link.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  const carteraRows = carteraPreview?.rows.slice(0, 80) ?? []
  const importableRows = carteraPreview?.rows.filter((row) => row.errors.length === 0).length ?? 0
  const rejectedRows = carteraPreview?.rows.filter((row) => row.errors.length > 0).length ?? 0

  return (
    <>
      <PageHeader eyebrow="Ayuda" title="Carga masiva y plantillas" description="Descarga plantillas, revisa columnas esperadas y valida datos antes de importar." onRefresh={() => undefined} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}
      <section className="page-grid page-grid-wide">
        <article className="panel section-card full-span">
          <PanelTitle title="Plantillas Excel" subtitle="Usa estas plantillas para evitar errores de formato." />
          <div className="download-grid">
            <a className="primary-button" href="/api/carga-masiva/plantilla-cartera">Descargar cartera</a>
            <a className="primary-button" href="/api/carga-masiva/plantilla-cartera-financiera">Descargar financiera</a>
            <a className="primary-button" href="/api/talleres/plantilla">Descargar talleres</a>
          </div>
          <div className="info-grid bulk-info-grid">
            <Info label="Cartera" value="NOMBRE/CLIENTE, COMPANIA SEGUROS, RAMO, CUOTAS, POLIZA, PRIMA TOTAL, VIGENCIA/FECHA INGRESO, vehiculo y CUOTA 1-12." />
            <Info label="Financiera" value="CLIENTE es el asegurado y FINANCIERA/CLIENTE FINANCIERA es el contratante (si aplica)." />
            <Info label="Talleres" value="NOMBRE, CIUDAD, ASEGURADORA, RAMO, TELEFONO, DIRECCION." />
            <Info label="Fechas" value="Usa dd/mm/aaaa o yyyy-mm-dd. Ejemplo: 31/12/2026." />
            <Info label="Telefonos" value="Acepta Honduras, USA, Espana, Panama y formato internacional. Se normaliza para WhatsApp Business." />
            <Info label="Tamano maximo" value="Cada archivo Excel puede pesar hasta 5 MB." />
          </div>
        </article>

        <article className="panel section-card full-span">
          <PanelTitle title="Cargar cartera" subtitle="Acepta cartera estandar y financiera en el mismo flujo, con vehiculo completo y hasta 12 cuotas." />
          <div className="upload-panel">
            <label className="file-drop">
              <Upload size={26} />
              <strong>{carteraFile ? carteraFile.name : 'Selecciona el archivo de cartera'}</strong>
              <span>Archivo .xlsx hasta 5 MB. Primero revisa el preview antes de importar.</span>
              <input
                type="file"
                accept=".xlsx"
                onChange={(event) => {
                  setCarteraFile(event.target.files?.[0] || null)
                  setCarteraPreview(null)
                  setMessage(null)
                  setError(null)
                }}
              />
            </label>
            {carteraPreview && (
              <div className={rejectedRows > 0 ? 'inline-alert warning' : 'inline-alert success'}>
                Importables: {importableRows}. Rechazadas: {rejectedRows}. Errores: {carteraPreview.errorCount}. Advertencias: {carteraPreview.warningCount}.
              </div>
            )}
            <div className="action-row">
              <button className="icon-button secondary" onClick={previewCarteraFile}><Search size={18} />Ver preview</button>
              <button className="icon-button secondary" onClick={() => setExpandedPreview(true)} disabled={!carteraPreview}><Maximize2 size={18} />Vista ampliada</button>
              <button className="icon-button secondary" onClick={downloadCarteraReport} disabled={!carteraFile}><Download size={18} />Reporte</button>
              <button className="icon-button secondary" onClick={downloadCleanCartera} disabled={!carteraFile}><Download size={18} />Excel limpio</button>
              <button className="primary-button" onClick={importCarteraFile} disabled={!carteraPreview || importableRows === 0}><Upload size={18} />Importar validas</button>
            </div>
          </div>
          <CarteraPreviewTable rows={carteraRows} />
        </article>

        <article className="panel section-card full-span">
          <PanelTitle title="Cargar talleres" subtitle="Primero revisa el preview. Si una fila tiene errores, la importacion queda bloqueada." />
          <div className="upload-panel">
            <label className="file-drop compact">
              <Upload size={24} />
              <strong>{file ? file.name : 'Selecciona el archivo de talleres'}</strong>
              <span>Valida talleres, ciudad, aseguradora y telefono antes de guardar.</span>
              <input
                type="file"
                accept=".xlsx"
                onChange={(event) => {
                  setFile(event.target.files?.[0] || null)
                  setPreview([])
                  setMessage(null)
                  setError(null)
                }}
              />
            </label>
            {hasPreviewErrors && <div className="inline-alert danger">Corrige las filas marcadas antes de importar.</div>}
            <div className="action-row">
              <button className="icon-button secondary" onClick={previewFile}><Search size={18} />Ver preview</button>
              <button className="primary-button" onClick={importFile} disabled={preview.length === 0 || hasPreviewErrors}><Upload size={18} />Importar</button>
            </div>
          </div>
          {preview.length === 0 && <div className="empty-state">Sube un archivo y usa Ver preview para revisar las filas antes de importar.</div>}
          <DataTable
            headers={['Fila', 'Taller', 'Ciudad', 'Resultado']}
            rows={preview.map((row) => [
              row.fila,
              row.taller.nombre,
              row.taller.ciudad,
              row.errores.length
                ? <StatusPill text={row.errores.join(', ')} tone="danger" />
                : <StatusPill text="Lista para importar" tone="success" />,
            ])}
          />
        </article>
      </section>
      {expandedPreview && carteraPreview && (
        <div className="preview-modal" role="dialog" aria-modal="true">
          <div className="preview-modal-content">
            <div className="preview-modal-head">
              <PanelTitle title="Vista ampliada de cartera" subtitle={`Importables: ${importableRows}. Rechazadas: ${rejectedRows}. Revisa originales, limpios, errores y advertencias.`} />
              <button className="icon-button secondary" onClick={() => setExpandedPreview(false)}><X size={18} />Cerrar</button>
            </div>
            <CarteraPreviewTable rows={carteraPreview.rows} expanded />
          </div>
        </div>
      )}
    </>
  )
}

function CarteraPreviewTable({ rows, expanded = false }: { rows: CarteraImportPreview['rows']; expanded?: boolean }) {
  const normalizeField = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^A-Z0-9]/gi, '').toUpperCase()
  if (rows.length === 0) {
    return <div className={expanded ? 'large-preview-table' : 'preview-table'}><div className="empty-state">Aun no hay filas para revisar. Selecciona un archivo y genera el preview.</div></div>
  }
  return (
    <div className={expanded ? 'large-preview-table' : 'preview-table'}>
      <DataTable
        headers={['Fila', 'Estado', 'Cliente', 'Telefono WhatsApp', 'Ready', 'Secundario', 'Correos', 'Poliza', 'Aseguradora', 'Vigencia/Hasta', 'Errores', 'Advertencias']}
        rows={rows.map((row) => {
          const get = (field: string) => row.values.find((value) => normalizeField(value.field) === normalizeField(field))
          return [
            row.rowNumber,
            row.estado === 'ERROR'
              ? <StatusPill text="Error" tone="danger" />
              : row.estado === 'CON_ADVERTENCIAS' ? <StatusPill text="Con advertencias" tone="warning" /> : <StatusPill text="Valida" tone="success" />,
            <OriginalClean original={get('NOMBRE')?.original} clean={get('NOMBRE')?.clean} />,
            <OriginalClean original={get('CONTACTO')?.original} clean={row.phoneNormalization.principal ? `${row.phoneNormalization.principal} / ${row.phoneNormalization.principalWhatsApp}` : ''} />,
            row.phoneNormalization.whatsappReady ? <StatusPill text="Si" tone="success" /> : <StatusPill text="No" tone="warning" />,
            row.phoneNormalization.secondary || <span className="muted-text">Vacio</span>,
            <OriginalClean original={get('CORREO')?.original} clean={[row.emailNormalization.principal, ...row.emailNormalization.extras].filter(Boolean).join(', ')} />,
            <OriginalClean original={get('POLIZA')?.original} clean={get('POLIZA')?.clean} />,
            <OriginalClean original={get('COMPANIA SEGUROS')?.original} clean={get('COMPANIA SEGUROS')?.clean} />,
            <div className="clean-cell"><span>{get('VIGENCIA')?.clean || 'Vacio'}</span><strong>{get('HASTA')?.clean || 'Vacio'}</strong></div>,
            <IssueList issues={row.errors} empty="Sin errores" />,
            <IssueList issues={row.warnings} empty="Sin advertencias" />,
          ]
        })}
      />
    </div>
  )
}

function IssueList({ issues, empty }: { issues: Array<{ field: string; message: string }>; empty: string }) {
  if (issues.length === 0) return <span className="muted-text">{empty}</span>
  return (
    <div className="issue-list">
      {issues.map((issue, index) => <span key={`${issue.field}-${index}`}><strong>{issue.field}:</strong> {issue.message}</span>)}
    </div>
  )
}

function OriginalClean({ original, clean }: { original?: string; clean?: string }) {
  const originalValue = original || 'Vacio'
  const cleanValue = clean || 'Vacio'
  return (
    <div className="clean-cell">
      <span>{originalValue}</span>
      <strong>{cleanValue}</strong>
    </div>
  )
}
