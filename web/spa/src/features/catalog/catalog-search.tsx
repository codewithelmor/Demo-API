import { useEffect, useRef, useState } from 'react'
import { FormField } from '@/components/form-field'
import { Input } from '@/components/ui/input'
import { SEARCH_MAX, SEARCH_MIN } from '@/features/catalog/catalog-params'

const DEBOUNCE_MS = 300

interface CatalogSearchProps {
  // The search in the URL.
  value: string
  // undefined = no search. Never called with fewer than 2 characters.
  onSearch: (search: string | undefined) => void
}

// Search by name or SKU. The URL (and so the request) updates 300 ms after the last keystroke.
// One character only shows a hint: nothing is sent (US-F026 AC2).
export function CatalogSearch({ value, onSearch }: CatalogSearchProps) {
  const [text, setText] = useState(value)
  // The last value this box put into the URL, and the URL value it last saw.
  const [committed, setCommitted] = useState(value)
  const [seen, setSeen] = useState(value)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  // The URL changed by something else (Clear filters, Back): show it. A change this box made
  // itself must not overwrite what the user has typed since.
  if (value !== seen) {
    setSeen(value)
    if (value !== committed) {
      setCommitted(value)
      setText(value)
    }
  }

  useEffect(
    () => () => {
      clearTimeout(timer.current)
    },
    [],
  )

  function change(next: string) {
    setText(next)
    clearTimeout(timer.current)
    const trimmed = next.trim()
    if (trimmed.length === 1) return
    timer.current = setTimeout(() => {
      setCommitted(trimmed)
      onSearch(trimmed === '' ? undefined : trimmed)
    }, DEBOUNCE_MS)
  }

  const tooShort = text.trim().length > 0 && text.trim().length < SEARCH_MIN

  return (
    <FormField
      label="Search name or SKU"
      hint={tooShort ? 'Type at least 2 characters' : undefined}
      render={(a11y) => (
        <Input
          type="search"
          autoComplete="off"
          maxLength={SEARCH_MAX}
          {...a11y}
          value={text}
          onChange={(event) => {
            change(event.target.value)
          }}
        />
      )}
    />
  )
}
