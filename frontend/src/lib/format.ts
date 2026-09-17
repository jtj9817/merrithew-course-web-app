import type { Inquiry } from './types'

/** ISO-8601 UTC date part (`YYYY-MM-DD`) — slicing keeps the UTC instant's date. */
export function formatIsoDate(isoTimestamp: string): string {
  return isoTimestamp.slice(0, 10)
}

export function displayName(inquiry: Inquiry): string {
  return `${inquiry.firstName} ${inquiry.lastName}`
}
