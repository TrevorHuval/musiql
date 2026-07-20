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

  if (error.status === 401) return 'That email and password don’t match.'
  if (error.status === 423) return 'Too many attempts. This account is locked for a few minutes.'
  if (error.status === 429) return 'You’re going a little fast. Try again in a moment.'

  return error.problem?.title ?? error.message ?? fallback
}
