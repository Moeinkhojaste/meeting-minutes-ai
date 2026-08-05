interface HeaderProps {
  dir: 'ltr' | 'rtl'
  onToggleDir: () => void
  onNewMeetingClick: () => void
  onHomeClick: () => void
}

export function Header({ dir, onToggleDir, onNewMeetingClick, onHomeClick }: HeaderProps) {
  return (
    <header className="header">
      <div className="header-brand" onClick={onHomeClick} style={{ cursor: 'pointer' }}>
        <div className="header-logo">AI</div>
        <div>
          <h1 className="header-title">Meeting Minutes AI</h1>
          <span style={{ fontSize: '0.75rem', color: '#64748b', display: 'block' }}>
            {dir === 'rtl' ? 'سامانه هوشمند تولید صورت‌جلسه' : 'Automated Meeting Processing System'}
          </span>
        </div>
      </div>

      <div className="header-actions">
        <button
          className="btn btn-secondary btn-sm"
          onClick={onToggleDir}
          title="Toggle text direction (LTR / RTL for Persian)"
        >
          🌐 {dir === 'rtl' ? 'English (LTR)' : 'فارسی (RTL)'}
        </button>

        <button className="btn btn-primary btn-sm" onClick={onNewMeetingClick}>
          + New Meeting
        </button>
      </div>
    </header>
  )
}
