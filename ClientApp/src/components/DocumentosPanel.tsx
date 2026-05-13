import { useEffect, useRef, useState } from 'react'
import { Download, FileImage, FileText, Trash2, Upload } from 'lucide-react'
import { deleteDocumento, getDocumentos, updateDocumentoObservacion, uploadDocumento } from '../api/documentosApi'
import type { DocumentItem, EntityType } from '../types/documentos'
import { dateFmt } from '../utils/formatters'
import { useAuth } from '../hooks/useAuth'
import { ConfirmDialog } from './ConfirmDialog'
import { notify } from './ToastHost'

const maxDocumentBytes = 5 * 1024 * 1024
const allowedDocumentExtensions = ['pdf', 'jpg', 'jpeg', 'png', 'webp', 'txt', 'doc', 'docx', 'xls', 'xlsx']
const documentTypes = ['POLIZA', 'IDENTIDAD', 'RTN', 'RECIBO', 'COMPROBANTE_TRANSFERENCIA', 'COMPROBANTE_DEBITO', 'FACTURA', 'FOTO_RECLAMO', 'COTIZACION_TALLER', 'INFORME_TALLER', 'FINIQUITO', 'LICENCIA', 'TARJETA_CIRCULACION', 'OTRO']

export function DocumentosPanel({ entidadTipo, entidadId, compact = false }: { entidadTipo: EntityType; entidadId: number; compact?: boolean }) {
  const [items, setItems] = useState<DocumentItem[]>([])
  const [tipoDocumento, setTipoDocumento] = useState('OTRO')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<DocumentItem | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [previewItem, setPreviewItem] = useState<DocumentItem | null>(null)
  const [previewText, setPreviewText] = useState<string>('')
  const [selectedFileName, setSelectedFileName] = useState('')
  const [observacion, setObservacion] = useState('')
  const [observacionesDraft, setObservacionesDraft] = useState<Record<number, string>>({})
  const [savingObservationId, setSavingObservationId] = useState<number | null>(null)
  const inputRef = useRef<HTMLInputElement | null>(null)
  const { hasPermission } = useAuth()
  const canUpload = hasPermission('documentos.subir')
  const canDelete = hasPermission('documentos.eliminar')
  const compactMode = compact || entidadTipo === 'PAGO'

  async function load() {
    setError(null)
    try {
      const data = await getDocumentos(entidadTipo, entidadId)
      setItems(data.items)
      setObservacionesDraft(Object.fromEntries(data.items.map((item) => [item.id, item.observacion || ''])))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function upload(file: File | null | undefined) {
    const validation = validateDocumentFile(file)
    if (validation) {
      setError(validation)
      return
    }
    if (!file) return

    setError(null)
    setMessage(null)
    setUploading(true)
    try {
      await uploadDocumento(entidadTipo, entidadId, tipoDocumento, file, observacion)
      notify('Documento subido correctamente.', 'success')
      setMessage('Documento subido correctamente.')
      if (inputRef.current) inputRef.current.value = ''
      setSelectedFileName('')
      setObservacion('')
      await load()
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error inesperado.'
      setError(text)
      notify(text, 'error')
    } finally {
      setUploading(false)
    }
  }

  async function remove(item: DocumentItem) {
    setError(null)
    setMessage(null)
    setDeleting(true)
    try {
      await deleteDocumento(item.id)
      notify('Documento eliminado.', 'success')
      setMessage('Documento eliminado.')
      setDeleteTarget(null)
      await load()
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error inesperado.'
      setError(text)
      notify(text, 'error')
    } finally {
      setDeleting(false)
    }
  }

  useEffect(() => {
    let alive = true
    getDocumentos(entidadTipo, entidadId)
      .then((data) => {
        if (!alive) return
        setItems(data.items)
        setObservacionesDraft(Object.fromEntries(data.items.map((item) => [item.id, item.observacion || ''])))
        setError(null)
        setSelectedFileName('')
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })

    return () => {
      alive = false
    }
  }, [entidadTipo, entidadId])

  useEffect(() => {
    if (!message) return
    const timeout = window.setTimeout(() => setMessage(null), 3500)
    return () => window.clearTimeout(timeout)
  }, [message])

  useEffect(() => {
    if (!previewItem || (previewItem.mimeType || '').toLowerCase() !== 'text/plain') return
    fetch(previewItem.verUrl, { credentials: 'include' })
      .then((r) => (r.ok ? r.text() : 'No se pudo cargar el texto.'))
      .then(setPreviewText)
      .catch(() => setPreviewText('No se pudo cargar el texto.'))
  }, [previewItem])

  async function saveObservation(item: DocumentItem) {
    setSavingObservationId(item.id)
    setError(null)
    setMessage(null)
    try {
      const nextValue = observacionesDraft[item.id] ?? ''
      await updateDocumentoObservacion(item.id, nextValue)
      setItems((current) => current.map((doc) => doc.id === item.id ? { ...doc, observacion: nextValue } : doc))
      setMessage('Observacion actualizada.')
      notify('Observacion actualizada.', 'success')
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Error inesperado.'
      setError(text)
      notify(text, 'error')
    } finally {
      setSavingObservationId(null)
    }
  }

  return (
    <section className={`documents-panel ${compactMode ? 'compact-documents-panel' : ''} ${entidadTipo === 'RECLAMO' ? 'claim-documents-panel' : ''}`}>
      <div className="documents-head">
        <div>
          <h3>{compactMode ? 'Comprobante' : 'Documentos'}</h3>
          {!compactMode && <p>PDF, JPG o PNG hasta 5 MB. Los archivos quedan asociados al expediente.</p>}
        </div>
        <label className="field compact-field">
          <span>Tipo</span>
          <select value={tipoDocumento} onChange={(event) => setTipoDocumento(event.target.value)}>
            {documentTypes.map((type) => <option key={type} value={type}>{type}</option>)}
          </select>
        </label>
        {entidadTipo === 'RECLAMO' && (
          <label className="field compact-field document-observation-field">
            <span>Observacion</span>
            <input value={observacion} onChange={(event) => setObservacion(event.target.value)} placeholder="Nota del adjunto" />
          </label>
        )}
      </div>

      {error && <div className="inline-alert danger">{error}</div>}
      {message && <div className="inline-alert success">{message}</div>}

      {canUpload && (
        <div
          className={`dropzone ${compactMode ? 'compact' : ''} ${dragging ? 'active' : ''}`}
          onDragOver={(event) => {
            event.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(event) => {
            event.preventDefault()
            setDragging(false)
            void upload(event.dataTransfer.files[0])
          }}
        >
          <Upload size={22} />
          <strong>{uploading ? 'Subiendo archivo...' : compactMode ? 'Adjuntar comprobante' : 'Arrastra un archivo o seleccionalo'}</strong>
          {!compactMode && <span>No se exponen rutas internas del servidor.</span>}
          <div className="dropzone-picker">
            <button
              className="icon-button secondary"
              type="button"
              onClick={() => inputRef.current?.click()}
            >
              Elegir archivo
            </button>
            <span className="dropzone-file-name">{selectedFileName || 'Ningun archivo seleccionado'}</span>
          </div>
          <input
            ref={inputRef}
            type="file"
            accept=".pdf,.jpg,.jpeg,.png,.webp,.txt,.doc,.docx,.xls,.xlsx"
            onChange={(event) => {
              const file = event.target.files?.[0]
              setSelectedFileName(file?.name || '')
              void upload(file)
            }}
          />
        </div>
      )}

      <div className="documents-list">
        {items.length === 0 ? (
          <div className="empty">No hay documentos cargados.</div>
        ) : (
          items.map((item) => (
            <div className={`document-row ${compactMode ? 'compact' : ''} ${entidadTipo === 'RECLAMO' ? 'claim-document-row' : ''}`} key={item.id}>
              {isImage(item.extension) ? <FileImage size={20} /> : <FileText size={20} />}
              <div>
                <strong>{item.nombreArchivoOriginal}</strong>
                <span>{item.tipoDocumento} / {dateFmt.format(new Date(item.fechaSubida))} / {item.usuario || 'Sistema'} / {formatBytes(item.tamanoBytes)}</span>
              </div>
              {entidadTipo === 'RECLAMO' && (
                <label className="document-observation-cell">
                  <span>Observacion</span>
                  <textarea
                    value={observacionesDraft[item.id] ?? item.observacion ?? ''}
                    rows={2}
                    onChange={(event) => setObservacionesDraft((current) => ({ ...current, [item.id]: event.target.value }))}
                  />
                  <button
                    className="icon-button secondary compact"
                    disabled={savingObservationId === item.id || (observacionesDraft[item.id] ?? '') === (item.observacion ?? '')}
                    onClick={() => void saveObservation(item)}
                  >
                    Guardar
                  </button>
                </label>
              )}
              <div className="table-actions">
                <button className="icon-button secondary" onClick={() => { setPreviewText(''); setPreviewItem(item) }}><FileText size={16} />Preview</button>
                <a className="icon-button secondary" href={item.descargarUrl} target="_blank" rel="noreferrer"><Download size={16} />Descargar</a>
                {canDelete && <button className="icon-button danger-button" onClick={() => setDeleteTarget(item)}><Trash2 size={16} />Eliminar</button>}
              </div>
            </div>
          ))
        )}
      </div>

      {previewItem && (
        <div className="modal-backdrop" onClick={() => { setPreviewItem(null); setPreviewText('') }}>
          <div className="panel documents-preview-modal" onClick={(event) => event.stopPropagation()}>
            <PanelHeader title={`Vista previa: ${previewItem.nombreArchivoOriginal}`} onClose={() => { setPreviewItem(null); setPreviewText('') }} />
            {renderPreview(previewItem, previewText)}
          </div>
        </div>
      )}
      <ConfirmDialog
        open={Boolean(deleteTarget)}
        title="Eliminar documento"
        message={deleteTarget ? `Se eliminara ${deleteTarget.nombreArchivoOriginal}. Esta accion requiere confirmacion.` : ''}
        confirmText="Eliminar"
        danger
        busy={deleting}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => deleteTarget && void remove(deleteTarget)}
      />
    </section>
  )
}

function PanelHeader({ title, onClose }: { title: string; onClose: () => void }) {
  return (
    <div className="panel-header documents-preview-head">
      <h3>{title}</h3>
      <button className="icon-button secondary" onClick={onClose}>Cerrar</button>
    </div>
  )
}

function renderPreview(item: DocumentItem, textContent: string) {
  const mime = (item.mimeType || '').toLowerCase()
  if (mime === 'application/pdf') {
    return <iframe className="documents-preview-frame" src={item.verUrl} title={item.nombreArchivoOriginal} />
  }
  if (mime.startsWith('image/')) {
    return <img className="documents-preview-image" src={item.verUrl} alt={item.nombreArchivoOriginal} />
  }
  if (mime === 'text/plain') {
    return <pre className="documents-preview-text">{textContent || 'Cargando texto...'}</pre>
  }
  return <div className="empty">Este archivo no se puede previsualizar. Puede descargarlo.</div>
}

function validateDocumentFile(file: File | null | undefined) {
  if (!file) return 'Selecciona un archivo.'
  if (file.size > maxDocumentBytes) return 'El archivo supera el limite permitido de 5 MB.'

  const extension = file.name.split('.').pop()?.toLowerCase() || ''
  if (!allowedDocumentExtensions.includes(extension)) return 'Solo se permiten PDF, imagenes, Word, Excel o texto.'

  return null
}

function isImage(extension: string) {
  return ['jpg', 'jpeg', 'png'].includes(extension.toLowerCase())
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}
