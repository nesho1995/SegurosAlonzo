import { useEffect, useState } from 'react'
import { Bot, CheckCircle2, FlaskConical, PauseCircle, Plus, Trash2 } from 'lucide-react'
import { createAutomatizacion, deleteAutomatizacion, getAutomatizaciones, testAutomatizacion, updateAutomatizacion } from '../api/automatizacionesApi'
import { StatusPill } from '../components/Badge'
import { CellTitle, DataTable } from '../components/DataTable'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, PanelTitle, SelectField } from '../components/FormControls'
import { LoadingCard } from '../components/LoadingState'
import { Metric } from '../components/StatCard'
import { PageHeader } from '../components/Topbar'
import { useAuth } from '../hooks/useAuth'
import type { AutomationAction, AutomationCondition, AutomationRequest, AutomationResponse, AutomationRule, AutomationTestResult } from '../types/automatizaciones'
import { dateFmt } from '../utils/formatters'
import { statusLabel } from '../utils/labels'

const eventOptions = ['pago_vencido', 'reclamo_nuevo', 'cliente_creado', 'pago_registrado', 'poliza_por_vencer']
const fieldOptions = ['estado', 'monto', 'cliente', 'aseguradora', 'ramo', 'telefono']
const operatorOptions = ['=', '!=', '>', '>=', '<', '<=', 'contiene', 'no_contiene', 'existe', 'vacio']
const actionOptions = ['whatsapp', 'email', 'actualizar_estado', 'asignar_taller']

const emptyCondition: AutomationCondition = { campo: 'estado', operador: '=', valor: 'VENCIDA' }
const emptyAction: AutomationAction = { tipoAccion: 'whatsapp', parametrosJson: '' }

