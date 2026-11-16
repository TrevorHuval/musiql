import { ApiError } from './client'

export function problemToMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (!(error instanceof ApiError)) {
    return error instanceof Error && error.message ? error.message : fallback
  }

  const fieldErrors = error.fieldErrors
  if (fieldErrors) {
    const messages = Object.values(fieldErrors).flat()
    if (messages.length > 0) return messages.join(' ')
  }

  // These problems carry a message written for the user; show it as is.
  const type = error.problem?.type ?? ''
  if (/\/problems\/(access-request-|spotify-invite-only)/.test(type) && error.problem?.detail) {
    return error.problem.detail
  }

  if (error.status === 401) return 'That email and password don’t match.'
  if (error.status === 423) return 'Too many attempts. This account is locked for a few minutes.'
  if (error.status === 429) return 'You’re going a little fast. Try again in a moment.'
  if (error.status === 503) return 'The server is busy right now. Try again in a few seconds.'

  return error.problem?.title ?? error.message ?? fallback
}
