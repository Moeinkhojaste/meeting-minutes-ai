import { apiBaseUrl } from '../config'
import type { ApiErrorResponse } from '../types/api'

export class ApiError extends Error {
  code: string
  status: number
  traceId?: string

  constructor(status: number, code: string, message: string, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.traceId = traceId
  }
}

function formatIfMatchHeader(version?: string): Record<string, string> {
  if (!version) return {}
  const cleanVersion = version.trim()
  const formatted = cleanVersion.startsWith('"') && cleanVersion.endsWith('"')
    ? cleanVersion
    : `"${cleanVersion}"`
  return { 'If-Match': formatted }
}

export async function fetchApi<T>(
  endpoint: string,
  options: RequestInit & { version?: string } = {}
): Promise<T> {
  const { version, headers: customHeaders, ...customOptions } = options
  const url = `${apiBaseUrl.replace(/\/+$/, '')}${endpoint}`

  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...formatIfMatchHeader(version),
    ...(customHeaders as Record<string, string>),
  }

  // Set Content-Type to application/json unless body is FormData
  if (customOptions.body && !(customOptions.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json'
  }

  try {
    const response = await fetch(url, {
      ...customOptions,
      headers,
    })

    if (!response.ok) {
      let errorData: ApiErrorResponse | null = null
      try {
        errorData = (await response.json()) as ApiErrorResponse
      } catch {
        // Response wasn't JSON
      }

      throw new ApiError(
        response.status,
        errorData?.code || `HTTP_${response.status}`,
        errorData?.message || `Request failed with status ${response.status}`,
        errorData?.traceId
      )
    }

    if (response.status === 204) {
      return {} as T
    }

    return (await response.json()) as T
  } catch (err) {
    if (err instanceof ApiError) {
      throw err
    }
    throw new ApiError(
      0,
      'NETWORK_ERROR',
      err instanceof Error ? err.message : 'Network request failed'
    )
  }
}
