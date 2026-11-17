import { describe, expect, it } from 'vitest'
import { mediaOperationOf } from '~/composables/useMediaOperations'

/**
 * Reading a media operation out of an entry's metadata. The metadata is free text as far as the
 * auditing library is concerned, so everything that is not this application's convention has to
 * come back as "not a media entry" rather than throw or guess.
 */
describe('mediaOperationOf', () => {
  it('reads each operation the API writes', () => {
    for (const operation of ['Attach', 'Detach', 'Replace', 'Reorder'] as const) {
      expect(mediaOperationOf({ metadata: JSON.stringify({ media: operation, fileId: 'f' }) }))
        .toEqual({ operation, fileDeleted: false })
    }
  })

  it('says when a detach came from the file being deleted', () => {
    expect(mediaOperationOf({ metadata: '{"media":"Detach","fileId":"f","reason":"FileDeleted"}' }))
      .toEqual({ operation: 'Detach', fileDeleted: true })
  })

  it.each([
    ['no metadata', null],
    ['empty metadata', ''],
    ['metadata that is not JSON', 'ticket 42'],
    ['JSON that is not an object', '"Attach"'],
    ['an object without media', '{"tenant":"acme"}'],
    ['an operation it does not know', '{"media":"Rename"}'],
    ['an operation in the wrong case', '{"media":"attach"}']
  ])('is null for %s', (_, metadata) => {
    expect(mediaOperationOf({ metadata })).toBeNull()
  })
})
