import { config } from '@/config';

export class UnauthorizedError extends Error {
  constructor(message = 'The session is not authenticated (401); re-login is required.') {
    super(message);
    this.name = 'UnauthorizedError';
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
  const response = await fetch(`${config.apiBaseUrl}/api/message`, {
    method: 'GET',
    credentials: 'include',
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
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

export interface InviteResult {
  readonly subject: string;
  readonly alreadyExisted: boolean;
  readonly passwordSet: boolean;
}

function asInviteResult(body: unknown): InviteResult | null {
  if (
    typeof body === 'object' &&
    body !== null &&
    'subject' in body &&
    'alreadyExisted' in body &&
    'passwordSet' in body &&
    typeof (body as { subject: unknown }).subject === 'string' &&
    typeof (body as { alreadyExisted: unknown }).alreadyExisted === 'boolean' &&
    typeof (body as { passwordSet: unknown }).passwordSet === 'boolean'
  ) {
    return {
      subject: (body as { subject: string }).subject,
      alreadyExisted: (body as { alreadyExisted: boolean }).alreadyExisted,
      passwordSet: (body as { passwordSet: boolean }).passwordSet,
    };
  }
  return null;
}

export async function createInvite(email: string): Promise<InviteResult> {
  const response = await fetch(`${config.apiBaseUrl}/auth/invite`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ username: email }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new ApiError(`POST /auth/invite failed with status ${response.status}`);
  }

  const body: unknown = await response.json();
  const parsed = asInviteResult(body);
  if (parsed === null) {
    throw new ApiError('POST /auth/invite returned an unexpected response shape');
  }

  return parsed;
}

export async function askAgent(
  question: string,
  sessionId: string | null,
): Promise<AskResult> {
  const response = await fetch(`${config.apiBaseUrl}/api/ask`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ question, session_id: sessionId }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
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
