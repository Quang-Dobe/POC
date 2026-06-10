import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/api/client', () => ({
  fetchMessage: vi.fn(),
  askAgent: vi.fn(),
}));
vi.mock('@/auth/AuthContext', () => ({
  useAuth: vi.fn(() => ({ logout: vi.fn() })),
}));

import { askAgent, fetchMessage } from '@/api/client';
import MessagePage from '@/pages/MessagePage';

const mockFetchMessage = vi.mocked(fetchMessage);
const mockAskAgent = vi.mocked(askAgent);

beforeEach(() => {
  mockFetchMessage.mockResolvedValue('seeded');
});

afterEach(() => {
  vi.clearAllMocks();
});

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

  describe('ask flow', () => {
    it('shows the answer below after a successful ask', async () => {
      mockAskAgent.mockResolvedValue({ answer: 'the agent answer', sessionId: 's1' });
      render(<MessagePage />);

      fireEvent.change(screen.getByLabelText('Ask the agent'), { target: { value: 'hello?' } });
      fireEvent.click(screen.getByRole('button', { name: 'Ask' }));

      expect(await screen.findByTestId('answer')).toHaveTextContent('the agent answer');
      expect(mockAskAgent).toHaveBeenCalledWith('hello?', null);
    });

    it('reuses the returned session_id on the next ask', async () => {
      mockAskAgent
        .mockResolvedValueOnce({ answer: 'a1', sessionId: 'srv-session' })
        .mockResolvedValueOnce({ answer: 'a2', sessionId: 'srv-session' });
      render(<MessagePage />);

      const input = screen.getByLabelText('Ask the agent');
      const button = screen.getByRole('button', { name: 'Ask' });

      fireEvent.change(input, { target: { value: 'first' } });
      fireEvent.click(button);
      expect(await screen.findByTestId('answer')).toHaveTextContent('a1');

      fireEvent.change(input, { target: { value: 'second' } });
      fireEvent.click(button);
      expect(await screen.findByTestId('answer')).toHaveTextContent('a2');

      expect(mockAskAgent).toHaveBeenNthCalledWith(1, 'first', null);
      expect(mockAskAgent).toHaveBeenNthCalledWith(2, 'second', 'srv-session');
    });

    it('shows an error when askAgent rejects', async () => {
      mockAskAgent.mockRejectedValue(new Error('boom'));
      render(<MessagePage />);

      fireEvent.change(screen.getByLabelText('Ask the agent'), { target: { value: 'hello?' } });
      fireEvent.click(screen.getByRole('button', { name: 'Ask' }));

      expect(await screen.findByText('Could not get an answer. Please try again.')).toBeInTheDocument();
      expect(screen.queryByTestId('answer')).not.toBeInTheDocument();
    });

    it('disables the Ask button when the input is empty', () => {
      render(<MessagePage />);

      expect(screen.getByRole('button', { name: 'Ask' })).toBeDisabled();
      expect(mockAskAgent).not.toHaveBeenCalled();
    });
  });
});
