import { useState } from 'react'
import type { RawTranscriptResponse, CleanedTranscriptResponse } from '../../types/api'
import { LoadingSpinner } from '../common/LoadingSpinner'
import { ErrorAlert } from '../common/ErrorAlert'

interface TranscriptViewProps {
  rawTranscript: RawTranscriptResponse | null
  cleanedTranscript: CleanedTranscriptResponse | null
  loading: boolean
  error: string | null
  onRefresh?: () => void
}

export function TranscriptView({
  rawTranscript,
  cleanedTranscript,
  loading,
  error,
  onRefresh,
}: TranscriptViewProps) {
  const [activeTab, setActiveTab] = useState<'raw' | 'cleaned'>('cleaned')
  const [searchTerm, setSearchTerm] = useState('')

  if (loading) {
    return <LoadingSpinner label="Loading meeting transcript..." />
  }

  if (error) {
    return <ErrorAlert title="Transcript Unavailable" message={error} onRetry={onRefresh} />
  }

  const currentTranscript = activeTab === 'cleaned' ? cleanedTranscript : rawTranscript
  const segments = currentTranscript?.segments || []

  const filteredSegments = segments.filter(
    (s) =>
      !searchTerm ||
      s.text.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (s.speaker && s.speaker.toLowerCase().includes(searchTerm.toLowerCase()))
  )

  const formatTimestamp = (seconds?: number | null) => {
    if (seconds == null) return ''
    const mins = Math.floor(seconds / 60)
    const secs = Math.floor(seconds % 60)
    return `[${mins}:${secs < 10 ? '0' : ''}${secs}]`
  }

  return (
    <div style={{ background: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0', padding: '20px' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            className={`btn btn-sm ${activeTab === 'cleaned' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setActiveTab('cleaned')}
          >
            Cleaned Transcript ({cleanedTranscript?.segments.length || 0})
          </button>
          <button
            className={`btn btn-sm ${activeTab === 'raw' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setActiveTab('raw')}
          >
            Raw Transcript ({rawTranscript?.segments.length || 0})
          </button>
        </div>

        <input
          type="text"
          className="input"
          placeholder="Filter transcript..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          style={{ width: '220px' }}
        />
      </div>

      {segments.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '32px', color: '#64748b' }}>
          <p style={{ margin: 0 }}>No transcript segments available for this meeting yet.</p>
        </div>
      ) : filteredSegments.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '24px', color: '#64748b' }}>
          <p style={{ margin: 0 }}>No segments match "{searchTerm}".</p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', maxHeight: '600px', overflowY: 'auto', paddingRight: '8px' }}>
          {filteredSegments.map((segment, index) => (
            <div
              key={index}
              style={{
                padding: '12px 14px',
                borderRadius: '8px',
                background: index % 2 === 0 ? '#f8fafc' : '#ffffff',
                border: '1px solid #f1f5f9',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                {segment.speaker && (
                  <span style={{ fontWeight: 600, fontSize: '0.85rem', color: '#4f46e5' }}>
                    {segment.speaker}
                  </span>
                )}
                {segment.startTime != null && (
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8', fontFamily: 'monospace' }}>
                    {formatTimestamp(segment.startTime)}
                    {segment.endTime != null ? ` - ${formatTimestamp(segment.endTime)}` : ''}
                  </span>
                )}
              </div>
              <p style={{ margin: 0, fontSize: '0.92rem', color: '#1e293b', lineHeight: 1.5 }}>
                {segment.text}
              </p>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
