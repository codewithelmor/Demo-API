import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SubmitRow } from '@/components/submit-row'

describe('SubmitRow', () => {
  it('keeps focus in the field when the button is pressed, so a blur error cannot move the button away', () => {
    render(<SubmitRow label="Create account" pendingLabel="Creating…" pending={false} />)

    // fireEvent returns false when preventDefault was called: the browser then does not move focus on mousedown.
    expect(fireEvent.mouseDown(screen.getByRole('button', { name: 'Create account' }))).toBe(false)
  })

  it('is disabled while pending and during a countdown', () => {
    const { rerender } = render(
      <SubmitRow label="Create account" pendingLabel="Creating…" pending />,
    )
    expect(screen.getByRole('button', { name: 'Creating…' })).toBeDisabled()

    rerender(
      <SubmitRow label="Create account" pendingLabel="Creating…" pending={false} secondsLeft={5} />,
    )
    expect(screen.getByRole('button', { name: 'Create account' })).toBeDisabled()
  })
})