export function AutomationView() {
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('automatizaciones.crear')
  const canEdit = hasPermission('automatizaciones.editar')
  const canDelete = hasPermission('automatizaciones.eliminar')
  const [data, setData] = useState<AutomationResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<AutomationRule | null>(null)
  const [form, setForm] = useState<AutomationRequest>(newRule())
  const [testResult, setTestResult] = useState<AutomationTestResult | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await getAutomatizaciones())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar las automatizaciones.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let alive = true
    getAutomatizaciones()
      .then((json) => {
        if (alive) setData(json)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'No se pudieron cargar las automatizaciones.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [])

  function edit(rule: AutomationRule) {
    setSelected(rule)
    setTestResult(null)
    setForm({
      nombre: rule.nombre,
      activo: rule.activo,
      tipoEvento: rule.tipoEvento,
      empresaId: rule.empresaId,
      condiciones: rule.condiciones.length ? rule.condiciones.map(({ campo, operador, valor }) => ({ campo, operador, valor })) : [{ ...emptyCondition }],
      acciones: rule.acciones.length ? rule.acciones.map(({ tipoAccion, parametrosJson }) => ({ tipoAccion, parametrosJson })) : [{ ...emptyAction }],
    })
  }

  async function save() {
    if (!canCreate && !canEdit) return
    const validation = validateRule(form)
    if (validation) {
      setError(validation)
      return
    }
    setSaving(true)
    setError(null)
    try {
      if (selected) await updateAutomatizacion(selected.id, form)
      else await createAutomatizacion(form)
      setSelected(null)
      setForm(newRule())
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar la regla.')
    } finally {
      setSaving(false)
    }
  }

  async function remove(id: number) {
    if (!canDelete) return
    if (!window.confirm('Confirmas eliminar esta regla de automatizacion? Esta accion no borra el historial.')) return
    setSaving(true)
    setError(null)
    try {
      await deleteAutomatizacion(id)
      if (selected?.id === id) {
        setSelected(null)
        setForm(newRule())
      }
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar la regla.')
    } finally {
      setSaving(false)
    }
  }

  async function runTest() {
    setSaving(true)
    setTestResult(null)
    setError(null)
    try {
      const datos: Record<string, string | number | boolean | null> = {}
      for (const condition of form.condiciones) datos[condition.campo] = condition.valor ?? ''
      setTestResult(await testAutomatizacion({ tipoEvento: form.tipoEvento, entidadTipo: 'PRUEBA', datos }))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo probar la regla.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Automatizacion"
        title="Motor de reglas"
        description="Define reglas SI / ENTONCES para pagos, reclamos, clientes y polizas con una configuracion guiada."
        onRefresh={load}
      />
      {loading && <LoadingCard text="Cargando automatizaciones..." />}
      {error && <ErrorCard text={error} />}
      {data && (
        <>
          <section className="mini-grid">
            <Metric title="Reglas activas" value={data.items.filter((item) => item.activo).length} hint="Listas para evaluar" tone="green" icon={CheckCircle2} />
            <Metric title="Reglas pausadas" value={data.items.filter((item) => !item.activo).length} hint="No se ejecutan" tone="amber" icon={PauseCircle} />
            <Metric title="Ejecuciones" value={data.logs.length} hint="Ultimos eventos" tone="blue" icon={Bot} />
          </section>

          <section className="split-grid">
            <article className="panel">
              <PanelTitle title="Reglas configuradas" subtitle="Activa, edita o elimina reglas del motor." />
              <DataTable
                headers={['Regla', 'Evento', 'Estado', 'Acciones']}
                rows={data.items.map((item) => [
                  <CellTitle title={item.nombre} subtitle={`${item.condiciones.length} condiciones / ${item.acciones.length} acciones`} />,
                  eventLabel(item.tipoEvento),
                  <StatusPill text={item.activo ? 'Activa' : 'Pausada'} tone={item.activo ? 'success' : 'warning'} />,
                  <div className="form-actions">
                    <button className="icon-button secondary" onClick={() => edit(item)}>Editar</button>
                    {canDelete && <button className="icon-button danger" onClick={() => void remove(item.id)}><Trash2 size={16} />Eliminar</button>}
                  </div>,
                ])}
              />
            </article>

            <article className="panel">
              <PanelTitle title="Datos de la regla" subtitle={selected ? 'Edita la regla seleccionada.' : 'Crea una regla nueva con datos completos.'} />
              <div className="form-grid">
                <Field label="Nombre" value={form.nombre} onChange={(value) => setForm({ ...form, nombre: value })} />
                <SelectField label="Cuando se ejecuta" value={form.tipoEvento} options={eventOptions} onChange={(value) => setForm({ ...form, tipoEvento: value })} />
                <label className="check-field"><input type="checkbox" checked={form.activo} onChange={(event) => setForm({ ...form, activo: event.target.checked })} />Activa</label>
              </div>

              <RuleConditions conditions={form.condiciones} onChange={(condiciones) => setForm({ ...form, condiciones })} />
              <RuleActions actions={form.acciones} onChange={(acciones) => setForm({ ...form, acciones })} />

              <div className="state-card">
                <strong>Resumen antes de guardar</strong>
                <p>{ruleSummary(form)}</p>
              </div>

              <div className="form-actions">
                {(canCreate || canEdit) && <button className="primary-button" disabled={saving} onClick={() => void save()}>{saving ? 'Guardando...' : 'Guardar regla'}</button>}
                <button className="icon-button secondary" disabled={saving} onClick={() => void runTest()}><FlaskConical size={16} />Probar</button>
                {selected && <button className="icon-button secondary" onClick={() => { setSelected(null); setForm(newRule()); setTestResult(null) }}>Nueva</button>}
              </div>

              {testResult && (
                <div className="state-card">
                  <strong>Resultado de prueba</strong>
                  <p>{testResult.reglasCoincidentes} de {testResult.reglasEvaluadas} reglas coinciden.</p>
                  {testResult.mensajes.map((message) => <p key={message}>{message}</p>)}
                </div>
              )}
            </article>
          </section>

          <article className="panel mt-panel">
            <PanelTitle title="Ultimas ejecuciones" subtitle="Historial operativo del motor." />
            <DataTable
              headers={['Regla', 'Entidad', 'Resultado', 'Fecha']}
              rows={data.logs.map((item) => [
                <CellTitle title={item.automatizacion} subtitle={item.mensaje} />,
                `${item.entidadTipo}${item.entidadId ? ` #${item.entidadId}` : ''}`,
                statusLabel(item.resultado),
                dateFmt.format(new Date(item.fecha)),
              ])}
            />
          </article>
        </>
      )}
    </>
  )
}

function RuleConditions({ conditions, onChange }: { conditions: AutomationCondition[]; onChange: (items: AutomationCondition[]) => void }) {
  return (
    <div className="stack-list">
      <PanelTitle title="Condiciones" subtitle="Todas deben cumplirse para preparar la accion." />
      {conditions.map((item, index) => (
        <div className="form-grid" key={index}>
          <SelectField label="Campo" value={item.campo} options={fieldOptions} onChange={(value) => updateAt(conditions, index, { ...item, campo: value }, onChange)} />
          <SelectField label="Operador" value={item.operador} options={operatorOptions} onChange={(value) => updateAt(conditions, index, { ...item, operador: value }, onChange)} />
          <Field label="Valor" value={item.valor ?? ''} onChange={(value) => updateAt(conditions, index, { ...item, valor: value }, onChange)} />
          <button className="icon-button secondary" onClick={() => onChange(conditions.filter((_, i) => i !== index))}><Trash2 size={16} />Quitar</button>
        </div>
      ))}
      <button className="icon-button secondary" onClick={() => onChange([...conditions, { ...emptyCondition }])}><Plus size={16} />Agregar condicion</button>
    </div>
  )
}

function RuleActions({ actions, onChange }: { actions: AutomationAction[]; onChange: (items: AutomationAction[]) => void }) {
  return (
    <div className="stack-list">
      <PanelTitle title="Accion" subtitle="La accion queda preparada y auditada por el motor." />
      {actions.map((item, index) => (
        <div className="form-grid" key={index}>
          <SelectField label="Tipo de accion" value={item.tipoAccion} options={actionOptions} onChange={(value) => updateAt(actions, index, { ...item, tipoAccion: value }, onChange)} />
          <label className="wide-field"><span>Detalle de accion</span><textarea value={item.parametrosJson ?? ''} onChange={(event) => updateAt(actions, index, { ...item, parametrosJson: event.target.value }, onChange)} placeholder="Opcional" /></label>
          <button className="icon-button secondary" onClick={() => onChange(actions.filter((_, i) => i !== index))}><Trash2 size={16} />Quitar</button>
        </div>
      ))}
      <button className="icon-button secondary" onClick={() => onChange([...actions, { ...emptyAction }])}><Plus size={16} />Agregar accion</button>
    </div>
  )
}

function validateRule(form: AutomationRequest) {
  if (!form.nombre.trim()) return 'El nombre de la regla es requerido.'
  if (!form.tipoEvento.trim()) return 'Selecciona cuando se ejecuta la regla.'
  if (form.condiciones.length === 0) return 'Agrega al menos una condicion.'
  if (form.acciones.length !== 1) return 'Deja una sola accion para simplificar esta regla.'
  for (const condition of form.condiciones) {
    if (!condition.campo.trim()) return 'Todas las condiciones necesitan un campo.'
    if (!condition.operador.trim()) return 'Todas las condiciones necesitan un operador.'
    if (!['existe', 'vacio'].includes(condition.operador) && !condition.valor?.trim()) return 'Las condiciones seleccionadas necesitan valor.'
  }
  const action = form.acciones[0]
  if (!action.tipoAccion.trim()) return 'Selecciona el tipo de accion.'
  if (action.parametrosJson?.trim()) {
    try {
      JSON.parse(action.parametrosJson)
    } catch {
      return 'El detalle de la accion no tiene un formato valido.'
    }
  }
  return null
}

function ruleSummary(form: AutomationRequest) {
  const status = form.activo ? 'activa' : 'pausada'
  const conditionText = form.condiciones.map((item) => `${statusLabel(item.campo)} ${statusLabel(item.operador)}${item.valor ? ` ${item.valor}` : ''}`).join(' y ')
  const action = form.acciones[0]?.tipoAccion || 'sin accion'
  return `Regla ${status}: cuando ocurra ${eventLabel(form.tipoEvento)}, si ${conditionText || 'no hay condiciones'}, preparar ${statusLabel(action)}.`
}

function updateAt<T>(items: T[], index: number, next: T, onChange: (items: T[]) => void) {
  onChange(items.map((item, i) => i === index ? next : item))
}

function newRule(): AutomationRequest {
  return {
    nombre: '',
    activo: true,
    tipoEvento: 'pago_vencido',
    condiciones: [{ ...emptyCondition }],
    acciones: [{ ...emptyAction }],
  }
}

function eventLabel(value: string) {
  const labels: Record<string, string> = {
    pago_vencido: 'Pago vencido',
    reclamo_nuevo: 'Reclamo nuevo',
    cliente_creado: 'Cliente creado',
    pago_registrado: 'Pago registrado',
    poliza_por_vencer: 'Poliza por vencer',
  }
  return labels[value] ?? value
}
