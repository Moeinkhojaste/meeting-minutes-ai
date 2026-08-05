import type { ProcessingRunView } from '../../types/api'

interface ModelProvenanceCardProps {
  run?: ProcessingRunView | null
}

export function ModelProvenanceCard({ run }: ModelProvenanceCardProps) {
  if (!run || !run.stages || run.stages.length === 0) {
    return null
  }

  const sttStage = run.stages.find((s) => s.stage === 'transcription')
  const minutesStage = run.stages.find((s) => s.stage === 'minutes')

  return (
    <div
      dir="ltr"
      style={{
        background: '#f8fafc',
        borderRadius: '10px',
        border: '1px solid #e2e8f0',
        padding: '12px 16px',
        marginTop: '12px',
        marginBottom: '16px',
        display: 'flex',
        flexWrap: 'wrap',
        gap: '16px',
        alignItems: 'center',
        justifyContent: 'space-between',
        fontSize: '0.85rem',
      }}
    >
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '16px', alignItems: 'center' }}>
        {/* STT Model Badge */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <span style={{ color: '#64748b', fontWeight: 500 }}>Speech-to-Text:</span>
          <span
            style={{
              background: '#e0e7ff',
              color: '#3730a3',
              padding: '2px 8px',
              borderRadius: '6px',
              fontWeight: 600,
              fontFamily: 'monospace',
            }}
          >
            {sttStage ? `${sttStage.actualProvider} (${sttStage.actualModel})` : 'faster-whisper (local)'}
          </span>
          <span style={{ fontSize: '0.75rem', color: '#16a34a', fontWeight: 600 }}>[Local GPU/CPU]</span>
        </div>

        {/* Minutes Model Badge */}
        {minutesStage && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ color: '#64748b', fontWeight: 500 }}>Summarization:</span>
            <span
              style={{
                background: minutesStage.actualProvider === 'gemini' ? '#fce7f3' : '#fef9c3',
                color: minutesStage.actualProvider === 'gemini' ? '#9d174d' : '#854d0e',
                padding: '2px 8px',
                borderRadius: '6px',
                fontWeight: 600,
                fontFamily: 'monospace',
              }}
            >
              {minutesStage.actualProvider} ({minutesStage.actualModel})
            </span>
            {minutesStage.fallbackUsed && (
              <span style={{ fontSize: '0.75rem', color: '#b45309' }}>
                (Fallback: {minutesStage.fallbackReason || 'Skipped'})
              </span>
            )}
          </div>
        )}
      </div>

      {/* Mode & Timing */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: '#64748b' }}>
        <span>
          Mode: <strong style={{ color: '#0f172a' }}>{run.requestedMode}</strong>
        </span>
        {sttStage?.durationMilliseconds != null && (
          <span>
            Duration: <strong style={{ color: '#0f172a' }}>{(sttStage.durationMilliseconds / 1000).toFixed(1)}s</strong>
          </span>
        )}
      </div>
    </div>
  )
}
