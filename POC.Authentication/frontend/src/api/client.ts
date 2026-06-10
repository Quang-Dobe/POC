import { config } from '@/config';
import { getAccessToken } from '@/auth/AuthContext';

export class NotAuthenticatedError extends Error {
  constructor(message = 'No valid access token; user is not authenticated.') {
    super(message);
    this.name = 'NotAuthenticatedError';
  }
}

export class ApiError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

interface MessageResponse {
  readonly message: string;
}

export interface AskResult {
  readonly answer: string;
  readonly sessionId: string;
}

function asAskResult(body: unknown): AskResult | null {
  if (
    typeof body === 'object' &&
    body !== null &&
    'answer' in body &&
    'session_id' in body &&
    typeof (body as { answer: unknown }).answer === 'string' &&
    typeof (body as { session_id: unknown }).session_id === 'string'
  ) {
    return {
      answer: (body as { answer: string }).answer,
      sessionId: (body as { session_id: string }).session_id,
    };
  }
  return null;
}

function asMessageResponse(body: unknown): MessageResponse | null {
  if (
    typeof body === 'object' &&
    body !== null &&
    'message' in body &&
    typeof (body as { message: unknown }).message === 'string'
  ) {
    return { message: (body as { message: string }).message };
  }
  return null;
}

export async function fetchMessage(): Promise<string> {
  const token = await getAccessToken();
  if (token === null) {
    throw new NotAuthenticatedError();
  }

  const response = await fetch(`${config.apiBaseUrl}/api/message`, {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new ApiError(`GET /api/message failed with status ${response.status}`);
  }

  const body: unknown = await response.json();
  const parsed = asMessageResponse(body);
  if (parsed === null) {
    throw new ApiError('GET /api/message returned an unexpected response shape');
  }

  return parsed.message;
}

export async function askAgent(
  question: string,
  sessionId: string | null,
): Promise<AskResult> {
  const token = await getAccessToken();
  if (token === null) {
    throw new NotAuthenticatedError();
  }

  const response = await fetch(`${config.apiBaseUrl}/api/ask`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ question, session_id: sessionId }),
  });

  if (!response.ok) {
    throw new ApiError(`POST /api/ask failed with status ${response.status}`);
  }

  const body: unknown = await response.json();
  const parsed = asAskResult(body);
  if (parsed === null) {
    throw new ApiError('POST /api/ask returned an unexpected response shape');
  }

  return parsed;
}
