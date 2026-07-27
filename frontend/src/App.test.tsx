import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App', () => {
  it('shows both approved project titles', () => {
    render(<App />)

    expect(
      screen.getByRole('heading', { name: 'Meeting Minutes AI' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText('سامانه هوشمند تولید صورت‌جلسه'),
    ).toBeInTheDocument()
  })
})
