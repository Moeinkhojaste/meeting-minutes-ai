import type { MeetingResponse } from '../../types/api'
import { StatusBadge } from './StatusBadge'

interface MeetingCardProps {
  meeting: MeetingResponse
  onSelect: (meeting: MeetingResponse) => void
  onDelete?: (meeting: MeetingResponse) => void
}

export function MeetingCard({ meeting, onSelect, onDelete }: MeetingCardProps) {
  return (
    <div className="meeting-card" onClick={() => onSelect(meeting)}>
      <div>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '8px', marginBottom: '8px' }}>
          <h4 style={{ margin: 0, fontSize: '1rem', color: '#0f172a', fontWeight: 600 }}>
            {meeting.title || 'Untitled Meeting'}
          </h4>
          <StatusBadge status={meeting.status} />
        </div>

        <p style={{ fontSize: '0.8rem', color: '#64748b', margin: '4px 0 12px 0' }}>
          Created {new Date(meeting.createdAt).toLocaleDateString()} at {new Date(meeting.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </p>

        {meeting.audio && (
          <div style={{ fontSize: '0.8rem', color: '#334155', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span>🎧</span> {meeting.audio.originalFileName} ({(meeting.audio.byteLength / (1024 * 1024)).toFixed(1)} MB)
          </div>
        )}

        {meeting.processingError && (
          <div style={{ fontSize: '0.75rem', color: '#dc2626', marginTop: '8px', background: '#fee2e2', padding: '4px 8px', borderRadius: '4px' }}>
            Error: {meeting.processingError.message}
          </div>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', paddingTop: '12px', borderTop: '1px solid #f1f5f9' }}>
        <span style={{ fontSize: '0.8rem', color: '#4f46e5', fontWeight: 500 }}>View Details &rarr;</span>
        {onDelete && (
          <button
            className="btn btn-sm btn-secondary"
            onClick={(e) => {
              e.stopPropagation()
              onDelete(meeting)
            }}
            title="Delete meeting"
            style={{ color: '#ef4444', borderColor: '#fecaca' }}
          >
            Delete
          </button>
        )}
      </div>
    </div>
  )
}
