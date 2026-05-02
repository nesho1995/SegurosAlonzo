import type { ReactNode } from 'react'
import type { View } from '../types/common'
import { Sidebar, type NavItem } from './Sidebar'
import { AutoSendStatus, NotificationBell } from './Topbar'
import { SearchTrigger } from './GlobalSearch'

export function Layout({ navItems, view, onViewChange, children }: { navItems: NavItem[]; view: View; onViewChange: (view: View) => void; children: ReactNode }) {
  return (
    <main className="app-shell">
      <Sidebar navItems={navItems} view={view} onViewChange={onViewChange} />
      <section className="workspace">
        <div className="page-container">
          <div className="workspace-topbar">
            <SearchTrigger />
            <div className="workspace-topbar-right">
              <AutoSendStatus />
              <NotificationBell />
            </div>
          </div>
          {children}
        </div>
      </section>
    </main>
  )
}
