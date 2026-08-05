import type { GeneratedMinutesResponse } from '../types/api'

export function exportAsJson(minutes: GeneratedMinutesResponse, filename?: string): void {
  const jsonStr = JSON.stringify(minutes, null, 2)
  downloadBlob(jsonStr, 'application/json', `${filename || 'meeting-minutes'}.json`)
}

export function exportAsMarkdown(minutes: GeneratedMinutesResponse, filename?: string): void {
  const lines: string[] = []

  lines.push(`# ${minutes.title || 'Meeting Minutes'}`)
  if (minutes.date) {
    lines.push(`**Date:** ${minutes.date}`)
  }
  if (minutes.participants.length > 0) {
    const participantNames = minutes.participants.map((p) => (p.role ? `${p.name} (${p.role})` : p.name))
    lines.push(`**Participants:** ${participantNames.join(', ')}`)
  }
  lines.push('')

  lines.push('## Executive Summary')
  lines.push(minutes.summary || '_No summary available._')
  lines.push('')

  if (minutes.topics.length > 0) {
    lines.push('## Discussion Topics')
    minutes.topics.forEach((topic, i) => {
      lines.push(`### ${i + 1}. ${topic.title}`)
      if (topic.summary) {
        lines.push(topic.summary)
      }
      if (topic.keyPoints && topic.keyPoints.length > 0) {
        topic.keyPoints.forEach((kp) => lines.push(`- ${kp}`))
      }
      lines.push('')
    })
  }

  if (minutes.decisions.length > 0) {
    lines.push('## Key Decisions')
    minutes.decisions.forEach((d) => {
      lines.push(`- ${d.text}${d.rationale ? ` *(Rationale: ${d.rationale})*` : ''}`)
    })
    lines.push('')
  }

  if (minutes.actionItems.length > 0) {
    lines.push('## Action Items')
    lines.push('| Task | Assignee | Deadline | Status |')
    lines.push('| --- | --- | --- | --- |')
    minutes.actionItems.forEach((item) => {
      lines.push(
        `| ${item.task} | ${item.assignee || '-'} | ${item.deadline || '-'} | ${item.status || 'Pending'} |`
      )
    })
    lines.push('')
  }

  if (minutes.openQuestions && minutes.openQuestions.length > 0) {
    lines.push('## Open Questions')
    minutes.openQuestions.forEach((q) => {
      lines.push(`- ${q.question}${q.raisedBy ? ` *(Raised by: ${q.raisedBy})*` : ''}`)
    })
    lines.push('')
  }

  if (minutes.uncertainties && minutes.uncertainties.length > 0) {
    lines.push('## Uncertainties / Discrepancies')
    minutes.uncertainties.forEach((u) => {
      lines.push(`- ${u.text}${u.category ? ` [${u.category}]` : ''}`)
    })
    lines.push('')
  }

  const content = lines.join('\n')
  downloadBlob(content, 'text/markdown;charset=utf-8', `${filename || 'meeting-minutes'}.md`)
}

export function exportAsPlainText(minutes: GeneratedMinutesResponse, filename?: string): void {
  const lines: string[] = []

  lines.push(`MEETING MINUTES: ${minutes.title || 'Untitled Meeting'}`)
  if (minutes.date) lines.push(`Date: ${minutes.date}`)
  if (minutes.participants.length > 0) {
    lines.push(`Participants: ${minutes.participants.map((p) => p.name).join(', ')}`)
  }
  lines.push('=' .repeat(50))
  lines.push('')

  lines.push('SUMMARY:')
  lines.push(minutes.summary || 'None')
  lines.push('')

  if (minutes.topics.length > 0) {
    lines.push('TOPICS:')
    minutes.topics.forEach((t, i) => {
      lines.push(`${i + 1}. ${t.title}`)
      if (t.summary) lines.push(`   ${t.summary}`)
    })
    lines.push('')
  }

  if (minutes.decisions.length > 0) {
    lines.push('DECISIONS:')
    minutes.decisions.forEach((d) => lines.push(`- ${d.text}`))
    lines.push('')
  }

  if (minutes.actionItems.length > 0) {
    lines.push('ACTION ITEMS:')
    minutes.actionItems.forEach((a) => {
      lines.push(`- [ ] ${a.task} (Assignee: ${a.assignee || 'Unassigned'}, Due: ${a.deadline || 'N/A'})`)
    })
    lines.push('')
  }

  const content = lines.join('\n')
  downloadBlob(content, 'text/plain;charset=utf-8', `${filename || 'meeting-minutes'}.txt`)
}

export function printMinutes(): void {
  window.print()
}

function downloadBlob(content: string, mimeType: string, filename: string): void {
  const blob = new Blob([content], { type: mimeType })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
