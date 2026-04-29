import { Search } from 'lucide-react'
import type { Policy } from '../types/cartera'
import { statusLabel } from '../utils/labels'
import { toDateInput } from '../utils/formatters'
export function PanelTitle({ title, subtitle }: { title: string; subtitle: string }) { return (<div className="panel-header"><h2>{title}</h2><p>{subtitle}</p></div>) }
export function Toolbar({ buscar, estado, estados, onBuscar, onEstado, onSubmit }: { buscar: string; estado: string; estados: string[]; onBuscar: (value: string) => void; onEstado: (value: string) => void; onSubmit: () => void }) { return (<form className="toolbar" onSubmit={(event) => { event.preventDefault(); onSubmit() }}><label className="search-box"><Search size={18} /><input value={buscar} onChange={(event) => onBuscar(event.target.value)} placeholder="Buscar cliente, poliza o telefono" /></label><select value={estado} onChange={(event) => onEstado(event.target.value)}>{estados.map((item) => (<option value={item} key={item}>{statusLabel(item)}</option>))}</select><button className="primary-button" type="submit">Filtrar</button></form>) }
export function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) { return (<label className="field"><span>{label}</span><input type={type} value={value} onChange={(event) => onChange(event.target.value)} /></label>) }
export function SelectField({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (value: string) => void }) { return (<label className="field"><span>{label}</span><select value={value} onChange={(event) => onChange(event.target.value)}>{options.map((item) => (<option value={item} key={item}>{statusLabel(item)}</option>))}</select></label>) }
export function PolicyFields({ policy, onChange, catalogs }: { policy: Policy; onChange: (policy: Policy) => void; catalogs?: Partial<Record<'ramos' | 'ramosNormalizados' | 'companias' | 'formasPago' | 'medios' | 'estadoPago' | 'tipoProceso' | 'estadoRevision' | 'estadoPolizaReal' | 'emisionRenovacion', string[]>> }) {
  const num = (value: string) => value ? Number(value) : undefined
  const sumaTexto = (policy.sumaAseguradaTextoOriginal || String(policy.sumaAsegurada ?? '')).trim()
  const isMedico = (policy.ramo || '').trim().toUpperCase() === 'MEDICO'
  return (
    <>
      <DatalistField label="Aseguradora" value={policy.aseguradora || ''} options={catalogs?.companias ?? []} listId="catalogo-companias" onChange={(value) => onChange({ ...policy, aseguradora: value })} />
      <DatalistField label="Ramo" value={policy.ramo || ''} options={catalogs?.ramos ?? []} listId="catalogo-ramos" onChange={(value) => onChange({ ...policy, ramo: value })} />
      <DatalistField label="Ramo normalizado" value={policy.ramoNormalizado || ''} options={catalogs?.ramosNormalizados ?? []} listId="catalogo-ramos-normalizados" onChange={(value) => onChange({ ...policy, ramoNormalizado: value })} />
      <Field label="Poliza" value={policy.numeroPoliza || ''} onChange={(value) => onChange({ ...policy, numeroPoliza: value })} />
      <Field
        label="Cliente empresa / financiera (opcional)"
        value={policy.clienteContratanteNombre || ''}
        onChange={(value) => onChange({ ...policy, clienteContratanteNombre: value, clienteContratanteId: undefined })}
      />
      <Field label="Certificado" value={policy.certificado || ''} onChange={(value) => onChange({ ...policy, certificado: value })} />
      <Field label="Endoso" value={policy.endoso || ''} onChange={(value) => onChange({ ...policy, endoso: value })} />
      <DatalistField label="Forma de pago" value={policy.formaPago || ''} options={catalogs?.formasPago ?? []} listId="catalogo-forma-pago" onChange={(value) => onChange({ ...policy, formaPago: value })} />
      <Field label="Cuotas" type="number" value={String(policy.cuotas ?? '')} onChange={(value) => onChange({ ...policy, cuotas: num(value) })} />
      <Field label="Prima neta" type="number" value={String(policy.primaNeta ?? '')} onChange={(value) => onChange({ ...policy, primaNeta: num(value) })} />
      <Field label="Seguro asiento" type="number" value={String(policy.seguroAsiento ?? '')} onChange={(value) => onChange({ ...policy, seguroAsiento: num(value) })} />
      <Field label="Prima comercial" type="number" value={String(policy.primaComercial ?? '')} onChange={(value) => onChange({ ...policy, primaComercial: num(value) })} />
      <Field label="Impuesto" type="number" value={String(policy.impuesto ?? '')} onChange={(value) => onChange({ ...policy, impuesto: num(value) })} />
      <Field label="Gastos emision" type="number" value={String(policy.gastosEmision ?? '')} onChange={(value) => onChange({ ...policy, gastosEmision: num(value) })} />
      <Field label="Bomberos" type="number" value={String(policy.bomberos ?? '')} onChange={(value) => onChange({ ...policy, bomberos: num(value) })} />
      <Field label="Prima total" type="number" value={String(policy.primaTotal ?? '')} onChange={(value) => onChange({ ...policy, primaTotal: num(value) })} />
      <Field label="Plan" value={policy.plan || ''} onChange={(value) => onChange({ ...policy, plan: value })} />
      <Field
        label="Suma asegurada"
        value={sumaTexto}
        onChange={(value) => onChange({ ...policy, sumaAseguradaTextoOriginal: value, sumaAsegurada: num(value.replaceAll(',', '').trim()) })}
      />
      {isMedico && (
        <>
          <Field label="Maximo vitalicio" type="number" value={String(policy.maximoVitalicio ?? '')} onChange={(value) => onChange({ ...policy, maximoVitalicio: num(value) })} />
          <Field label="Suma asegurada vida" type="number" value={String(policy.sumaAseguradaVida ?? '')} onChange={(value) => onChange({ ...policy, sumaAseguradaVida: num(value) })} />
        </>
      )}
      <Field label="Mes inicio poliza" value={policy.mesInicioPoliza || ''} onChange={(value) => onChange({ ...policy, mesInicioPoliza: value })} />
      <Field label="Vigencia" type="date" value={toDateInput(policy.vigencia)} onChange={(value) => onChange({ ...policy, vigencia: value || undefined })} />
      <Field label="Hasta" type="date" value={toDateInput(policy.hasta)} onChange={(value) => onChange({ ...policy, hasta: value || undefined })} />
      <DatalistField label="Medio" value={policy.medio || ''} options={catalogs?.medios ?? []} listId="catalogo-medios" onChange={(value) => onChange({ ...policy, medio: value })} />
      <DatalistField label="Estado de pago" value={policy.estadoPago || 'SIN_VALIDAR'} options={catalogs?.estadoPago && catalogs.estadoPago.length > 0 ? catalogs.estadoPago : ['SIN_VALIDAR', 'PENDIENTE', 'MORA', 'PAGADO', 'EN_CUOTAS']} listId="catalogo-estado-pago" onChange={(value) => onChange({ ...policy, estadoPago: value })} />
      <Field label="Motivo estado pago" value={policy.motivoEstadoPago || ''} onChange={(value) => onChange({ ...policy, motivoEstadoPago: value })} />
      <Field label="Vehiculo" value={policy.vehiculo || ''} onChange={(value) => onChange({ ...policy, vehiculo: value })} />
      <Field label="Marca" value={policy.marca || ''} onChange={(value) => onChange({ ...policy, marca: value })} />
      <Field label="Modelo" value={policy.modelo || ''} onChange={(value) => onChange({ ...policy, modelo: value })} />
      <Field label="Ano" type="number" value={String(policy.anio ?? '')} onChange={(value) => onChange({ ...policy, anio: num(value) })} />
      <Field label="Color" value={policy.color || ''} onChange={(value) => onChange({ ...policy, color: value })} />
      <Field label="Tipo vehiculo" value={policy.tipoVehiculo || ''} onChange={(value) => onChange({ ...policy, tipoVehiculo: value })} />
      <Field label="Placa" value={policy.placa || ''} onChange={(value) => onChange({ ...policy, placa: value })} />
      <Field label="Motor" value={policy.motor || ''} onChange={(value) => onChange({ ...policy, motor: value })} />
      <Field label="VIN/Serie" value={policy.vinSerie || ''} onChange={(value) => onChange({ ...policy, vinSerie: value })} />
      <Field label="Chasis" value={policy.chasis || ''} onChange={(value) => onChange({ ...policy, chasis: value })} />
      <Field label="Agente" value={policy.agenteAsignado || ''} onChange={(value) => onChange({ ...policy, agenteAsignado: value })} />
      <DatalistField label="Emision o renovacion" value={policy.emisionRenovacion || ''} options={catalogs?.emisionRenovacion ?? []} listId="catalogo-emision-renovacion" onChange={(value) => onChange({ ...policy, emisionRenovacion: value })} />
      <DatalistField label="Tipo proceso" value={policy.tipoProceso || ''} options={catalogs?.tipoProceso ?? []} listId="catalogo-tipo-proceso" onChange={(value) => onChange({ ...policy, tipoProceso: value })} />
      <DatalistField label="Estado poliza real" value={policy.estadoPolizaReal || ''} options={catalogs?.estadoPolizaReal ?? []} listId="catalogo-estado-poliza-real" onChange={(value) => onChange({ ...policy, estadoPolizaReal: value })} />
      <Field label="Motivo cancelacion" value={policy.motivoCancelacion || ''} onChange={(value) => onChange({ ...policy, motivoCancelacion: value })} />
      <Field label="Observacion 2" value={policy.observacion2 || ''} onChange={(value) => onChange({ ...policy, observacion2: value })} />
      <DatalistField label="Estado revision" value={policy.estadoRevision || 'OK'} options={catalogs?.estadoRevision ?? []} listId="catalogo-estado-revision" onChange={(value) => onChange({ ...policy, estadoRevision: value })} />
      <label className="wide-field"><span>Observaciones</span><textarea value={policy.observaciones || ''} onChange={(event) => onChange({ ...policy, observaciones: event.target.value })} /></label>
      <label className="wide-field"><span>Motivo revision</span><textarea value={policy.motivoRevision || ''} onChange={(event) => onChange({ ...policy, motivoRevision: event.target.value })} /></label>
    </>
  )
}

function DatalistField({ label, value, onChange, options, listId }: { label: string; value: string; onChange: (value: string) => void; options: string[]; listId: string }) {
  return (
    <label className="field">
      <span>{label}</span>
      <input value={value} list={listId} onChange={(event) => onChange(event.target.value)} />
      <datalist id={listId}>
        {options.map((item) => <option key={item} value={item} />)}
      </datalist>
    </label>
  )
}
export function Info({ label, value }: { label: string; value: string }) { return (<div className="info-item"><span>{label}</span><strong>{value}</strong></div>) }
