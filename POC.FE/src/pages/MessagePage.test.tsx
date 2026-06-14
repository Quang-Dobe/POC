import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { StreamHandlers } from '@/api/stream';

vi.mock('@/api/client', () => ({
  fetchMessage: vi.fn(),
}));
vi.mock('@/api/stream', () => ({
  openAskStream: vi.fn(),
  postExtendSession: vi.fn(),
}));
const { mockLogout, mockRecheck, mockMe } = vi.hoisted(() => ({
  mockLogout: vi.fn(),
  mockRecheck: vi.fn(),
  mockMe: { value: null as { displayName: string; roles: string[] } | null },
}));
vi.mock('@/auth/AuthContext', () => ({
  useAuth: () => ({ logout: mockLogout, recheck: mockRecheck, me: mockMe.value }),
}));

import { fetchMessage } from '@/api/client';
import { openAskStream, postExtendSession } from '@/api/stream';
import MessagePage from '@/pages/MessagePage';

function renderPage(): void {
  render(
    <MemoryRouter>
      <MessagePage />
    </MemoryRouter>,
  );
}

const mockFetchMessage = vi.mocked(fetchMessage);
const mockOpenAskStream = vi.mocked(openAskStream);
const mockPostExtendSession = vi.mocked(postExtendSession);

beforeEach(() => {
  mockFetchMessage.mockResolvedValue('seeded');
  mockPostExtendSession.mockResolvedValue(true);
  mockMe.value = null;
});

afterEach(() => {
  vi.clearAllMocks();
});

function typeAndAsk(value: string): void {
  fireEvent.change(screen.getByLabelText('Ask the agent'), { target: { value } });
  fireEvent.click(screen.getByRole('button', { name: 'Ask' }));
}

describe('MessagePage', () => {
  it('renders the string returned by fetchMessage', async () => {
    mockFetchMessage.mockResolvedValue('the seeded secret string');
    renderPage();

    expect(await screen.findByTestId('message')).toHaveTextContent('the seeded secret string');
  });

  it('renders an error state when fetchMessage rejects', async () => {
    mockFetchMessage.mockRejectedValue(new Error('boom'));
    renderPage();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByTestId('message')).not.toBeInTheDocument();
  });

  it('shows the invite link for a manager', async () => {
    mockMe.value = { displayName: 'Mona Manager', roles: ['manager'] };
    renderPage();

    const link = await screen.findByRole('link', { name: 'Invite a new user' });
    expect(link).toHaveAttribute('href', '/invite');
  });

  it('hides the invite link for a non-manager', async () => {
    mockMe.value = { displayName: 'Rita Reader', roles: ['reader'] };
    renderPage();

    await screen.findByTestId('message');
    expect(screen.queryByRole('link', { name: 'Invite a new user' })).not.toBeInTheDocument();
  });

  describe('streamed ask flow', () => {
    it('renders incremental streamed chunks accumulated into the answer', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onMessage('Hello ');
        handlers.onMessage('world');
      });
      renderPage();

      typeAndAsk('hi?');

      expect(await screen.findByTestId('answer')).toHaveTextContent('Hello world');
      expect(mockOpenAskStream).toHaveBeenCalledTimes(1);
      expect(mockOpenAskStream.mock.calls[0][0]).toBe('hi?');
    });

    it('finalizes the rendered answer on EOF (stream resolves -> success)', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onMessage('done');
      });
      renderPage();

      typeAndAsk('q');

      expect(await screen.findByTestId('answer')).toHaveTextContent('done');
      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Ask' })).not.toBeDisabled();
      });
    });

    it('posts to /auth/extend-session on a control:extend-session event mid-stream', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onMessage('chunk');
        handlers.onControl('extend-session');
      });
      renderPage();

      typeAndAsk('long question');

      await screen.findByTestId('answer');
      expect(mockPostExtendSession).toHaveBeenCalledTimes(1);
    });

    it('ignores a non-extend control name (does not post)', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onControl('something-else');
      });
      renderPage();

      typeAndAsk('q');

      await waitFor(() => {
        expect(mockOpenAskStream).toHaveBeenCalled();
      });
      expect(mockPostExtendSession).not.toHaveBeenCalled();
    });

    it('shows an error when the pre-commit open rejects', async () => {
      mockOpenAskStream.mockRejectedValue(new Error('boom'));
      renderPage();

      typeAndAsk('q');

      expect(
        await screen.findByText('Could not get an answer. Please try again.'),
      ).toBeInTheDocument();
      expect(screen.queryByTestId('answer')).not.toBeInTheDocument();
    });

    it('disables the Ask button when the input is empty', () => {
      renderPage();

      expect(screen.getByRole('button', { name: 'Ask' })).toBeDisabled();
      expect(mockOpenAskStream).not.toHaveBeenCalled();
    });
  });
});
