import { useState, useEffect, useCallback } from 'react'
import './App.css'
import type {
  MeetingResponse,
  RawTranscriptResponse,
  CleanedTranscriptResponse,
  GeneratedMinutesResponse,
} from './types/api'
import { meetingService } from './services/meetingService'
import { Layout } from './components/layout/Layout'
import { MeetingList } from './components/meeting/MeetingList'
import { CreateMeetingModal } from './components/meeting/CreateMeetingModal'
import { StatusBadge } from './components/meeting/StatusBadge'
import { AudioUploader } from './components/audio/AudioUploader'
import { AudioPlayer } from './components/audio/AudioPlayer'
import { TranscriptView } from './components/transcript/TranscriptView'
import { MinutesView } from './components/minutes/MinutesView'
import { Tabs } from './components/common/Tabs'
import { LoadingSpinner } from './components/common/LoadingSpinner'
import { ErrorAlert } from './components/common/ErrorAlert'

function App() {
  const [dir, setDir] = useState<'ltr' | 'rtl'>('ltr')
  const [meetings, setMeetings] = useState<MeetingResponse[]>([])
  const [selectedMeeting, setSelectedMeeting] = useState<MeetingResponse | null>(null)
  
  const [rawTranscript, setRawTranscript] = useState<RawTranscriptResponse | null>(null)
  const [cleanedTranscript, setCleanedTranscript] = useState<CleanedTranscriptResponse | null>(null)
  const [minutes, setMinutes] = useState<GeneratedMinutesResponse | null>(null)

  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [processing, setProcessing] = useState(false)
  const [processMode, setProcessMode] = useState<'fast' | 'quality'>('fast')
  const [error, setError] = useState<string | null>(null)

  const [activeTab, setActiveTab] = useState<'minutes' | 'transcripts' | 'audio'>('minutes')
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false)

  const fetchMeetings = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const page = await meetingService.listMeetings(1, 50)
      setMeetings(page.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch meetings')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchMeetings()
  }, [fetchMeetings])

  const fetchMeetingDetails = useCallback(async (meeting: MeetingResponse) => {
    setDetailLoading(true)
    setRawTranscript(null)
    setCleanedTranscript(null)
    setMinutes(null)

    try {
      const refreshedMeeting = await meetingService.getMeeting(meeting.id)
      setSelectedMeeting(refreshedMeeting)

      // Fetch transcripts & minutes in parallel if status is applicable
      const isCompleted =
        refreshedMeeting.status === 'completed' ||
        refreshedMeeting.status === 'partiallyCompleted'

      if (isCompleted || refreshedMeeting.status === 'generatingMinutes') {
        const [rawRes, cleanedRes, minutesRes] = await Promise.allSettled([
          meetingService.getRawTranscript(meeting.id),
          meetingService.getCleanedTranscript(meeting.id),
          meetingService.getGeneratedMinutes(meeting.id),
        ])

        if (rawRes.status === 'fulfilled') setRawTranscript(rawRes.value)
        if (cleanedRes.status === 'fulfilled') setCleanedTranscript(cleanedRes.value)
        if (minutesRes.status === 'fulfilled') setMinutes(minutesRes.value)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch meeting details')
    } finally {
      setDetailLoading(false)
    }
  }, [])

  // Auto-polling for active processing status
  useEffect(() => {
    if (!selectedMeeting) return

    const activeStatuses = ['queued', 'transcribing', 'generatingMinutes']
    if (!activeStatuses.includes(selectedMeeting.status)) return

    const interval = setInterval(() => {
      fetchMeetingDetails(selectedMeeting)
    }, 3000)

    return () => clearInterval(interval)
  }, [selectedMeeting, fetchMeetingDetails])

  const handleCreateMeeting = async (title?: string) => {
    const created = await meetingService.createMeeting(title)
    await fetchMeetings()
    setSelectedMeeting(created)
  }

  const handleDeleteMeeting = async (meeting: MeetingResponse) => {
    if (!confirm(`Are you sure you want to delete "${meeting.title || 'Untitled Meeting'}"?`)) return
    try {
      await meetingService.deleteMeeting(meeting.id, meeting.version)
      if (selectedMeeting?.id === meeting.id) {
        setSelectedMeeting(null)
      }
      await fetchMeetings()
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to delete meeting')
    }
  }

  const handleAudioUpload = async (file: File) => {
    if (!selectedMeeting) return
    const updated = await meetingService.uploadAudio(
      selectedMeeting.id,
      file,
      selectedMeeting.version
    )
    setSelectedMeeting(updated)
    await fetchMeetings()
  }

  const handleStartProcessing = async () => {
    if (!selectedMeeting) return
    try {
      setProcessing(true)
      setError(null)
      const updated = await meetingService.processMeeting(
        selectedMeeting.id,
        processMode,
        selectedMeeting.version
      )
      setSelectedMeeting(updated)
      await fetchMeetings()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Processing failed to start')
    } finally {
      setProcessing(false)
    }
  }

  return (
    <Layout
      dir={dir}
      onToggleDir={() => setDir(dir === 'ltr' ? 'rtl' : 'ltr')}
      onNewMeetingClick={() => setIsCreateModalOpen(true)}
      onHomeClick={() => setSelectedMeeting(null)}
    >
      <CreateMeetingModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onCreate={handleCreateMeeting}
      />

      {!selectedMeeting ? (
        // Dashboard View
        <div>
          <div style={{ marginBottom: '24px' }}>
            <h2 style={{ margin: '0 0 6px 0', fontSize: '1.6rem', color: '#0f172a' }}>
              Meeting Dashboard
            </h2>
            <p style={{ margin: 0, color: '#64748b', fontSize: '0.95rem' }}>
              Upload meeting audio recordings and generate structured meeting minutes locally.
            </p>
          </div>

          {error && <ErrorAlert message={error} onRetry={fetchMeetings} />}

          {loading ? (
            <LoadingSpinner label="Loading meetings..." />
          ) : (
            <MeetingList
              meetings={meetings}
              onSelectMeeting={(m) => {
                setSelectedMeeting(m)
                fetchMeetingDetails(m)
              }}
              onDeleteMeeting={handleDeleteMeeting}
              onCreateClick={() => setIsCreateModalOpen(true)}
            />
          )}
        </div>
      ) : (
        // Meeting Detail View
        <div>
          <div style={{ marginBottom: '16px' }}>
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => setSelectedMeeting(null)}
              style={{ marginBottom: '12px' }}
            >
              &larr; Back to Meetings Dashboard
            </button>

            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <h2 style={{ margin: 0, fontSize: '1.5rem' }}>
                    {selectedMeeting.title || 'Untitled Meeting'}
                  </h2>
                  <StatusBadge status={selectedMeeting.status} />
                </div>
                <p style={{ margin: '4px 0 0 0', fontSize: '0.85rem', color: '#64748b' }}>
                  ID: <code style={{ fontSize: '0.8rem' }}>{selectedMeeting.id}</code>
                </p>
              </div>

              {/* Action Toolbar */}
              <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                {(selectedMeeting.status === 'uploaded' || selectedMeeting.status === 'failed') && (
                  <>
                    <select
                      className="input"
                      value={processMode}
                      onChange={(e) => setProcessMode(e.target.value as 'fast' | 'quality')}
                      style={{ padding: '6px 10px', fontSize: '0.85rem' }}
                    >
                      <option value="fast">Fast Mode</option>
                      <option value="quality">Quality Mode</option>
                    </select>

                    <button
                      className="btn btn-primary"
                      onClick={handleStartProcessing}
                      disabled={processing}
                    >
                      {processing ? 'Starting...' : '⚡ Generate Minutes'}
                    </button>
                  </>
                )}

                <button
                  className="btn btn-danger btn-sm"
                  onClick={() => handleDeleteMeeting(selectedMeeting)}
                >
                  Delete
                </button>
              </div>
            </div>
          </div>

          {error && <ErrorAlert message={error} />}

          {/* Audio Section Card */}
          {!selectedMeeting.audio ? (
            <div className="glass-panel" style={{ padding: '20px', marginBottom: '20px', background: '#ffffff' }}>
              <h3 style={{ margin: '0 0 8px 0', fontSize: '1.05rem', color: '#1e293b' }}>
                1. Upload Meeting Audio
              </h3>
              <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 12px 0' }}>
                Upload the recorded meeting audio file to enable speech-to-text processing.
              </p>
              <AudioUploader onUpload={handleAudioUpload} />
            </div>
          ) : (
            <AudioPlayer audio={selectedMeeting.audio} />
          )}

          {/* Processing Indicator */}
          {['queued', 'transcribing', 'generatingMinutes'].includes(selectedMeeting.status) && (
            <div
              style={{
                background: '#eff6ff',
                border: '1px solid #bfdbfe',
                borderRadius: '10px',
                padding: '16px',
                margin: '16px 0',
                display: 'flex',
                alignItems: 'center',
                gap: '16px',
              }}
            >
              <div
                style={{
                  width: '28px',
                  height: '28px',
                  border: '3px solid #93c5fd',
                  borderTopColor: '#2563eb',
                  borderRadius: '50%',
                  animation: 'spin 0.8s linear infinite',
                }}
              />
              <div>
                <strong style={{ color: '#1e3a8a', fontSize: '0.95rem' }}>
                  Processing in progress...
                </strong>
                <p style={{ margin: '2px 0 0 0', fontSize: '0.85rem', color: '#1d4ed8' }}>
                  Current status: <StatusBadge status={selectedMeeting.status} />
                </p>
              </div>
            </div>
          )}

          {/* Tabs for Details */}
          {detailLoading ? (
            <LoadingSpinner label="Loading meeting content..." />
          ) : (
            <Tabs
              tabs={[
                { id: 'minutes', label: '📄 Generated Minutes' },
                { id: 'transcripts', label: '💬 Transcripts' },
              ]}
              activeTab={activeTab}
              onChange={(id) => setActiveTab(id as 'minutes' | 'transcripts')}
            >
              {activeTab === 'minutes' && (
                <MinutesView
                  minutes={minutes}
                  loading={detailLoading}
                  error={null}
                  onRefresh={() => fetchMeetingDetails(selectedMeeting)}
                />
              )}

              {activeTab === 'transcripts' && (
                <TranscriptView
                  rawTranscript={rawTranscript}
                  cleanedTranscript={cleanedTranscript}
                  loading={detailLoading}
                  error={null}
                  onRefresh={() => fetchMeetingDetails(selectedMeeting)}
                />
              )}
            </Tabs>
          )}
        </div>
      )}
    </Layout>
  )
}

export default App
