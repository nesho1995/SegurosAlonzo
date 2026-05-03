import { useEffect, useRef, useState } from 'react'
import { Search, Shield, User, X } from 'lucide-react'
import { buscar, type BusquedaResult } from '../api/dashboardApi'

function useDebounce<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(t)
  }, [value, delay])
  return debounced
}

/** Despacha navegación al sistema de rutas propio de la app */
function navigateTo(path: string) {
  window.history.pushState(null, '', path)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

export function GlobalSearch() {
  const [open, setOpen]       = useState(false)
  const [query, setQuery]     = useState('')
  const [results, setResults] = useState<BusquedaResult | null>(null)
  const [loading, setLoading] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const debouncedQ = useDebounce(query, 300)

  // Atajos de teclado
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        setOpen(true)
      }
      if (e.key === 'Escape') setOpen(false)
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 50)
    else { setQuery(''); setResults(null) }
  }, [open])

  useEffect(() => {
    if (debouncedQ.length < 2) { setResults(null); return }
    setLoading(true)
    buscar(debouncedQ)
      .then(setResults)
      .catch(() => null)
      .finally(() => setLoading(false))
  }, [debouncedQ])

  const hasResults = results && (results.clientes.length > 0 || results.polizas.length > 0)

  function goTo(path: string) {
    navigateTo(path)
    setOpen(false)
  }

  if (!open) return null

  return (
    <div className="gsearch-overlay" onClick={(e) => e.target === e.currentTarget && setOpen(false)}>
      <div className="gsearch-box" role="dialog" aria-modal="true" aria-label="Búsqueda global">

        {/* Input */}
        <div className="gsearch-input-row">
          <Search size={17} className="gsearch-icon" />
          <input
            ref={inputRef}
            className="gsearch-input"
            placeholder="Buscar cliente, póliza, número, placa…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            autoComplete="off"
          />
          {loading && <span className="gsearch-spinner" />}
          <button className="gsearch-close" onClick={() => setOpen(false)} aria-label="Cerrar"><X size={16} /></button>
        </div>

        {/* Resultados */}
        {query.length >= 2 && (
          <div className="gsearch-results">
            {!hasResults && !loading && (
              <div className="gsearch-empty">Sin resultados para <strong>"{query}"</strong></div>
            )}

            {results && results.clientes.length > 0 && (
              <section>
                <div className="gsearch-group-label">Clientes</div>
                {results.clientes.map((c) => (
                  <button key={c.id} className="gsearch-row" onClick={() => goTo('/clientes')}>
                    <User size={14} className="gsearch-row-icon" />
                    <div className="gsearch-row-text">
                      <strong>{c.nombre}</strong>
                      <span>{c.telefono || '—'} · {c.polizasActivas} póliza{c.polizasActivas !== 1 ? 's' : ''} activa{c.polizasActivas !== 1 ? 's' : ''}</span>
                    </div>
                  </button>
                ))}
              </section>
            )}

            {results && results.polizas.length > 0 && (
              <section>
                <div className="gsearch-group-label">Pólizas</div>
                {results.polizas.map((p) => (
                  <button key={p.id} className="gsearch-row" onClick={() => goTo('/clientes')}>
                    <Shield size={14} className="gsearch-row-icon" />
                    <div className="gsearch-row-text">
                      <strong>{p.codigo || 'Sin número'} — {p.cliente}</strong>
                      <span>{p.aseguradora || '—'} · {p.hasta ? new Date(p.hasta).toLocaleDateString('es-HN') : '—'}</span>
                    </div>
                    <span className={`gsearch-badge ${!p.activo ? 'inactive' : ''}`}>
                      {p.estado || (p.activo ? 'Activa' : 'Inactiva')}
                    </span>
                  </button>
                ))}
              </section>
            )}
          </div>
        )}

        {/* Hint teclado */}
        {query.length < 2 && (
          <div className="gsearch-hint">
            <kbd>↑↓</kbd> navegar &nbsp;·&nbsp; <kbd>Enter</kbd> abrir &nbsp;·&nbsp; <kbd>Esc</kbd> cerrar
          </div>
        )}
      </div>
    </div>
  )
}

/** Botón disparador visible en el topbar */
export function SearchTrigger() {
  return (
    <button
      className="search-trigger"
      onClick={() => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }))}
      aria-label="Búsqueda global"
    >
      <Search size={15} />
      <span>Buscar…</span>
      <kbd>Ctrl K</kbd>
    </button>
  )
}
