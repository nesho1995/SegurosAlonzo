import { useEffect, useMemo, useState } from 'react'
import { Download, Pencil, Power, Save, Search, Upload, X } from 'lucide-react'
import {
  cambiarEstadoTaller,
  getTalleres,
  getTalleresDetectados,
  importarTalleres,
  previewTalleres,
  resolverTallerDetectado,
  saveTaller,
} from '../api/talleresApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle } from '../components/FormControls'
import { PageHeader } from '../components/Topbar'
import type { DetectedWorkshop, Workshop, WorkshopImportRow } from '../types/talleres'
import { statusLabel } from '../utils/labels'
import { useAuth } from '../hooks/useAuth'

const emptyWorkshop: Workshop = {
  id: 0,
  nombre: '',
  ciudad: '',
  zona: '',
  direccion: '',
  telefono: '',
  whatsApp: '',
  email: '',
  contacto: '',
  aseguradora: '',
  ramo: '',
  activo: true,
  esPreferido: false,
  ordenPrioridad: 100,
  observaciones: '',
  aseguradorasAceptadas: [],
  ramosAtendidos: [],
}

function splitMulti(value: string) {
  return value.split(';').map((item) => item.trim()).filter(Boolean)
}

function BadgeList({ items, fallback }: { items: string[]; fallback?: string }) {
  const values = items.length ? items : (fallback ? [fallback] : [])
  return <div className="inline-badges">{values.map((item) => <span key={item}>{item}</span>)}</div>
}

