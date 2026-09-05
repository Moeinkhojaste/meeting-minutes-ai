import { fetchApi, setAuthToken } from './api'
import type { AuthResponse, UserResponse } from '../types/api'

export const authService = {
  async register(email: string, password: string, fullName: string): Promise<AuthResponse> {
    const response = await fetchApi<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, fullName }),
    })
    setAuthToken(response.token)
    return response
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const response = await fetchApi<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    setAuthToken(response.token)
    return response
  },

  async getCurrentUser(): Promise<UserResponse> {
    return fetchApi<UserResponse>('/api/auth/me')
  },

  logout(): void {
    setAuthToken(null)
  },
}
