import { useEffect, useMemo, useState } from 'react'
import { Plus, Save } from 'lucide-react'
import { getCatalogoByTipo, getCatalogoTipos, saveCatalogoItem, setCatalogoActivo } from '../api/catalogosApi'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle, SelectField } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { PageHeader } from '../components/Topbar'
import type { CatalogoItem } from '../types/catalogos'

const baseTipos = ['RAMOS', 'COMPANIAS', 'FORMAS_PAGO', 'ESTADOS_POLIZA', 'ESTADOS_PAGO', 'TIPO_PROCESO', 'ESTADO_REVISION', 'MEDIOS', 'CIUDADES', 'ENDOSATARIOS', 'SUCURSALES', 'AGENTES_REFERIDORES']

export function CatalogosView() {
  const [tipos, setTipos] = useState<string[]>(baseTipos)
  const [tipo, setTipo] = useState('RAMOS')
  const [items, setItems] = useState<CatalogoItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [draft, setDraft] = useState({ nombre: '', codigo: '', descripcion: '' })

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const [tiposResp, itemsResp] = await Promise.all([
        getCatalogoTipos().catch(() => ({ tipos: [] })),
        getCatalogoByTipo(tipo, true),
      ])
      const merged = Array.from(new Set([...baseTipos, ...tiposResp.tipos]))
      setTipos(merged)
      setItems(itemsResp.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar catalogos.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    Promise.all([
      getCatalogoTipos().catch(() => ({ tipos: [] })),
      getCatalogoByTipo(tipo, true),
    ])
      .then(([tiposResp, itemsResp]) => {
        if (!alive) return
        const merged = Array.from(new Set([...baseTipos, ...tiposResp.tipos]))
        setTipos(merged)
        setItems(itemsResp.items)
      })
      .catch((err) => {
        if (!alive) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar catalogos.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })
    return () => { alive = false }
  }, [tipo])

  async function save() {
    if (!draft.nombre.trim()) return
    await saveCatalogoItem({
      tipoCatalogo: tipo,
      nombre: draft.nombre.trim(),
      codigo: draft.codigo.trim() || draft.nombre.trim(),
      descripcion: draft.descripcion.trim() || undefined,
      activo: true,
      orden: 999,
      esDefault: false,
      pendienteRevision: false,
    })
    setDraft({ nombre: '', codigo: '', descripcion: '' })
    await load()
  }

  async function toggle(item: CatalogoItem) {
    await setCatalogoActivo(item.id, !item.activo)
    await load()
  }

  const activeCount = useMemo(() => items.filter(x => x.activo).length, [items])

  if (loading) return <LoadingCard text="Cargando catalogos..." />
  return (
    <>
      <PageHeader eyebrow="Admin" title="Catalogos" description="Administra valores desplegables sin tocar codigo." onRefresh={load} />
      {error && <ErrorCard text={error} />}
      <section className="content-grid">
        <article className="panel">
          <PanelTitle title="Tipo de catalogo" subtitle={`${activeCount}/${items.length} activos`} />
          <div className="form-grid">
            <SelectField label="Catalogo" value={tipo} options={tipos} onChange={setTipo} />
            <Field label="Nombre" value={draft.nombre} onChange={(value) => setDraft({ ...draft, nombre: value })} />
            <Field label="Codigo" value={draft.codigo} onChange={(value) => setDraft({ ...draft, codigo: value })} />
            <Field label="Descripcion" value={draft.descripcion} onChange={(value) => setDraft({ ...draft, descripcion: value })} />
            <div className="form-actions">
              <button className="primary-button" onClick={() => void save()}><Plus size={16} />Agregar</button>
            </div>
          </div>
        </article>
        <article className="panel">
          <PanelTitle title="Valores" subtitle="Activa/desactiva sin borrar historico." />
          <div className="client-list">
            {items.map((item) => (
              <div className="client-row" key={item.id}>
                <span>
                  <strong>{item.nombre}</strong>
                  <small>{item.codigo} · {item.pendienteRevision ? 'Pendiente revision' : 'OK'}</small>
                </span>
                <button className="icon-button secondary" onClick={() => void toggle(item)}><Save size={14} />{item.activo ? 'Desactivar' : 'Activar'}</button>
              </div>
            ))}
          </div>
        </article>
      </section>
    </>
  )
}
