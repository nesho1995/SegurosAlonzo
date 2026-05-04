import type { ReactNode } from 'react'

export function ConfirmDialog({ open, title, message, confirmText = 'Confirmar', cancelText = 'Cancelar', danger = false, busy = false, onConfirm, onCancel }: { open: boolean; title: string; message: ReactNode; confirmText?: string; cancelText?: string; danger?: boolean; busy?: boolean; onConfirm: () => void; onCancel: () => void }) {
  if (!open) return null

  return (
    <div className="modal-backdrop" onClick={onCancel}>
      <div className="panel confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="panel-header">
          <h2>{title}</h2>
        </div>
        <div className="confirm-dialog-body">{message}</div>
        <div className="form-actions confirm-dialog-actions">
          <button className="icon-button secondary" type="button" disabled={busy} onClick={onCancel}>{cancelText}</button>
          <button className={danger ? 'icon-button danger-button' : 'primary-button'} type="button" disabled={busy} onClick={onConfirm}>
            {busy ? 'Procesando...' : confirmText}
          </button>
        </div>
      </div>
    </div>
  )
}
