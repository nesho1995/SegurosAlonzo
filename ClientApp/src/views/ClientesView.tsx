import { Fragment, useEffect, useMemo, useState } from 'react'
import { Download, FileText, Pencil, Plus, Power, Save, X } from 'lucide-react'
import { cambiarEstadoCliente, cambiarEstadoPoliza, createCliente, createPoliza, getClienteDetalle, getClientes, getPolizaCuotas, marcarClienteRevisado, updateCliente, updatePoliza, updatePolizaCuotaMonto } from '../api/carteraApi'
import { actualizarFechaCuota } from '../api/pagosApi'
import { getCatalogoByTipo } from '../api/catalogosApi'
import { StatusPill } from '../components/Badge'
import { LoadingCard } from '../components/LoadingState'
import { ErrorCard } from '../components/ErrorAlert'
import { Field, Info, PanelTitle, PolicyFields } from '../components/FormControls'
import { DocumentosPanel } from '../components/DocumentosPanel'
import { PageHeader } from '../components/Topbar'
import type { Client, ClientDetailResponse, ClientListResponse, ClientSummary, Policy, PolicyInstallment } from '../types/cartera'
import { emptyClient, emptyPolicy } from '../types/cartera'
import { compactMeta, dateFmt, moneySafe } from '../utils/formatters'
import { stateTone, statusLabel } from '../utils/labels'
import { useAuth } from '../hooks/useAuth'

function toDateInputSafe(value?: string) {
  if (!value) return ''
  return value.slice(0, 10)
}

function formatJsonList(value?: string) {
  if (!value) return ''
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed.join(', ') : String(parsed)
  } catch {
    return value
  }
}

function extraPhones(phones: string[], client: Client) {
  return phones.filter((phone) => phone !== client.telefono && phone !== client.telefonoSecundario && phone !== client.contacto)
}

