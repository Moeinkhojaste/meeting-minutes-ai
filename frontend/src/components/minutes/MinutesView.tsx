import type { GeneratedMinutesResponse } from '../../types/api'
import { LoadingSpinner } from '../common/LoadingSpinner'
import { ErrorAlert } from '../common/ErrorAlert'
import { ExportMenu } from './ExportMenu'

interface MinutesViewProps {
  minutes: GeneratedMinutesResponse | null
  loading: boolean
  error: string | null
  onRefresh?: () => void
}

export function MinutesView({ minutes, loading, error, onRefresh }: MinutesViewProps) {
  if (loading) {
    return <LoadingSpinner label="Generating meeting minutes..." />
  }

  if (error) {
    return <ErrorAlert title="Minutes Unavailable" message={error} onRetry={onRefresh} />
  }

  if (!minutes) {
    return (
      <div style={{ textAlign: 'center', padding: '32px', color: '#64748b', background: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0' }}>
        <p style={{ margin: 0 }}>No generated meeting minutes available yet.</p>
      </div>
    )
  }

  return (
    <div className="glass-panel" style={{ padding: '24px', background: '#ffffff' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '20px', gap: '16px', flexWrap: 'wrap' }}>
        <div>
          <h2 dir="auto" style={{ margin: '0 0 6px 0', fontSize: '1.4rem', color: '#0f172a', unicodeBidi: 'plaintext', textAlign: 'start' }}>
            {minutes.title || 'Project Meeting Minutes'}
          </h2>
          <div style={{ display: 'flex', gap: '16px', fontSize: '0.85rem', color: '#64748b', flexWrap: 'wrap' }}>
            {minutes.date && <span>📅 Date: {minutes.date}</span>}
            {minutes.participants.length > 0 && (
              <span dir="auto">
                👥 Participants: {minutes.participants.map((p) => (p.role ? `${p.name} (${p.role})` : p.name)).join(', ')}
              </span>
            )}
          </div>
        </div>

        <ExportMenu minutes={minutes} />
      </div>

      <hr style={{ border: 'none', borderTop: '1px solid #e2e8f0', margin: '16px 0' }} />

      {/* Executive Summary */}
      <section style={{ marginBottom: '24px' }}>
        <h3 style={{ fontSize: '1.1rem', color: '#1e293b', marginTop: 0, marginBottom: '8px' }}>
          📝 Executive Summary
        </h3>
        <div dir="auto" style={{ background: '#f8fafc', borderLeft: '4px solid #4f46e5', padding: '12px 16px', borderRadius: '0 8px 8px 0', fontSize: '0.95rem', lineHeight: 1.6, unicodeBidi: 'plaintext', textAlign: 'start' }}>
          {minutes.summary || 'No summary available.'}
        </div>
      </section>

      {/* Topics */}
      {minutes.topics && minutes.topics.length > 0 && (
        <section style={{ marginBottom: '24px' }}>
          <h3 style={{ fontSize: '1.1rem', color: '#1e293b', marginBottom: '12px' }}>
            💡 Main Discussion Topics
          </h3>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {minutes.topics.map((topic, i) => (
              <div key={i} style={{ border: '1px solid #e2e8f0', borderRadius: '8px', padding: '14px' }}>
                <h4 style={{ margin: '0 0 6px 0', fontSize: '0.98rem', color: '#334155' }}>
                  {i + 1}. {topic.title}
                </h4>
                {topic.summary && (
                  <p style={{ margin: '0 0 8px 0', fontSize: '0.9rem', color: '#475569', lineHeight: 1.5 }}>
                    {topic.summary}
                  </p>
                )}
                {topic.keyPoints && topic.keyPoints.length > 0 && (
                  <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '0.875rem', color: '#64748b' }}>
                    {topic.keyPoints.map((kp, kpIndex) => (
                      <li key={kpIndex}>{kp}</li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Decisions */}
      {minutes.decisions && minutes.decisions.length > 0 && (
        <section style={{ marginBottom: '24px' }}>
          <h3 style={{ fontSize: '1.1rem', color: '#1e293b', marginBottom: '12px' }}>
            ✅ Key Decisions
          </h3>
          <ul style={{ margin: 0, paddingLeft: '20px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {minutes.decisions.map((dec, i) => (
              <li key={i} style={{ fontSize: '0.92rem', color: '#1e293b', lineHeight: 1.5 }}>
                <strong>{dec.text}</strong>
                {dec.rationale && (
                  <span style={{ color: '#64748b', fontSize: '0.85rem' }}> — Rationale: {dec.rationale}</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* Action Items */}
      {minutes.actionItems && minutes.actionItems.length > 0 && (
        <section style={{ marginBottom: '24px' }}>
          <h3 style={{ fontSize: '1.1rem', color: '#1e293b', marginBottom: '12px' }}>
            📌 Action Items
          </h3>
          <div style={{ overflowX: 'auto' }}>
            <table className="action-items-table">
              <thead>
                <tr>
                  <th>Task Description</th>
                  <th>Assignee</th>
                  <th>Deadline</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {minutes.actionItems.map((item, i) => (
                  <tr key={i}>
                    <td style={{ fontWeight: 500 }}>{item.task}</td>
                    <td>{item.assignee || <span style={{ color: '#94a3b8' }}>Unassigned</span>}</td>
                    <td>{item.deadline || <span style={{ color: '#94a3b8' }}>None</span>}</td>
                    <td>
                      <span className="badge badge-queued">{item.status || 'Pending'}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Open Questions */}
      {minutes.openQuestions && minutes.openQuestions.length > 0 && (
        <section style={{ marginBottom: '24px' }}>
          <h3 style={{ fontSize: '1.1rem', color: '#1e293b', marginBottom: '8px' }}>
            ❓ Open Questions
          </h3>
          <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '0.9rem', color: '#334155' }}>
            {minutes.openQuestions.map((q, i) => (
              <li key={i}>
                {q.question} {q.raisedBy && <span style={{ color: '#64748b' }}>(Raised by {q.raisedBy})</span>}
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* Uncertainties */}
      {minutes.uncertainties && minutes.uncertainties.length > 0 && (
        <section style={{ marginBottom: '16px' }}>
          <h3 style={{ fontSize: '1.1rem', color: '#b45309', marginBottom: '8px' }}>
            ⚠️ Model Uncertainties & Notes
          </h3>
          <div style={{ background: '#fefce8', border: '1px solid #fef08a', borderRadius: '8px', padding: '12px', fontSize: '0.85rem', color: '#713f12' }}>
            <ul style={{ margin: 0, paddingLeft: '18px' }}>
              {minutes.uncertainties.map((u, i) => (
                <li key={i}>{u.text}</li>
              ))}
            </ul>
          </div>
        </section>
      )}
    </div>
  )
}
