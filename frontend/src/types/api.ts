export type MeetingProcessingStatus =
  | 'created'
  | 'uploaded'
  | 'queued'
  | 'transcribing'
  | 'generatingMinutes'
  | 'partiallyCompleted'
  | 'completed'
  | 'failed'

export interface ProcessingErrorResponse {
  code: string
  message: string
}

export interface AudioResponse {
  originalFileName: string
  contentType: string
  byteLength: number
  durationMilliseconds?: number | null
  uploadedAt: string
}

export interface ProcessingStageView {
  stage: string
  primaryProvider: string
  primaryModel: string
  actualProvider: string
  actualModel: string
  fallbackUsed: boolean
  fallbackReason?: string | null
  durationMilliseconds: number
}

export interface ProcessingRunView {
  attemptNumber: number
  requestedMode: string
  status: string
  stages: ProcessingStageView[]
}

export interface MeetingResponse {
  id: string
  title: string | null
  status: MeetingProcessingStatus
  processingError: ProcessingErrorResponse | null
  audio: AudioResponse | null
  latestRun?: ProcessingRunView | null
  createdAt: string
  updatedAt: string
  version: string
}

export interface MeetingPageResponse {
  items: MeetingResponse[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UserResponse {
  id: string
  email: string
  fullName: string
  createdAt: string
}

export interface AuthResponse {
  token: string
  tokenType: string
  user: UserResponse
  expiresAt: string
}

export interface TranscriptSegment {
  startTime?: number | null
  endTime?: number | null
  text: string
  speaker?: string | null
}

export interface RawTranscriptResponse {
  schemaVersion: number
  segments: TranscriptSegment[]
  createdAt: string
  version: string
}

export interface CleanedTranscriptResponse {
  schemaVersion: number
  segments: TranscriptSegment[]
  createdAt: string
  version: string
}

export interface ParticipantView {
  name: string
  role?: string | null
}

export interface TopicView {
  title: string
  summary?: string | null
  keyPoints?: string[]
}

export interface DecisionView {
  text: string
  rationale?: string | null
}

export interface ActionItemView {
  task: string
  assignee?: string | null
  deadline?: string | null
  status?: string | null
}

export interface OpenQuestionView {
  question: string
  raisedBy?: string | null
}

export interface UncertaintyView {
  text: string
  category?: string | null
}

export interface GeneratedMinutesResponse {
  schemaVersion: number
  title: string | null
  date: string | null
  summary: string
  participants: ParticipantView[]
  topics: TopicView[]
  decisions: DecisionView[]
  actionItems: ActionItemView[]
  openQuestions: OpenQuestionView[]
  uncertainties: UncertaintyView[]
  createdAt: string
  version: string
}

export interface CreateMeetingRequest {
  title?: string
}

export interface UpdateMeetingRequest {
  title?: string
}

export interface ProcessMeetingRequest {
  mode?: 'fast' | 'quality'
}

export interface ApiErrorResponse {
  code: string
  message: string
  traceId?: string
}
