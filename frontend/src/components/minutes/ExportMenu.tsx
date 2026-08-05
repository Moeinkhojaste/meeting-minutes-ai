import { useState } from 'react'
import type { GeneratedMinutesResponse } from '../../types/api'
import {
  exportAsJson,
  exportAsMarkdown,
  exportAsPlainText,
  printMinutes,
} from '../../services/exportService'

interface ExportMenuProps {
  minutes: GeneratedMinutesResponse
}

export function ExportMenu({ minutes }: ExportMenuProps) {
  const [isOpen, setIsOpen] = useState(false)

  const sanitizedTitle = (minutes.title || 'meeting-minutes')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')

  return (
    <div style={{ position: 'relative', display: 'inline-block' }}>
      <button className="btn btn-primary btn-sm no-print" onClick={() => setIsOpen(!isOpen)}>
        📥 Export Minutes ▼
      </button>

      {isOpen && (
        <div
          className="no-print"
          style={{
            position: 'absolute',
            right: 0,
            top: '100%',
            marginTop: '6px',
            background: '#ffffff',
            border: '1px solid #e2e8f0',
            borderRadius: '8px',
            boxShadow: '0 10px 15px -3px rgba(0,0,0,0.1)',
            padding: '6px',
            display: 'flex',
            flexDirection: 'column',
            gap: '4px',
            zIndex: 50,
            minWidth: '160px',
          }}
        >
          <button
            className="btn btn-secondary btn-sm"
            style={{ justifyContent: 'flex-start' }}
            onClick={() => {
              exportAsMarkdown(minutes, sanitizedTitle)
              setIsOpen(false)
            }}
          >
            📄 Markdown (.md)
          </button>
          <button
            className="btn btn-secondary btn-sm"
            style={{ justifyContent: 'flex-start' }}
            onClick={() => {
              exportAsJson(minutes, sanitizedTitle)
              setIsOpen(false)
            }}
          >
            {'{ }'} JSON (.json)
          </button>
          <button
            className="btn btn-secondary btn-sm"
            style={{ justifyContent: 'flex-start' }}
            onClick={() => {
              exportAsPlainText(minutes, sanitizedTitle)
              setIsOpen(false)
            }}
          >
            📝 Plain Text (.txt)
          </button>
          <button
            className="btn btn-secondary btn-sm"
            style={{ justifyContent: 'flex-start' }}
            onClick={() => {
              printMinutes()
              setIsOpen(false)
            }}
          >
            🖨️ Print / Save as PDF
          </button>
        </div>
      )}
    </div>
  )
}
