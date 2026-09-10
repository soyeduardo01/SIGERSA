import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AllItemsAdmin } from './AllItemsAdmin'

const apiMocks = vi.hoisted(() => ({
  createAllItem: vi.fn(),
  deleteAllItem: vi.fn(),
  getAllItems: vi.fn(),
  reorderAllItems: vi.fn(),
  updateAllItem: vi.fn(),
}))

vi.mock('../../lib/api', () => apiMocks)
vi.mock('../../lib/alerts', () => ({
  alerts: {
    confirm: vi.fn().mockResolvedValue(true),
    success: vi.fn().mockResolvedValue(undefined),
    error: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('AllItemsAdmin', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiMocks.getAllItems.mockResolvedValue([
      { items: 1, itemsId: '1', description: 'Categoría', sectionType: 'C', parents: null },
      { items: 2, itemsId: '1.1', description: 'Pregunta', sectionType: 'I', parents: '1' },
    ])
    apiMocks.deleteAllItem.mockResolvedValue(undefined)
  })

  it('requires an explicit strategy before deleting a parent', async () => {
    render(<AllItemsAdmin />)
    await screen.findByText('Categoría')

    fireEvent.click(screen.getAllByRole('button', { name: 'Remover' })[0])

    expect(screen.getByRole('dialog', { name: 'El nodo tiene hijos' })).toBeInTheDocument()
    expect(apiMocks.deleteAllItem).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Reubicar hijos y remover' }))

    await waitFor(() =>
      expect(apiMocks.deleteAllItem).toHaveBeenCalledWith(1, 'REPARENT', undefined),
    )
  })

  it('muestra el nombre completo del tipo almacenado como abreviatura', async () => {
    render(<AllItemsAdmin />)
    expect((await screen.findAllByText('Capítulo', { selector: 'td' }))[0]).toBeVisible()
    expect(screen.getAllByText('Pregunta', { selector: 'td' })[0]).toBeVisible()
  })
})
