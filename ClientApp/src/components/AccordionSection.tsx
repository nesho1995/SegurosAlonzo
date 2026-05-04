import type { ReactNode } from 'react'
import { ChevronDown } from 'lucide-react'

export function AccordionSection({ title, subtitle, defaultOpen = true, children, actions }: { title: string; subtitle?: string; defaultOpen?: boolean; children: ReactNode; actions?: ReactNode }) {
  return (
    <details className="accordion-section" open={defaultOpen}>
      <summary>
        <span>
          <strong>{title}</strong>
          {subtitle && <small>{subtitle}</small>}
        </span>
        <span className="accordion-actions">
          {actions}
          <ChevronDown size={18} />
        </span>
      </summary>
      <div className="accordion-body">{children}</div>
    </details>
  )
}
