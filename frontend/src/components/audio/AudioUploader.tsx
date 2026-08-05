import React, { useState, useRef } from 'react'

interface AudioUploaderProps {
  onUpload: (file: File) => Promise<void>
  disabled?: boolean
}

export function AudioUploader({ onUpload, disabled }: AudioUploaderProps) {
  const [isDragOver, setIsDragOver] = useState(false)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const allowedExtensions = ['.wav', '.mp3', '.m4a', '.flac', '.ogg', '.aac', '.wma']
  const maxSizeBytes = 500 * 1024 * 1024 // 500MB

  const handleFileSelect = (file: File) => {
    setError(null)
    const ext = '.' + file.name.split('.').pop()?.toLowerCase()
    if (!allowedExtensions.includes(ext)) {
      setError(`Unsupported file type. Please upload audio files (${allowedExtensions.join(', ')})`)
      return
    }
    if (file.size > maxSizeBytes) {
      setError('File size exceeds 500MB limit.')
      return
    }
    setSelectedFile(file)
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
    if (disabled || uploading) return
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileSelect(e.dataTransfer.files[0])
    }
  }

  const handleUploadSubmit = async () => {
    if (!selectedFile) return
    try {
      setUploading(true)
      setError(null)
      await onUpload(selectedFile)
      setSelectedFile(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Audio upload failed')
    } finally {
      setUploading(false)
    }
  }

  return (
    <div style={{ margin: '16px 0' }}>
      <div
        className={`uploader-box ${isDragOver ? 'drag-over' : ''}`}
        onDragOver={(e) => {
          e.preventDefault()
          if (!disabled && !uploading) setIsDragOver(true)
        }}
        onDragLeave={() => setIsDragOver(false)}
        onDrop={handleDrop}
        onClick={() => !disabled && !uploading && fileInputRef.current?.click()}
      >
        <input
          type="file"
          ref={fileInputRef}
          style={{ display: 'none' }}
          accept="audio/*,.wav,.mp3,.m4a,.flac,.ogg,.aac"
          onChange={(e) => {
            if (e.target.files && e.target.files.length > 0) {
              handleFileSelect(e.target.files[0])
            }
          }}
        />
        <div className="uploader-icon">🎙️</div>
        {selectedFile ? (
          <div>
            <p style={{ fontWeight: 600, margin: '4px 0', color: '#1e293b' }}>{selectedFile.name}</p>
            <p style={{ fontSize: '0.8rem', color: '#64748b', margin: 0 }}>
              {(selectedFile.size / (1024 * 1024)).toFixed(2)} MB
            </p>
          </div>
        ) : (
          <div>
            <p style={{ fontWeight: 500, margin: '4px 0' }}>
              Drag & drop meeting audio here, or <span style={{ color: '#4f46e5', textDecoration: 'underline' }}>browse</span>
            </p>
            <p style={{ fontSize: '0.8rem', color: '#64748b', margin: 0 }}>
              Supports WAV, MP3, M4A, FLAC, OGG (Max 500MB)
            </p>
          </div>
        )}
      </div>

      {error && (
        <p style={{ color: '#dc2626', fontSize: '0.85rem', marginTop: '8px' }}>{error}</p>
      )}

      {selectedFile && (
        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', marginTop: '12px' }}>
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => setSelectedFile(null)}
            disabled={uploading}
          >
            Cancel
          </button>
          <button
            className="btn btn-primary btn-sm"
            onClick={handleUploadSubmit}
            disabled={uploading}
          >
            {uploading ? 'Uploading...' : 'Upload Audio'}
          </button>
        </div>
      )}
    </div>
  )
}
