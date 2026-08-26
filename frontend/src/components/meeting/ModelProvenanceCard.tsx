import type { ProcessingRunView } from '../../types/api'

interface ModelProvenanceCardProps {
  run?: ProcessingRunView | null
  dir?: 'ltr' | 'rtl'
}

export function ModelProvenanceCard({ run, dir = 'ltr' }: ModelProvenanceCardProps) {
  if (!run || !run.stages || run.stages.length === 0) {
    return null
  }

  const isRtl = dir === 'rtl'
  const sttStage = run.stages.find((s) => s.stage === 'transcription')
  const minutesStage = run.stages.find((s) => s.stage === 'minutes')

  const totalDurationSeconds =
    run.stages.reduce((sum, s) => sum + (s.durationMilliseconds || 0), 0) / 1000

  const hasAnyFallback = run.stages.some((s) => s.fallbackUsed)

  return (
    <div
      style={{
        background: '#ffffff',
        borderRadius: '12px',
        border: hasAnyFallback ? '1px solid #fed7aa' : '1px solid #e2e8f0',
        boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
        padding: '14px 18px',
        marginTop: '12px',
        marginBottom: '16px',
      }}
    >
      {/* Header Bar */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '8px',
          paddingBottom: '10px',
          marginBottom: '10px',
          borderBottom: '1px solid #f1f5f9',
          fontSize: '0.85rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600, color: '#334155' }}>
          <span>🔍 {isRtl ? 'ردگیری مدل‌های استفاده‌شده' : 'Model Execution & Provenance'}</span>
          {hasAnyFallback ? (
            <span
              style={{
                fontSize: '0.75rem',
                background: '#fff7ed',
                color: '#c2410c',
                border: '1px solid #ffedd5',
                borderRadius: '9999px',
                padding: '2px 8px',
                fontWeight: 600,
              }}
            >
              ⚠️ {isRtl ? 'حالت فالبک فعال شد' : 'Fallback Active'}
            </span>
          ) : (
            <span
              style={{
                fontSize: '0.75rem',
                background: '#f0fdf4',
                color: '#15803d',
                border: '1px solid #dcfce7',
                borderRadius: '9999px',
                padding: '2px 8px',
                fontWeight: 600,
              }}
            >
              ✨ {isRtl ? 'تمام مراحل با مدل اصلی (Gemini)' : 'Primary Gemini Execution'}
            </span>
          )}
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '14px', color: '#64748b', fontSize: '0.8rem' }}>
          <span>
            {isRtl ? 'حالت:' : 'Mode:'}{' '}
            <strong style={{ color: '#0f172a', textTransform: 'capitalize' }}>{run.requestedMode}</strong>
          </span>
          {totalDurationSeconds > 0 && (
            <span>
              {isRtl ? 'مجموع زمان:' : 'Total Time:'}{' '}
              <strong style={{ color: '#0f172a' }}>{totalDurationSeconds.toFixed(1)}s</strong>
            </span>
          )}
          {run.attemptNumber > 1 && (
            <span>
              {isRtl ? `تلاش ${run.attemptNumber}` : `Attempt #${run.attemptNumber}`}
            </span>
          )}
        </div>
      </div>

      {/* Stages Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: '12px',
        }}
      >
        {/* Stage 1: STT */}
        {sttStage && (
          <div
            style={{
              background: sttStage.fallbackUsed ? '#fffbeb' : '#f8fafc',
              border: sttStage.fallbackUsed ? '1px solid #fde68a' : '1px solid #e2e8f0',
              borderRadius: '8px',
              padding: '10px 12px',
              fontSize: '0.85rem',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
              <span style={{ fontWeight: 600, color: '#1e293b' }}>
                🎙️ {isRtl ? 'مرحله ۱: تبدیل گفتار به متن (STT)' : 'Stage 1: Speech-to-Text'}
              </span>
              {sttStage.durationMilliseconds != null && (
                <span style={{ fontSize: '0.75rem', color: '#64748b', fontFamily: 'monospace' }}>
                  {(sttStage.durationMilliseconds / 1000).toFixed(1)}s
                </span>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '6px' }}>
              {/* Model Tag */}
              <span
                style={{
                  background: sttStage.actualProvider === 'gemini' ? '#e0e7ff' : '#fef3c7',
                  color: sttStage.actualProvider === 'gemini' ? '#3730a3' : '#92400e',
                  padding: '3px 8px',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '0.8rem',
                  fontFamily: 'monospace',
                }}
              >
                {sttStage.actualModel}
              </span>

              {/* Status / Fallback Indicator */}
              {!sttStage.fallbackUsed ? (
                <span style={{ fontSize: '0.75rem', color: '#16a34a', fontWeight: 600 }}>
                  ☁️ {isRtl ? 'جمنای اصلی (ابری)' : 'Gemini Primary (Cloud)'}
                </span>
              ) : (
                <span style={{ fontSize: '0.75rem', color: '#d97706', fontWeight: 600 }}>
                  💻 {isRtl ? 'فالبک: Faster-Whisper (محلی)' : 'Fallback: Faster-Whisper (Local)'}
                </span>
              )}
            </div>

            {sttStage.fallbackUsed && sttStage.fallbackReason && (
              <p style={{ margin: '4px 0 0 0', fontSize: '0.75rem', color: '#b45309' }}>
                {isRtl ? 'دلیل سوئیچ: ' : 'Reason: '}
                <code>{sttStage.fallbackReason}</code>
              </p>
            )}
          </div>
        )}

        {/* Stage 2: Minutes Generation */}
        {minutesStage && (
          <div
            style={{
              background: minutesStage.fallbackUsed ? '#fffbeb' : '#f8fafc',
              border: minutesStage.fallbackUsed ? '1px solid #fde68a' : '1px solid #e2e8f0',
              borderRadius: '8px',
              padding: '10px 12px',
              fontSize: '0.85rem',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
              <span style={{ fontWeight: 600, color: '#1e293b' }}>
                📝 {isRtl ? 'مرحله ۲: ساخت صورت‌جلسه و خلاصه‌سازی' : 'Stage 2: Minutes & Extraction'}
              </span>
              {minutesStage.durationMilliseconds != null && (
                <span style={{ fontSize: '0.75rem', color: '#64748b', fontFamily: 'monospace' }}>
                  {(minutesStage.durationMilliseconds / 1000).toFixed(1)}s
                </span>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '6px' }}>
              {/* Model Tag */}
              <span
                style={{
                  background: minutesStage.actualProvider === 'gemini' ? '#fce7f3' : '#fef3c7',
                  color: minutesStage.actualProvider === 'gemini' ? '#9d174d' : '#92400e',
                  padding: '3px 8px',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '0.8rem',
                  fontFamily: 'monospace',
                }}
              >
                {minutesStage.actualModel}
              </span>

              {/* Status / Fallback Indicator */}
              {!minutesStage.fallbackUsed ? (
                <span style={{ fontSize: '0.75rem', color: '#16a34a', fontWeight: 600 }}>
                  ☁️ {isRtl ? 'جمنای اصلی (ابری)' : 'Gemini Primary (Cloud)'}
                </span>
              ) : (
                <span style={{ fontSize: '0.75rem', color: '#d97706', fontWeight: 600 }}>
                  💻 {isRtl ? 'فالبک: Local LLM (محلی)' : 'Fallback: Local LLM (Local)'}
                </span>
              )}
            </div>

            {minutesStage.fallbackUsed && minutesStage.fallbackReason && (
              <p style={{ margin: '4px 0 0 0', fontSize: '0.75rem', color: '#b45309' }}>
                {isRtl ? 'دلیل سوئیچ: ' : 'Reason: '}
                <code>{minutesStage.fallbackReason}</code>
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
