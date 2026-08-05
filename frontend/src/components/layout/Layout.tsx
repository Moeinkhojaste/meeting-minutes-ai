import React from 'react'
import { Header } from './Header'

interface LayoutProps {
  dir: 'ltr' | 'rtl'
  onToggleDir: () => void
  onNewMeetingClick: () => void
  onHomeClick: () => void
  children: React.ReactNode
}

export function Layout({
  dir,
  onToggleDir,
  onNewMeetingClick,
  onHomeClick,
  children,
}: LayoutProps) {
  return (
    <div className="app-layout" dir={dir}>
      <Header
        dir={dir}
        onToggleDir={onToggleDir}
        onNewMeetingClick={onNewMeetingClick}
        onHomeClick={onHomeClick}
      />
      <main className="main-content">{children}</main>
    </div>
  )
}
