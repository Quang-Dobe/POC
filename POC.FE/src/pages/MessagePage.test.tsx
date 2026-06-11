import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { StreamHandlers } from '@/api/stream';

vi.mock('@/api/client', () => ({
  fetchMessage: vi.fn(),
}));
vi.mock('@/api/stream', () => ({
  openAskStream: vi.fn(),
  postExtendSession: vi.fn(),
}));
const { mockLogout, mockRecheck } = vi.hoisted(() => ({
  mockLogout: vi.fn(),
  mockRecheck: vi.fn(),
}));
vi.mock('@/auth/AuthContext', () => ({
  useAuth: () => ({ logout: mockLogout, recheck: mockRecheck }),
}));

import { fetchMessage } from '@/api/client';
import { openAskStream, postExtendSession } from '@/api/stream';
import MessagePage from '@/pages/MessagePage';

const mockFetchMessage = vi.mocked(fetchMessage);
const mockOpenAskStream = vi.mocked(openAskStream);
const mockPostExtendSession = vi.mocked(postExtendSession);

beforeEach(() => {
  mockFetchMessage.mockResolvedValue('seeded');
  mockPostExtendSession.mockResolvedValue(true);
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
    render(<MessagePage />);

    expect(await screen.findByTestId('message')).toHaveTextContent('the seeded secret string');
  });

  it('renders an error state when fetchMessage rejects', async () => {
    mockFetchMessage.mockRejectedValue(new Error('boom'));
    render(<MessagePage />);

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByTestId('message')).not.toBeInTheDocument();
  });

  describe('streamed ask flow', () => {
    it('renders incremental streamed chunks accumulated into the answer', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onMessage('Hello ');
        handlers.onMessage('world');
      });
      render(<MessagePage />);

      typeAndAsk('hi?');

      expect(await screen.findByTestId('answer')).toHaveTextContent('Hello world');
      expect(mockOpenAskStream).toHaveBeenCalledTimes(1);
      expect(mockOpenAskStream.mock.calls[0][0]).toBe('hi?');
    });

    it('finalizes the rendered answer on EOF (stream resolves -> success)', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onMessage('done');
      });
      render(<MessagePage />);

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
      render(<MessagePage />);

      typeAndAsk('long question');

      await screen.findByTestId('answer');
      expect(mockPostExtendSession).toHaveBeenCalledTimes(1);
    });

    it('ignores a non-extend control name (does not post)', async () => {
      mockOpenAskStream.mockImplementation(async (_question, handlers: StreamHandlers) => {
        handlers.onControl('something-else');
      });
      render(<MessagePage />);

      typeAndAsk('q');

      await waitFor(() => {
        expect(mockOpenAskStream).toHaveBeenCalled();
      });
      expect(mockPostExtendSession).not.toHaveBeenCalled();
    });

    it('shows an error when the pre-commit open rejects', async () => {
      mockOpenAskStream.mockRejectedValue(new Error('boom'));
      render(<MessagePage />);

      typeAndAsk('q');

      expect(
        await screen.findByText('Could not get an answer. Please try again.'),
      ).toBeInTheDocument();
      expect(screen.queryByTestId('answer')).not.toBeInTheDocument();
    });

    it('disables the Ask button when the input is empty', () => {
      render(<MessagePage />);

      expect(screen.getByRole('button', { name: 'Ask' })).toBeDisabled();
      expect(mockOpenAskStream).not.toHaveBeenCalled();
    });
  });
});
