interface ErrorAlertProps {
  title?: string
  message: string
  onRetry?: () => void
}

export function ErrorAlert({ title = 'Error', message, onRetry }: ErrorAlertProps) {
  return (
    <div
      style={{
        background: '#fef2f2',
        border: '1px solid #fecaca',
        borderRadius: '8px',
        padding: '16px',
        color: '#991b1b',
        margin: '12px 0',
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyBetween: 'space-between', gap: '8px' }}>
        <strong style={{ fontSize: '0.95rem' }}>{title}</strong>
        {onRetry && (
          <button className="btn btn-sm btn-secondary" onClick={onRetry} style={{ marginLeft: 'auto' }}>
            Retry
          </button>
        )}
      </div>
      <p style={{ margin: 0, fontSize: '0.875rem' }}>{message}</p>
    </div>
  )
}