export function WorkshopsView() {
  const [items, setItems] = useState<Workshop[]>([])
  const [detected, setDetected] = useState<DetectedWorkshop[]>([])
  const [buscar, setBuscar] = useState('')
  const [estado, setEstado] = useState('ACTIVO')
  const [ciudad, setCiudad] = useState('')
  const [aseguradora, setAseguradora] = useState('')
  const [form, setForm] = useState<Workshop>(emptyWorkshop)
  const [aseguradorasText, setAseguradorasText] = useState('')
  const [ramosText, setRamosText] = useState('')
  const [importFile, setImportFile] = useState<File | null>(null)
  const [previewRows, setPreviewRows] = useState<WorkshopImportRow[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const { hasPermission } = useAuth()
  const canEdit = hasPermission('talleres.editar')

  const previewSummary = useMemo(() => {
    const errores = previewRows.filter((row) => row.errores.length > 0).length
    return { validos: previewRows.length - errores, errores }
  }, [previewRows])

  function buildQuery() {
    const query = new URLSearchParams({ estado })
    if (buscar.trim()) query.set('buscar', buscar.trim())
    if (ciudad.trim()) query.set('ciudad', ciudad.trim())
    if (aseguradora.trim()) query.set('aseguradora', aseguradora.trim())
    return query
  }

  async function load() {
    setError(null)
    try {
      const data = await getTalleres(buildQuery())
      const pending = await getTalleresDetectados()
      setItems(data.items)
      setDetected(pending.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  function editWorkshop(item: Workshop) {
    setForm({
      ...emptyWorkshop,
      ...item,
      aseguradorasAceptadas: item.aseguradorasAceptadas?.length ? item.aseguradorasAceptadas : splitMulti(item.aseguradora || ''),
      ramosAtendidos: item.ramosAtendidos?.length ? item.ramosAtendidos : splitMulti(item.ramo || ''),
    })
    setAseguradorasText((item.aseguradorasAceptadas?.length ? item.aseguradorasAceptadas : splitMulti(item.aseguradora || '')).join('; '))
    setRamosText((item.ramosAtendidos?.length ? item.ramosAtendidos : splitMulti(item.ramo || '')).join('; '))
  }

  async function save() {
    setError(null)
    setMessage(null)
    try {
      const payload = {
        ...form,
        aseguradorasAceptadas: splitMulti(aseguradorasText),
        ramosAtendidos: splitMulti(ramosText),
      }
      await saveTaller(payload)
      setForm(emptyWorkshop)
      setAseguradorasText('')
      setRamosText('')
      setMessage('Taller guardado.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function toggleWorkshop(item: Workshop) {
    setError(null)
    setMessage(null)
    try {
      await cambiarEstadoTaller(item)
      setMessage(item.activo ? 'Taller inactivado.' : 'Taller activado.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function detectedAction(id: number, action: 'aprobar' | 'descartar') {
    await resolverTallerDetectado(id, action)
    await load()
  }

  async function previewImport() {
    if (!importFile) return
    setError(null)
    const data = await previewTalleres(importFile)
    setPreviewRows(data.items)
  }

  async function runImport() {
    if (!importFile) return
    setError(null)
    const result = await importarTalleres(importFile)
    setMessage(`Importacion finalizada: ${result.importados} importados, ${result.rechazados} rechazados.`)
    setPreviewRows(result.errores)
    await load()
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ estado })
    if (buscar.trim()) query.set('buscar', buscar.trim())
    if (ciudad.trim()) query.set('ciudad', ciudad.trim())
    if (aseguradora.trim()) query.set('aseguradora', aseguradora.trim())

    Promise.all([getTalleres(query), getTalleresDetectados()])
      .then(([data, pending]) => {
        if (!alive) return
        setItems(data.items)
        setDetected(pending.items)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })
    return () => { alive = false }
  }, [estado, buscar, ciudad, aseguradora])

  return (
    <>
      <PageHeader eyebrow="Catalogo" title="Talleres inteligentes" description="Administra talleres por ciudad, aseguradora y ramo para sugerirlos en reclamos." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      {message && <div className="inline-alert success">{message}</div>}

      <article className="panel form-panel">
        <PanelTitle title="Filtros" subtitle="Ubica talleres por texto, ciudad, aseguradora o estado." />
        <div className="form-grid">
          <Field label="Buscar" value={buscar} onChange={setBuscar} />
          <Field label="Ciudad" value={ciudad} onChange={setCiudad} />
          <Field label="Aseguradora" value={aseguradora} onChange={setAseguradora} />
          <label className="field">
            <span>Estado</span>
            <select value={estado} onChange={(event) => setEstado(event.target.value)}>
              {['ACTIVO', 'INACTIVO', 'TODOS'].map((item) => <option value={item} key={item}>{statusLabel(item)}</option>)}
            </select>
          </label>
          <div className="form-actions">
            <button className="primary-button" onClick={load}><Search size={18} />Filtrar</button>
          </div>
        </div>
      </article>

      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Catalogo de talleres" subtitle="La sugerencia usa preferido, prioridad, ciudad, aseguradora y ramo." />
          <DataTable
            headers={['Nombre', 'Ciudad', 'Aseguradoras', 'Ramos', 'Prioridad', 'Estado', 'Acciones']}
            rows={items.map((item) => [
              <CellTitle title={item.nombre} subtitle={item.direccion || item.zona || 'Sin direccion'} />,
              item.ciudad,
              <BadgeList items={item.aseguradorasAceptadas || []} fallback={item.aseguradora} />,
              <BadgeList items={item.ramosAtendidos || []} fallback={item.ramo || 'GENERAL'} />,
              item.esPreferido ? `Preferido / ${item.ordenPrioridad || 100}` : item.ordenPrioridad || 100,
              <StatusPill text={item.activo ? 'Activo' : 'Inactivo'} tone={item.activo ? 'success' : 'slate'} />,
              canEdit ? <div className="table-actions">
                <button className="icon-button secondary" onClick={() => editWorkshop(item)} title="Editar taller"><Pencil size={16} />Editar</button>
                <button className={item.activo ? 'icon-button danger-button' : 'primary-button'} onClick={() => void toggleWorkshop(item)} title={item.activo ? 'Inactivar taller' : 'Activar taller'}>
                  <Power size={16} />{item.activo ? 'Inactivar' : 'Activar'}
                </button>
              </div> : 'Sin permiso',
            ])}
          />
        </article>

        {canEdit && <article className="panel">
          <PanelTitle title={form.id ? 'Editar taller' : 'Nuevo taller'} subtitle="Usa punto y coma para varias aseguradoras o ramos." />
          <div className="form-grid">
            <Field label="Nombre" value={form.nombre} onChange={(value) => setForm({ ...form, nombre: value })} />
            <Field label="Ciudad" value={form.ciudad} onChange={(value) => setForm({ ...form, ciudad: value })} />
            <Field label="Zona" value={form.zona || ''} onChange={(value) => setForm({ ...form, zona: value })} />
            <Field label="Telefono" value={form.telefono || ''} onChange={(value) => setForm({ ...form, telefono: value })} />
            <Field label="WhatsApp" value={form.whatsApp || ''} onChange={(value) => setForm({ ...form, whatsApp: value })} />
            <Field label="Email" value={form.email || ''} onChange={(value) => setForm({ ...form, email: value })} />
            <Field label="Contacto" value={form.contacto || ''} onChange={(value) => setForm({ ...form, contacto: value })} />
            <Field label="Aseguradoras" value={aseguradorasText} onChange={setAseguradorasText} />
            <Field label="Ramos" value={ramosText} onChange={setRamosText} />
            <Field label="Prioridad" type="number" value={String(form.ordenPrioridad || 100)} onChange={(value) => setForm({ ...form, ordenPrioridad: Number(value) || 100 })} />
            <label className="check-row"><input type="checkbox" checked={Boolean(form.esPreferido)} onChange={(event) => setForm({ ...form, esPreferido: event.target.checked })} />Preferido</label>
            <label className="check-row"><input type="checkbox" checked={form.activo} onChange={(event) => setForm({ ...form, activo: event.target.checked })} />Activo</label>
            <label className="wide-field">
              <span>Direccion</span>
              <textarea value={form.direccion || ''} onChange={(event) => setForm({ ...form, direccion: event.target.value })} />
            </label>
            <label className="wide-field">
              <span>Observaciones</span>
              <textarea value={form.observaciones || ''} onChange={(event) => setForm({ ...form, observaciones: event.target.value })} />
            </label>
            <div className="form-actions">
              <button className="primary-button" onClick={save}><Save size={18} />Guardar taller</button>
              {form.id > 0 && <button className="icon-button secondary" onClick={() => { setForm(emptyWorkshop); setAseguradorasText(''); setRamosText('') }}><X size={18} />Cancelar</button>}
            </div>
          </div>
        </article>}
      </section>

      {canEdit && <article className="panel mt-panel">
        <PanelTitle title="Importar talleres" subtitle="Puedes cargar CSV o XLSX. Las filas validas se importan aunque otras tengan errores." />
        <div className="form-grid">
          <label className="wide-field">
            <span>Archivo</span>
            <input type="file" accept=".csv,.xlsx" onChange={(event) => { setImportFile(event.target.files?.[0] || null); setPreviewRows([]) }} />
          </label>
          <div className="form-actions">
            <a className="icon-button secondary" href="/api/talleres/plantilla-csv"><Download size={18} />Plantilla CSV</a>
            <a className="icon-button secondary" href="/api/talleres/plantilla"><Download size={18} />Plantilla Excel</a>
            <button className="icon-button secondary" disabled={!importFile} onClick={() => void previewImport()}><Search size={18} />Previsualizar</button>
            <button className="primary-button" disabled={!importFile} onClick={() => void runImport()}><Upload size={18} />Importar</button>
          </div>
        </div>
        {previewRows.length > 0 && <>
          <div className={previewSummary.errores > 0 ? 'inline-alert warning' : 'inline-alert success'}>
            {previewSummary.validos} filas validas / {previewSummary.errores} con errores.
          </div>
          <DataTable
            headers={['Fila', 'Taller', 'Ciudad', 'Aseguradoras', 'Errores']}
            rows={previewRows.slice(0, 30).map((row) => [
              row.fila,
              row.taller.nombre || 'Sin nombre',
              row.taller.ciudad || 'Sin ciudad',
              row.taller.aseguradorasAceptadas.join(', ') || row.taller.aseguradora || 'Sin aseguradora',
              row.errores.length ? row.errores.join(' ') : 'OK',
            ])}
          />
        </>}
      </article>}

      <article className="panel mt-panel">
        <PanelTitle title="Talleres detectados" subtitle="Bandeja de posibles talleres mencionados en correos." />
        <DataTable
          headers={['Nombre', 'Ciudad', 'Aseguradora', 'Accion']}
          rows={detected.map((item) => [
            item.nombre,
            item.ciudad || 'Sin ciudad',
            item.aseguradora || 'Sin aseguradora',
            canEdit ? <div className="form-actions"><button className="primary-button" onClick={() => void detectedAction(item.id, 'aprobar')}>Aprobar</button><button className="icon-button secondary" onClick={() => void detectedAction(item.id, 'descartar')}>Descartar</button></div> : 'Sin permiso',
          ])}
        />
      </article>
    </>
  )
}