export function ClientsView() {
  const [data, setData] = useState<ClientListResponse | null>(null)
  const [items, setItems] = useState<ClientSummary[]>([])
  const [detail, setDetail] = useState<ClientDetailResponse | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [estado, setEstado] = useState('TODOS')
  const [buscar, setBuscar] = useState('')
  const [financiera, setFinanciera] = useState('')
  const [aseguradora, setAseguradora] = useState('')
  const [ramo, setRamo] = useState('')
  const [estadoPago, setEstadoPago] = useState('')
  const [ciudad, setCiudad] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [creatingClient, setCreatingClient] = useState(false)
  const [newClient, setNewClient] = useState<Client>(emptyClient)
  const [savingClient, setSavingClient] = useState(false)
  const [hideClientList, setHideClientList] = useState(false)
  const { hasPermission } = useAuth()
  const canCreateClient = hasPermission('clientes.crear')
  const hasMorePages = useMemo(() => (data?.pagina ?? 1) < (data?.totalPaginas ?? 1), [data?.pagina, data?.totalPaginas])

  async function loadList(targetPage = 1, append = false) {
    if (append) setLoadingMore(true)
    else setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ estado, pageSize: '40', pagina: String(targetPage) })
      if (buscar.trim()) query.set('buscar', buscar.trim())
      if (financiera.trim()) query.set('financiera', financiera.trim())
      if (aseguradora.trim()) query.set('aseguradora', aseguradora.trim())
      if (ramo.trim()) query.set('ramo', ramo.trim())
      if (estadoPago.trim()) query.set('estadoPago', estadoPago.trim())
      if (ciudad.trim()) query.set('ciudad', ciudad.trim())
      const json = await getClientes(query)
      const mergedItems = append ? [...items, ...json.items] : json.items
      setData({ ...json, items: mergedItems })
      setItems(mergedItems)
      if (!selectedId && mergedItems.length > 0) {
        setSelectedId(mergedItems[0].id)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      if (append) setLoadingMore(false)
      else setLoading(false)
    }
  }

  async function loadDetail(id: number) {
    setError(null)
    try {
      setDetail(await getClienteDetalle(id))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    }
  }

  async function createClient() {
    if (!newClient.nombre.trim()) {
      setError('El nombre del cliente es requerido.')
      return
    }

    setSavingClient(true)
    setError(null)
    try {
      const created = await createCliente(newClient)
      setCreatingClient(false)
      setNewClient(emptyClient)
      await loadList()
      if (created?.id) {
        setSelectedId(created.id)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSavingClient(false)
    }
  }

  useEffect(() => {
    let alive = true
    const query = new URLSearchParams({ estado, pageSize: '40', pagina: '1' })
    if (buscar.trim()) query.set('buscar', buscar.trim())
    if (financiera.trim()) query.set('financiera', financiera.trim())
    if (aseguradora.trim()) query.set('aseguradora', aseguradora.trim())
    if (ramo.trim()) query.set('ramo', ramo.trim())
    if (estadoPago.trim()) query.set('estadoPago', estadoPago.trim())
    if (ciudad.trim()) query.set('ciudad', ciudad.trim())

    getClientes(query)
      .then((json) => {
        if (!alive) return
        setData(json)
        setItems(json.items)
        setSelectedId((current) => current || json.items[0]?.id || null)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [estado, buscar, financiera, aseguradora, ramo, estadoPago, ciudad])

  useEffect(() => {
    if (!selectedId) return
    let alive = true

    getClienteDetalle(selectedId)
      .then((json) => {
        if (!alive) return
        setDetail(json)
        setError(null)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'Error inesperado.')
      })

    return () => {
      alive = false
    }
  }, [selectedId])

  return (
    <>
      <PageHeader
        eyebrow="CRM"
        title="Clientes y polizas"
        description="Consulta, edita datos de contacto y activa o inactiva polizas desde una vista clara de cartera."
        onRefresh={() => { void loadList() }}
        action={canCreateClient ? (
          <button className="primary-button" onClick={() => setCreatingClient((value) => !value)}>
            {creatingClient ? <X size={18} /> : <Plus size={18} />}
            {creatingClient ? 'Cancelar' : 'Nuevo cliente'}
          </button>
        ) : undefined}
      />
      <form className="toolbar clients-toolbar" onSubmit={(event) => { event.preventDefault(); void loadList() }}>
        <label className="search-box">
          <input value={buscar} onChange={(event) => setBuscar(event.target.value)} placeholder="Buscar cliente, poliza, telefono o email" />
        </label>
        <select value={estado} onChange={(event) => setEstado(event.target.value)}>
          {['TODOS', 'OK', 'PENDIENTE_REVISION', 'ERROR_IMPORTACION', 'ACTIVO', 'INACTIVO'].map((item) => (
            <option key={item} value={item}>{item.replaceAll('_', ' ')}</option>
          ))}
        </select>
        <input className="clients-filter-input" value={financiera} onChange={(event) => setFinanciera(event.target.value)} placeholder="Financiera/contratante" />
        <input className="clients-filter-input" value={aseguradora} onChange={(event) => setAseguradora(event.target.value)} placeholder="Aseguradora" />
        <input className="clients-filter-input" value={ramo} onChange={(event) => setRamo(event.target.value)} placeholder="Ramo" />
        <input className="clients-filter-input" value={ciudad} onChange={(event) => setCiudad(event.target.value)} placeholder="Ciudad" />
        <select value={estadoPago} onChange={(event) => setEstadoPago(event.target.value)}>
          <option value="">Estado pago (todos)</option>
          {['SIN_VALIDAR', 'PENDIENTE', 'EN_CUOTAS', 'PARCIAL', 'MORA', 'PAGADO'].map((item) => (
            <option key={item} value={item}>{item.replaceAll('_', ' ')}</option>
          ))}
        </select>
        <button className="primary-button" type="submit">Filtrar</button>
        <a
          className="icon-button secondary"
          href={`/api/cartera/excel/clientes?${new URLSearchParams(Object.fromEntries(Object.entries({ estado, buscar, financiera, aseguradora, ramo, estadoPago, ciudad }).filter(([, v]) => v && v !== 'TODOS')))}`}
          title="Exportar a Excel"
          download
        >
          <Download size={16} />
          Excel
        </a>
      </form>

      {loading && <LoadingCard text="Cargando clientes..." />}
      {error && <ErrorCard text={error} />}
      {creatingClient && (
        <article className="panel form-panel">
          <PanelTitle title="Nuevo cliente" subtitle="Registra el contacto principal antes de agregar sus polizas." />
          <div className="form-grid">
            <Field label="Nombre" value={newClient.nombre} onChange={(value) => setNewClient({ ...newClient, nombre: value })} />
            <Field label="Telefono principal" value={newClient.telefono || ''} onChange={(value) => setNewClient({ ...newClient, telefono: value })} />
            <Field label="Telefono secundario" value={newClient.telefonoSecundario || newClient.contacto || ''} onChange={(value) => setNewClient({ ...newClient, telefonoSecundario: value, contacto: value })} />
            <Field label="Telefonos extra" value={formatJsonList(newClient.telefonosExtraJson)} onChange={(value) => setNewClient({ ...newClient, telefonosExtraJson: value })} />
            <Field label="Correo principal" value={newClient.email || ''} onChange={(value) => setNewClient({ ...newClient, email: value })} />
            <Field label="Correos extra" value={formatJsonList(newClient.correosExtraJson)} onChange={(value) => setNewClient({ ...newClient, correosExtraJson: value })} />
            <Field label="Fecha nacimiento" type="date" value={toDateInputSafe(newClient.fechaNacimiento)} onChange={(value) => setNewClient({ ...newClient, fechaNacimiento: value || undefined })} />
            <Field label="Ciudad" value={newClient.ciudad || ''} onChange={(value) => setNewClient({ ...newClient, ciudad: value })} />
            <label className="wide-field">
              <span>Observaciones</span>
              <textarea value={newClient.observaciones || ''} onChange={(event) => setNewClient({ ...newClient, observaciones: event.target.value })} />
            </label>
            <div className="form-actions">
              <button className="primary-button" onClick={createClient} disabled={savingClient}>
                <Save size={18} />
                Guardar cliente
              </button>
            </div>
          </div>
        </article>
      )}

      {data && (
        <section className={`client-layout ${hideClientList ? 'client-layout-full' : ''}`}>
          {!hideClientList && <article className="panel">
            <PanelTitle title={`${data.total} clientes`} subtitle="Selecciona un cliente para ver y editar sus polizas." />
            <div
              className="client-list"
              onScroll={(event) => {
                if (loadingMore || !data || !hasMorePages) return
                const target = event.currentTarget
                const nearBottom = target.scrollTop + target.clientHeight >= target.scrollHeight - 80
                if (nearBottom) void loadList(data.pagina + 1, true)
              }}
            >
              {items.length === 0 ? (
                <div className="empty">No hay clientes para mostrar.</div>
              ) : (
                items.map((item) => (
                  <button className={`client-row ${selectedId === item.id ? 'active' : ''}`} key={item.id} onClick={() => setSelectedId(item.id)}>
                    <span>
                      <strong>{item.nombre}</strong>
                      <small>{compactMeta([item.telefono, item.email, item.ciudad])}</small>
                    </span>
                    <div className="client-row-badges">
                      {item.estadoNegocio && item.estadoNegocio !== 'ACTIVO' && (
                        <StatusPill
                          text={item.estadoNegocio === 'EN_RIESGO' ? 'En riesgo' : item.estadoNegocio === 'PROSPECTO' ? 'Prospecto' : 'Inactivo'}
                          tone={item.estadoNegocio === 'EN_RIESGO' ? 'warning' : item.estadoNegocio === 'INACTIVO' ? 'danger' : 'info'}
                        />
                      )}
                      <StatusPill text={item.estadoRevision === 'ERROR_IMPORTACION' ? 'Error importacion' : item.requiereRevisionManual || item.estadoRevision === 'PENDIENTE_REVISION' ? 'Pendiente revision' : 'OK'} tone={item.estadoRevision === 'ERROR_IMPORTACION' ? 'danger' : item.requiereRevisionManual || item.estadoRevision === 'PENDIENTE_REVISION' ? 'warning' : 'success'} />
                    </div>
                  </button>
                ))
              )}
            </div>
            {hasMorePages && (
              <div className="client-list-actions">
                <button className="icon-button secondary" onClick={() => void loadList(data.pagina + 1, true)} disabled={loadingMore}>
                  {loadingMore ? 'Cargando...' : 'Cargar mas clientes'}
                </button>
              </div>
            )}
          </article>}

          <article className="panel client-detail">
            <div className="client-detail-toolbar">
              <button className="icon-button secondary" type="button" onClick={() => setHideClientList((value) => !value)}>
                {hideClientList ? 'Mostrar clientes' : 'Ocultar clientes'}
              </button>
            </div>
            {!detail ? (
              <div className="empty">Selecciona un cliente para ver el detalle.</div>
            ) : (
              <ClientDetail
                key={detail.cliente.id}
                detail={detail}
                onSaved={() => loadDetail(detail.cliente.id)}
                onPolicySaved={() => {
                  void loadList()
                  void loadDetail(detail.cliente.id)
                }}
              />
            )}
          </article>
        </section>
      )}
    </>
  )
}

export function ClientDetail({ detail, onSaved, onPolicySaved }: { detail: ClientDetailResponse; onSaved: () => void; onPolicySaved: () => void }) {
  const { hasPermission } = useAuth()
  const canEditClient = hasPermission('clientes.editar')
  const canCreatePolicy = hasPermission('polizas.crear')
  const canEditPolicy = hasPermission('polizas.editar')
  const [editingClient, setEditingClient] = useState(false)
  const [clientForm, setClientForm] = useState<Client>(detail.cliente)
  const [editingPolicyId, setEditingPolicyId] = useState<number | null>(null)
  const [policyForm, setPolicyForm] = useState<Policy | null>(null)
  const [creatingPolicy, setCreatingPolicy] = useState(false)
  const [newPolicy, setNewPolicy] = useState<Policy>(() => emptyPolicy(detail.cliente.id))
  const [expandedPolicies, setExpandedPolicies] = useState<number[]>([])
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [catalogs, setCatalogs] = useState<{ ramos: string[]; ramosNormalizados: string[]; companias: string[]; formasPago: string[]; medios: string[]; estadoPago: string[]; tipoProceso: string[]; estadoRevision: string[]; estadoPolizaReal: string[]; emisionRenovacion: string[] }>({
    ramos: [], ramosNormalizados: [], companias: [], formasPago: [], medios: [], estadoPago: [], tipoProceso: [], estadoRevision: [], estadoPolizaReal: [], emisionRenovacion: []
  })

  useEffect(() => {
    let alive = true
    Promise.all([
      getCatalogoByTipo('RAMOS', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('COMPANIAS', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('FORMAS_PAGO', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('MEDIOS', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('ESTADOS_PAGO', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('TIPO_PROCESO', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('ESTADO_REVISION', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('ESTADOS_POLIZA', false).catch(() => ({ items: [] })),
      getCatalogoByTipo('EMISION_RENOVACION', false).catch(() => ({ items: [] })),
    ]).then(([ramos, companias, formasPago, medios, estadoPago, tipoProceso, estadoRevision, estadosPoliza, emisionRenovacion]) => {
      if (!alive) return
      setCatalogs({
        ramos: ramos.items.map(x => x.nombre),
        ramosNormalizados: ramos.items.map(x => x.nombre),
        companias: companias.items.map(x => x.nombre),
        formasPago: formasPago.items.map(x => x.nombre),
        medios: medios.items.map(x => x.nombre),
        estadoPago: estadoPago.items.map(x => x.nombre),
        tipoProceso: tipoProceso.items.map(x => x.nombre),
        estadoRevision: estadoRevision.items.map(x => x.nombre),
        estadoPolizaReal: estadosPoliza.items.map(x => x.nombre),
        emisionRenovacion: emisionRenovacion.items.map(x => x.nombre),
      })
    }).catch(() => {})
    return () => { alive = false }
  }, [])

  async function saveClient() {
    setSaving(true)
    setError(null)
    try {
      await updateCliente(clientForm)
      setMessage('Cliente actualizado.')
      setEditingClient(false)
      onSaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  async function savePolicy() {
    if (!policyForm) return

    setSaving(true)
    setError(null)
    try {
      await updatePoliza(policyForm)
      setMessage('Poliza actualizada.')
      setEditingPolicyId(null)
      setPolicyForm(null)
      onPolicySaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  async function createPolicy() {
    setSaving(true)
    setError(null)
    try {
      await createPoliza(detail.cliente.id, newPolicy)
      setMessage('Poliza creada.')
      setCreatingPolicy(false)
      setNewPolicy(emptyPolicy(detail.cliente.id))
      onPolicySaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  async function togglePolicy(policy: Policy) {
    const next = policy.activo ? 'inactivar' : 'activar'
    if (!window.confirm(`Confirmas ${next} esta poliza?`)) return
    setSaving(true)
    setError(null)
    try {
      await cambiarEstadoPoliza(policy)
      setMessage(policy.activo ? 'Poliza inactivada.' : 'Poliza activada.')
      onPolicySaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  async function toggleClient() {
    const next = detail.cliente.activo ? 'desactivar' : 'reactivar'
    if (!window.confirm(`Confirmas ${next} este cliente? No se borraran polizas, pagos, documentos ni auditoria.`)) return
    setSaving(true)
    setError(null)
    try {
      await cambiarEstadoCliente(detail.cliente)
      setMessage(detail.cliente.activo ? 'Cliente desactivado.' : 'Cliente reactivado.')
      onSaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  async function markReviewed() {
    setSaving(true)
    setError(null)
    try {
      await marcarClienteRevisado(detail.cliente.id)
      setMessage('Cliente marcado como revisado.')
      onSaved()
      onPolicySaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error inesperado.')
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => {
    if (!message) return
    const timeout = window.setTimeout(() => setMessage(null), 3500)
    return () => window.clearTimeout(timeout)
  }, [message])

  return (
    <>
      <div className="detail-head">
        <div>
          <span className="eyebrow">Cliente seleccionado</span>
          <h2>{detail.cliente.nombre}</h2>
          <p>{compactMeta([detail.cliente.telefono, detail.cliente.email, detail.cliente.ciudad])}</p>
        </div>
        {canEditClient && <div className="form-actions">
          <button className="icon-button secondary" onClick={() => setEditingClient((value) => !value)} disabled={saving}>
            {editingClient ? <X size={18} /> : <Pencil size={18} />}
            <span>{editingClient ? 'Cancelar' : 'Editar'}</span>
          </button>
          <button className={`icon-button ${detail.cliente.activo ? 'danger-button' : 'secondary'}`} onClick={() => void toggleClient()} disabled={saving}>
            <Power size={18} />
            <span>{detail.cliente.activo ? 'Desactivar' : 'Reactivar'}</span>
          </button>
          <button className="icon-button secondary" onClick={() => void markReviewed()} disabled={saving}>
            <Save size={18} />
            <span>Marcar revisado</span>
          </button>
        </div>}
      </div>

      {message && <div className="inline-alert success">{message}</div>}
      {error && <div className="inline-alert danger">{error}</div>}

      {editingClient ? (
        <div className="form-grid">
          <Field label="Nombre" value={clientForm.nombre} onChange={(value) => setClientForm({ ...clientForm, nombre: value })} />
          <Field label="Telefono principal" value={clientForm.telefono || ''} onChange={(value) => setClientForm({ ...clientForm, telefono: value })} />
          <Field label="Telefono secundario" value={clientForm.telefonoSecundario || clientForm.contacto || ''} onChange={(value) => setClientForm({ ...clientForm, telefonoSecundario: value, contacto: value })} />
          <Field label="Telefonos extra" value={formatJsonList(clientForm.telefonosExtraJson)} onChange={(value) => setClientForm({ ...clientForm, telefonosExtraJson: value })} />
          <Field label="Correo principal" value={clientForm.email || ''} onChange={(value) => setClientForm({ ...clientForm, email: value })} />
          <Field label="Correos extra" value={formatJsonList(clientForm.correosExtraJson)} onChange={(value) => setClientForm({ ...clientForm, correosExtraJson: value })} />
          <Field label="Fecha nacimiento" type="date" value={toDateInputSafe(clientForm.fechaNacimiento)} onChange={(value) => setClientForm({ ...clientForm, fechaNacimiento: value || undefined })} />
          <Field label="Ciudad" value={clientForm.ciudad || ''} onChange={(value) => setClientForm({ ...clientForm, ciudad: value })} />
          <label className="check-field">
            <input type="checkbox" checked={clientForm.activo} onChange={(event) => setClientForm({ ...clientForm, activo: event.target.checked })} />
            Cliente activo
          </label>
          <label className="check-field">
            <input type="checkbox" checked={Boolean(clientForm.datosRevisados)} onChange={(event) => setClientForm({ ...clientForm, datosRevisados: event.target.checked })} />
            Datos revisados
          </label>
          <label className="wide-field">
            <span>Observaciones</span>
            <textarea value={clientForm.observaciones || ''} onChange={(event) => setClientForm({ ...clientForm, observaciones: event.target.value })} />
          </label>
          <div className="form-actions">
            <button className="primary-button" onClick={saveClient} disabled={saving}>
              <Save size={18} />
              Guardar cliente
            </button>
          </div>
        </div>
      ) : (
        <>
          <section className="info-grid">
            <Info label="Telefono principal" value={detail.cliente.telefono || 'Sin telefono'} />
            <Info label="Telefono secundario" value={detail.cliente.telefonoSecundario || detail.cliente.contacto || 'Sin secundario'} />
            <Info label="Telefonos extra" value={formatJsonList(detail.cliente.telefonosExtraJson) || extraPhones(detail.telefonos, detail.cliente).join(', ') || 'Sin extras'} />
            <Info label="Correo principal" value={detail.cliente.email || 'Sin email'} />
            <Info label="Correos extra" value={formatJsonList(detail.cliente.correosExtraJson) || 'Sin extras'} />
            <Info label="Fecha nacimiento" value={detail.cliente.fechaNacimiento ? dateFmt.format(new Date(detail.cliente.fechaNacimiento)) : 'Sin fecha'} />
            <Info label="Ciudad" value={detail.cliente.ciudad || 'Sin ciudad'} />
            <Info label="Revision de datos" value={detail.cliente.datosRevisados ? 'Revisados' : 'Pendiente de revision'} />
            <Info label="Estado revision" value={detail.cliente.estadoRevision || 'OK'} />
            <Info label="Motivo revision" value={detail.cliente.motivoRevision || 'Sin motivo'} />
          </section>
          {detail.cliente.notasCalidadJson && <div className="inline-alert warning">Notas de calidad: {formatJsonList(detail.cliente.notasCalidadJson)}</div>}
          <DocumentosPanel entidadTipo="CLIENTE" entidadId={detail.cliente.id} />
        </>
      )}

      <div className="section-title-row">
        <PanelTitle title={`${detail.polizas.length} polizas`} subtitle="Puedes editar datos clave o activar/inactivar una poliza." />
        {canCreatePolicy && <button className="primary-button" onClick={() => setCreatingPolicy((value) => !value)} disabled={saving}>
          {creatingPolicy ? <X size={18} /> : <Plus size={18} />}
          {creatingPolicy ? 'Cancelar' : 'Nueva poliza'}
        </button>}
      </div>
      {creatingPolicy && (
        <div className="form-grid policy-form inline-create">
          <PolicyFields policy={newPolicy} onChange={setNewPolicy} catalogs={catalogs} />
          <div className="form-actions wide-field">
            <button className="primary-button" onClick={createPolicy} disabled={saving}>
              <Save size={18} />
              Guardar poliza
            </button>
          </div>
        </div>
      )}
      <div className="policy-list">
        {detail.polizas.length === 0 ? (
          <div className="empty">Este cliente no tiene polizas registradas.</div>
        ) : (
          detail.polizas.map((policy) => {
            const editing = editingPolicyId === policy.id && policyForm
            const expanded = expandedPolicies.includes(policy.id)

            // Semáforo: estado calculado desde fecha real
            const hoy = new Date(); hoy.setHours(0,0,0,0)
            const hasta = policy.hasta ? new Date(policy.hasta) : null
            const diasRestantes = hasta ? Math.ceil((hasta.getTime() - hoy.getTime()) / 86400000) : null
            const semaforoTone =
              !policy.activo                        ? 'slate'
              : diasRestantes !== null && diasRestantes < 0  ? 'danger'
              : diasRestantes !== null && diasRestantes <= 7  ? 'danger'
              : diasRestantes !== null && diasRestantes <= 30 ? 'warning'
              : policy.estadoPago === 'MORA'         ? 'warning'
              :                                        'success'
            const semaforoTexto =
              !policy.activo                        ? 'Inactiva'
              : diasRestantes !== null && diasRestantes < 0  ? `Vencida (${Math.abs(diasRestantes)}d)`
              : diasRestantes !== null && diasRestantes <= 7  ? `Vence en ${diasRestantes}d`
              : diasRestantes !== null && diasRestantes <= 30 ? `Vence en ${diasRestantes}d`
              : policy.estadoPago === 'MORA'         ? 'En mora'
              :                                        'Vigente'

            return (
              <div className={`policy-card semaforo-${semaforoTone}`} key={policy.id}>
                <div className="policy-head">
                  <FileText size={20} />
                  <div>
                    <strong>{policy.numeroPoliza || 'Sin poliza'}</strong>
                    <span>{compactMeta([policy.aseguradora, policy.ramo, policy.vehiculo, policy.clienteContratanteNombre || 'Sin financiera'])}</span>
                  </div>
                  <StatusPill text={semaforoTexto} tone={semaforoTone} />
                  <a
                    className="icon-button secondary"
                    href={`/api/cartera/${policy.id}/pdf`}
                    target="_blank"
                    rel="noreferrer"
                    title="Descargar PDF de esta póliza"
                    style={{ textDecoration: 'none' }}
                  >
                    <Download size={15} />
                  </a>
                </div>
                <div className="policy-actions policy-actions-compact">
                  <button
                    className="icon-button secondary"
                    onClick={() => setExpandedPolicies((prev) => prev.includes(policy.id) ? prev.filter((item) => item !== policy.id) : [...prev, policy.id])}
                    type="button"
                  >
                    {expanded ? 'Contraer detalle' : 'Expandir detalle'}
                  </button>
                </div>

                {editing ? (
                  <div className="form-grid policy-form">
                    <PolicyFields policy={policyForm} onChange={setPolicyForm} catalogs={catalogs} />
                    <div className="form-actions wide-field">
                      <button className="primary-button" onClick={savePolicy} disabled={saving}>
                        <Save size={18} />
                        Guardar poliza
                      </button>
                      <button className="icon-button secondary" onClick={() => setEditingPolicyId(null)} disabled={saving}>
                        <X size={18} />
                        Cancelar
                      </button>
                    </div>
                  </div>
                ) : expanded ? (
                  <>
                    {/* ── BLOQUE 1: RESUMEN ── */}
                    <div className="policy-section">
                      <span className="policy-section-title">Resumen</span>
                      <div className="info-grid compact">
                        <Info label="Vigencia" value={policy.vigencia ? dateFmt.format(new Date(policy.vigencia)) : 'Sin fecha'} />
                        <Info label="Hasta" value={policy.hasta ? dateFmt.format(new Date(policy.hasta)) : 'Sin fecha'} />
                        <Info label="Prima" value={policy.primaTotal ? moneySafe(policy.primaTotal) : 'Sin prima'} />
                        <Info label="Mes inicio" value={policy.mesInicioPoliza || 'Sin dato'} />
                        <Info label="Pago" value={statusLabel(policy.estadoPago)} />
                        <Info label="Financiera" value={policy.clienteContratanteNombre || (policy.clienteContratanteId ? `Cliente #${policy.clienteContratanteId}` : 'Sin contratante')} />
                        <Info label="Placa" value={policy.placa || 'Sin placa'} />
                        <Info label="VIN/Serie" value={policy.vinSerie || 'Sin VIN'} />
                        <Info label="Agente" value={policy.agenteAsignado || 'Sin agente'} />
                      </div>
                      {canEditPolicy && (
                        <div className="policy-actions">
                          <button className="icon-button secondary" onClick={() => { setEditingPolicyId(policy.id); setPolicyForm(policy) }} disabled={saving}>
                            <Pencil size={18} /> Editar
                          </button>
                          <button className={`icon-button ${policy.activo ? 'danger-button' : 'secondary'}`} onClick={() => void togglePolicy(policy)} disabled={saving}>
                            <Power size={18} /> {policy.activo ? 'Inactivar' : 'Activar'}
                          </button>
                        </div>
                      )}
                    </div>

                    {/* ── BLOQUE 2: CUOTAS ── */}
                    <div className="policy-section">
                      <span className="policy-section-title">Cuotas</span>
                      <PolicyInstallmentsTable policyId={policy.id} totalCuotas={policy.cuotas} />
                    </div>

                    {/* ── BLOQUE 3: DOCUMENTOS DE PÓLIZA ── */}
                    <div className="policy-section">
                      <span className="policy-section-title">Documentos de póliza</span>
                      <DocumentosPanel entidadTipo="POLIZA" entidadId={policy.id} />
                    </div>
                  </>
                ) : (
                  <div className="inline-alert info">Vista compacta activa. Expande para editar, cuotas y documentos.</div>
                )}
              </div>
            )
          })
        )}
      </div>
    </>
  )
}

function PolicyInstallmentsTable({ policyId, totalCuotas }: { policyId: number; totalCuotas?: number }) {
  const [items, setItems] = useState<PolicyInstallment[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [savingCuotaId, setSavingCuotaId] = useState<number | null>(null)
  const [amountDrafts, setAmountDrafts] = useState<Record<number, string>>({})
  const [fechaDrafts, setFechaDrafts] = useState<Record<number, string>>({})
  const [editingCuotaId, setEditingCuotaId] = useState<number | null>(null)

  function applyDrafts(items: PolicyInstallment[]) {
    setAmountDrafts((current) => {
      const next = { ...current }
      for (const item of items) {
        if (item.id && next[item.id] === undefined) next[item.id] = String(item.monto ?? 0)
      }
      return next
    })
    setFechaDrafts((current) => {
      const next = { ...current }
      for (const item of items) {
        if (item.id && next[item.id] === undefined) next[item.id] = item.fechaVencimiento?.slice(0, 10) ?? ''
      }
      return next
    })
  }

  async function loadInstallments() {
    const data = await getPolizaCuotas(policyId)
    setItems(data.items)
    applyDrafts(data.items)
  }

  useEffect(() => {
    let alive = true
    getPolizaCuotas(policyId)
      .then((data) => {
        if (!alive) return
        setItems(data.items)
        applyDrafts(data.items)
      })
      .catch((err) => {
        if (alive) setError(err instanceof Error ? err.message : 'No se pudieron cargar las cuotas.')
      })
    return () => { alive = false }
  }, [policyId])

  useEffect(() => {
    if (!message) return
    const timeout = window.setTimeout(() => setMessage(null), 3000)
    return () => window.clearTimeout(timeout)
  }, [message])

  const visibleCuotas = totalCuotas && totalCuotas > 0 ? Math.min(totalCuotas, 12) : 12
  const slots = Array.from({ length: visibleCuotas }, (_, index) => items.find((item) => item.numeroCuota === index + 1) ?? {
    polizaId: policyId,
    numeroCuota: index + 1,
    estado: 'PENDIENTE',
  } as PolicyInstallment)

  return (
    <div className="cuotas-table-wrap">
      {error && <div className="inline-alert danger">{error}</div>}
      {message && <div className="inline-alert success">{message}</div>}
      <table className="cuotas-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Vencimiento</th>
            <th>Monto</th>
            <th>Estado</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {slots.map((cuota) => (
            <Fragment key={cuota.numeroCuota}>
              <tr className={cuota.id && editingCuotaId === cuota.id ? 'cuota-row-active' : ''}>
                <td className="cuota-num">{cuota.numeroCuota}</td>
                <td>{cuota.fechaVencimiento ? dateFmt.format(new Date(cuota.fechaVencimiento)) : <span className="text-muted">—</span>}</td>
                <td>{cuota.monto !== undefined ? moneySafe(cuota.monto) : <span className="text-muted">—</span>}</td>
                <td><StatusPill text={statusLabel(cuota.estado)} tone={stateTone(cuota.estado)} /></td>
                <td className="cuota-actions">
                  {cuota.id ? (
                    <button
                      className={`icon-button secondary compact${editingCuotaId === cuota.id ? ' active' : ''}`}
                      type="button"
                      title="Editar cuota"
                      onClick={() => setEditingCuotaId(editingCuotaId === cuota.id ? null : cuota.id!)}
                    >
                      <Pencil size={13} />
                    </button>
                  ) : <span className="text-muted">—</span>}
                </td>
              </tr>

              {cuota.id && editingCuotaId === cuota.id && (
                <tr className="cuota-row-detail">
                  <td colSpan={5}>
                    <div className="cuota-edit-inline">
                      <label className="field compact-field">
                        <span>Monto cuota {cuota.numeroCuota}</span>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={amountDrafts[cuota.id] ?? String(cuota.monto ?? 0)}
                          onChange={(event) => {
                            const value = event.target.value
                            setAmountDrafts((current) => ({ ...current, [cuota.id!]: value }))
                          }}
                        />
                      </label>
                      <label className="field compact-field">
                        <span>Fecha vencimiento</span>
                        <input
                          type="date"
                          value={fechaDrafts[cuota.id] ?? cuota.fechaVencimiento?.slice(0, 10) ?? ''}
                          disabled={cuota.estado === 'PAGADA'}
                          title={cuota.estado === 'PAGADA' ? 'Las cuotas pagadas no permiten cambiar vencimiento.' : undefined}
                          onChange={(event) => {
                            const value = event.target.value
                            setFechaDrafts((current) => ({ ...current, [cuota.id!]: value }))
                          }}
                        />
                      </label>
                      <div className="cuota-edit-actions">
                        <button
                          className="primary-button"
                          type="button"
                          disabled={savingCuotaId === cuota.id}
                          onClick={async () => {
                            const raw = amountDrafts[cuota.id!] ?? String(cuota.monto ?? 0)
                            const parsed = Number(raw)
                            if (!Number.isFinite(parsed) || parsed < 0) {
                              setError('El monto debe ser un numero valido mayor o igual a cero.')
                              return
                            }
                            setSavingCuotaId(cuota.id!)
                            setError(null)
                            try {
                              await updatePolizaCuotaMonto(cuota.id!, parsed)
                              const fecha = fechaDrafts[cuota.id!]
                              if (fecha && cuota.estado !== 'PAGADA') {
                                await actualizarFechaCuota(cuota.id!, fecha)
                              }
                              await loadInstallments()
                              setMessage(`Cuota ${cuota.numeroCuota} actualizada.`)
                              setEditingCuotaId(null)
                            } catch (err) {
                              setError(err instanceof Error ? err.message : 'No se pudo guardar la cuota.')
                            } finally {
                              setSavingCuotaId(null)
                            }
                          }}
                        >
                          {savingCuotaId === cuota.id ? 'Guardando...' : 'Guardar'}
                        </button>
                        <button className="icon-button secondary" type="button" onClick={() => setEditingCuotaId(null)}>
                          <X size={14} /> Cancelar
                        </button>
                      </div>
                    </div>
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  )
}
