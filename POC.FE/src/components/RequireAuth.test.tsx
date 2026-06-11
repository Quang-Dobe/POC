import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/auth/AuthContext', () => ({
  useAuth: vi.fn(),
}));

import { useAuth, type AuthContextValue } from '@/auth/AuthContext';
import RequireAuth from '@/components/RequireAuth';

const mockUseAuth = vi.mocked(useAuth);

function setAuth(partial: Pick<AuthContextValue, 'isAuthenticated' | 'isLoading'>): void {
  mockUseAuth.mockReturnValue(partial as AuthContextValue);
}

function renderGuarded(): void {
  render(
    <MemoryRouter initialEntries={['/message']}>
      <Routes>
        <Route path="/" element={<div>Landing</div>} />
        <Route
          path="/message"
          element={
            <RequireAuth>
              <div>Protected content</div>
            </RequireAuth>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.clearAllMocks();
});

describe('RequireAuth', () => {
  it('renders the loading placeholder (no redirect, no children) while isLoading', () => {
    setAuth({ isAuthenticated: false, isLoading: true });
    renderGuarded();

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
    expect(screen.queryByText('Landing')).not.toBeInTheDocument();
  });

  it('redirects to the landing page when resolved and not authenticated', () => {
    setAuth({ isAuthenticated: false, isLoading: false });
    renderGuarded();

    expect(screen.getByText('Landing')).toBeInTheDocument();
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
  });

  it('renders children when resolved and authenticated', () => {
    setAuth({ isAuthenticated: true, isLoading: false });
    renderGuarded();

    expect(screen.getByText('Protected content')).toBeInTheDocument();
    expect(screen.queryByText('Landing')).not.toBeInTheDocument();
  });
});
