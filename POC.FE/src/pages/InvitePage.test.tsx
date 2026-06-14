import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/api/client', () => ({
  createInvite: vi.fn(),
}));
const { mockRecheck, mockMe } = vi.hoisted(() => ({
  mockRecheck: vi.fn(),
  mockMe: { value: null as { displayName: string; roles: string[] } | null },
}));
vi.mock('@/auth/AuthContext', () => ({
  useAuth: () => ({ recheck: mockRecheck, me: mockMe.value }),
}));

import { createInvite } from '@/api/client';
import InvitePage from '@/pages/InvitePage';

const mockCreateInvite = vi.mocked(createInvite);

function renderPage(): void {
  render(
    <MemoryRouter>
      <InvitePage />
    </MemoryRouter>,
  );
}

function submitEmail(value: string): void {
  fireEvent.change(screen.getByLabelText('New user email'), { target: { value } });
  fireEvent.click(screen.getByRole('button', { name: 'Invite user' }));
}

beforeEach(() => {
  mockMe.value = { displayName: 'Mona Manager', roles: ['manager'] };
});

afterEach(() => {
  vi.clearAllMocks();
});

describe('InvitePage', () => {
  it('blocks a non-manager from the invite form', () => {
    mockMe.value = { displayName: 'Rita Reader', roles: ['reader'] };
    renderPage();

    expect(screen.getByText('You do not have permission to invite users.')).toBeInTheDocument();
    expect(screen.queryByLabelText('New user email')).not.toBeInTheDocument();
  });

  it('lets a manager invite a user and confirms the email was sent', async () => {
    mockCreateInvite.mockResolvedValue({
      subject: 'erin@example.com',
      alreadyExisted: false,
      invitationSent: true,
    });
    renderPage();

    submitEmail('erin@example.com');

    expect(await screen.findByTestId('invite-sent')).toHaveTextContent('erin@example.com');
    expect(mockCreateInvite).toHaveBeenCalledWith('erin@example.com');
  });

  it('reports an already-existing user without sending an email', async () => {
    mockCreateInvite.mockResolvedValue({
      subject: 'erin@example.com',
      alreadyExisted: true,
      invitationSent: false,
    });
    renderPage();

    submitEmail('erin@example.com');

    expect(await screen.findByTestId('invite-already-existed')).toBeInTheDocument();
    expect(screen.queryByTestId('invite-sent')).not.toBeInTheDocument();
  });

  it('shows an error when the invite fails', async () => {
    mockCreateInvite.mockRejectedValue(new Error('boom'));
    renderPage();

    submitEmail('erin@example.com');

    expect(
      await screen.findByText('Could not invite the user. Please try again.'),
    ).toBeInTheDocument();
  });

  it('disables submit when the email is empty', () => {
    renderPage();

    expect(screen.getByRole('button', { name: 'Invite user' })).toBeDisabled();
    expect(mockCreateInvite).not.toHaveBeenCalled();
  });
});
