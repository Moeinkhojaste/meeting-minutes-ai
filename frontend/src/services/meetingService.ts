import { fetchApi } from './api'
import type {
  MeetingResponse,
  MeetingPageResponse,
  RawTranscriptResponse,
  CleanedTranscriptResponse,
  GeneratedMinutesResponse,
} from '../types/api'

export const meetingService = {
  async listMeetings(page = 1, pageSize = 20): Promise<MeetingPageResponse> {
    return fetchApi<MeetingPageResponse>(
      `/api/meetings?page=${page}&pageSize=${pageSize}`
    )
  },

  async getMeeting(id: string): Promise<MeetingResponse> {
    return fetchApi<MeetingResponse>(`/api/meetings/${id}`)
  },

  async createMeeting(title?: string): Promise<MeetingResponse> {
    return fetchApi<MeetingResponse>('/api/meetings', {
      method: 'POST',
      body: JSON.stringify({ title: title?.trim() || null }),
    })
  },

  async updateMeeting(
    id: string,
    title: string,
    version: string
  ): Promise<MeetingResponse> {
    return fetchApi<MeetingResponse>(`/api/meetings/${id}`, {
      method: 'PUT',
      version,
      body: JSON.stringify({ title: title.trim() }),
    })
  },

  async deleteMeeting(id: string, version: string): Promise<void> {
    await fetchApi<void>(`/api/meetings/${id}`, {
      method: 'DELETE',
      version,
    })
  },

  async uploadAudio(
    id: string,
    file: File,
    version: string
  ): Promise<MeetingResponse> {
    const formData = new FormData()
    formData.append('audio', file)

    return fetchApi<MeetingResponse>(`/api/meetings/${id}/audio`, {
      method: 'POST',
      version,
      body: formData,
    })
  },

  async processMeeting(
    id: string,
    mode: 'fast' | 'quality' = 'fast',
    version: string
  ): Promise<MeetingResponse> {
    return fetchApi<MeetingResponse>(`/api/meetings/${id}/process`, {
      method: 'POST',
      version,
      body: JSON.stringify({ mode }),
    })
  },

  async getRawTranscript(id: string): Promise<RawTranscriptResponse> {
    return fetchApi<RawTranscriptResponse>(`/api/meetings/${id}/transcripts/raw`)
  },

  async getCleanedTranscript(id: string): Promise<CleanedTranscriptResponse> {
    return fetchApi<CleanedTranscriptResponse>(
      `/api/meetings/${id}/transcripts/cleaned`
    )
  },

  async getGeneratedMinutes(id: string): Promise<GeneratedMinutesResponse> {
    return fetchApi<GeneratedMinutesResponse>(
      `/api/meetings/${id}/minutes/generated`
    )
  },
}
