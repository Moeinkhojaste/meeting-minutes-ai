import type { AudioResponse } from '../../types/api'

interface AudioPlayerProps {
  audio: AudioResponse
}

export function AudioPlayer({ audio }: AudioPlayerProps) {
  const formatSize = (bytes: number) => {
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB'
  }

  const formatDuration = (ms?: number | null) => {
    if (!ms) return null
    const seconds = Math.floor(ms / 1000)
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins}:${secs < 10 ? '0' : ''}${secs}`
  }

  return (
    <div
      style={{
        background: '#f8fafc',
        border: '1px solid #e2e8f0',
        borderRadius: '10px',
        padding: '12px 16px',
        margin: '12px 0',
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{ fontSize: '1.2rem' }}>🎵</span>
          <div>
            <strong style={{ fontSize: '0.9rem', color: '#1e293b' }}>{audio.originalFileName}</strong>
            <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
              {formatSize(audio.byteLength)}
              {audio.durationMilliseconds ? ` • ${formatDuration(audio.durationMilliseconds)}` : ''}
            </div>
          </div>
        </div>
        <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
          Uploaded: {new Date(audio.uploadedAt).toLocaleDateString()}
        </span>
      </div>
    </div>
  )
}
