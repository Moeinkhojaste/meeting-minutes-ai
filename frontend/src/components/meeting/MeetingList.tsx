import { useState } from 'react'
import type { MeetingResponse } from '../../types/api'
import { MeetingCard } from './MeetingCard'

interface MeetingListProps {
  meetings: MeetingResponse[]
  onSelectMeeting: (meeting: MeetingResponse) => void
  onDeleteMeeting: (meeting: MeetingResponse) => void
  onCreateClick: () => void
}

export function MeetingList({
  meetings,
  onSelectMeeting,
  onDeleteMeeting,
  onCreateClick,
}: MeetingListProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [filterStatus, setFilterStatus] = useState<string>('all')

  const filteredMeetings = meetings.filter((m) => {
    const matchesSearch =
      !searchTerm ||
      (m.title && m.title.toLowerCase().includes(searchTerm.toLowerCase())) ||
      (m.audio?.originalFileName &&
        m.audio.originalFileName.toLowerCase().includes(searchTerm.toLowerCase()))

    const matchesStatus = filterStatus === 'all' || m.status === filterStatus
    return matchesSearch && matchesStatus
  })

  return (
    <div>
      <div style={{ display: 'flex', gap: '12px', marginBottom: '20px', flexWrap: 'wrap', alignItems: 'center' }}>
        <input
          type="text"
          className="input"
          placeholder="Search meetings by title or file name..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          style={{ flex: 1, minWidth: '240px' }}
        />
        <select
          className="input"
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value)}
          style={{ width: 'auto', minWidth: '160px' }}
        >
          <option value="all">All Statuses</option>
          <option value="created">Created</option>
          <option value="uploaded">Uploaded</option>
          <option value="queued">Queued</option>
          <option value="transcribing">Transcribing</option>
          <option value="generatingMinutes">Generating Minutes</option>
          <option value="completed">Completed</option>
          <option value="failed">Failed</option>
        </select>
        <button className="btn btn-primary" onClick={onCreateClick}>
          + New Meeting
        </button>
      </div>

      {filteredMeetings.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '48px 16px', background: '#ffffff', borderRadius: '12px', border: '1px dashed #cbd5e1' }}>
          <span style={{ fontSize: '2.5rem' }}>📂</span>
          <h3 style={{ margin: '12px 0 4px 0', color: '#1e293b' }}>No meetings found</h3>
          <p style={{ color: '#64748b', fontSize: '0.9rem', marginBottom: '16px' }}>
            {meetings.length === 0
              ? 'Get started by creating your first meeting and uploading audio.'
              : 'No meetings match your current search or filter criteria.'}
          </p>
          {meetings.length === 0 && (
            <button className="btn btn-primary" onClick={onCreateClick}>
              Create Meeting
            </button>
          )}
        </div>
      ) : (
        <div className="meetings-grid">
          {filteredMeetings.map((meeting) => (
            <MeetingCard
              key={meeting.id}
              meeting={meeting}
              onSelect={onSelectMeeting}
              onDelete={onDeleteMeeting}
            />
          ))}
        </div>
      )}
    </div>
  )
}
