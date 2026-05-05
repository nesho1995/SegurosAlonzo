import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle, RefreshCw } from 'lucide-react'

interface Props {
  children: ReactNode
  fallbackLabel?: string
}

interface State {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[ErrorBoundary]', error, info.componentStack)
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null })
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center',
          justifyContent: 'center', gap: 16, padding: 48, textAlign: 'center',
        }}>
          <AlertTriangle size={40} color="#dc2626" />
          <div>
            <h3 style={{ margin: '0 0 6px', fontSize: 18, fontWeight: 700, color: '#111' }}>
              {this.props.fallbackLabel ?? 'Algo salió mal'}
            </h3>
            <p style={{ margin: 0, color: '#6b7280', fontSize: 14 }}>
              {this.state.error?.message ?? 'Error inesperado en esta sección.'}
            </p>
          </div>
          <button className="secondary-button" onClick={this.handleReset}>
            <RefreshCw size={14} /> Reintentar
          </button>
        </div>
      )
    }
    return this.props.children
  }
}
