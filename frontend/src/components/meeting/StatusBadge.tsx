import type { MeetingProcessingStatus } from '../../types/api'

interface StatusBadgeProps {
  status: MeetingProcessingStatus
}

export function StatusBadge({ status }: StatusBadgeProps) {
  const getLabel = (s: MeetingProcessingStatus) => {
    switch (s) {
      case 'created':
        return 'Created'
      case 'uploaded':
        return 'Audio Uploaded'
      case 'queued':
        return 'Queued'
      case 'transcribing':
        return 'Transcribing...'
      case 'generatingMinutes':
        return 'Generating Minutes...'
      case 'partiallyCompleted':
        return 'Partially Completed'
      case 'completed':
        return 'Completed'
      case 'failed':
        return 'Failed'
      default:
        return s
    }
  }

  return <span className={`badge badge-${status}`}>{getLabel(status)}</span>
}
