import { useEffect, useRef, useState, type FormEvent } from 'react';
import { fetchMessage } from '@/api/client';
import { openAskStream, postExtendSession } from '@/api/stream';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

type MessageState =
  | { readonly status: 'loading' }
  | { readonly status: 'success'; readonly message: string }
  | { readonly status: 'error' };

type AskState =
  | { readonly status: 'idle' }
  | { readonly status: 'streaming'; readonly answer: string }
  | { readonly status: 'success'; readonly answer: string }
  | { readonly status: 'error' };

function MessagePage(): JSX.Element {
  const { logout, recheck } = useAuth();
  const [state, setState] = useState<MessageState>({ status: 'loading' });
  const [question, setQuestion] = useState('');
  const [ask, setAsk] = useState<AskState>({ status: 'idle' });
  const answerRef = useRef('');

  function handleAsk(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const trimmed = question.trim();
    if (trimmed === '' || ask.status === 'streaming') {
      return;
    }

    answerRef.current = '';
    setAsk({ status: 'streaming', answer: '' });

    void openAskStream(trimmed, {
      onMessage: (chunk) => {
        answerRef.current += chunk;
        setAsk({ status: 'streaming', answer: answerRef.current });
      },
      onControl: (controlName) => {
        if (controlName === 'extend-session') {
          void postExtendSession();
        }
      },
    })
      .then(() => {
        setAsk({ status: 'success', answer: answerRef.current });
      })
      .catch((error: unknown) => {
        if (error instanceof Error && error.name === 'UnauthorizedError') {
          void recheck();
        }
        setAsk({ status: 'error' });
      });
  }

  useEffect(() => {
    let active = true;
    void fetchMessage()
      .then((message) => {
        if (active) {
          setState({ status: 'success', message });
        }
      })
      .catch((error: unknown) => {
        if (error instanceof Error && error.name === 'UnauthorizedError') {
          void recheck();
        }
        if (active) {
          setState({ status: 'error' });
        }
      });
    return () => {
      active = false;
    };
  }, [recheck]);

  const renderedAnswer =
    ask.status === 'streaming' || ask.status === 'success' ? ask.answer : '';

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-4 text-foreground">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Protected message</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {state.status === 'loading' && <p role="status">Loading message...</p>}
          {state.status === 'success' && <p data-testid="message">{state.message}</p>}
          {state.status === 'error' && (
            <p role="alert">Could not load the message. Please try again.</p>
          )}

          <form onSubmit={handleAsk} className="space-y-2">
            <label htmlFor="ask-input" className="text-sm font-medium">
              Ask the agent
            </label>
            <input
              id="ask-input"
              type="text"
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              placeholder="Type your question..."
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
            <Button
              type="submit"
              className="w-full"
              disabled={ask.status === 'streaming' || question.trim() === ''}
            >
              {ask.status === 'streaming' ? 'Asking...' : 'Ask'}
            </Button>
          </form>

          {(ask.status === 'streaming' || ask.status === 'success') && (
            <p data-testid="answer" className="whitespace-pre-wrap rounded-md bg-muted p-3 text-sm">
              {renderedAnswer}
            </p>
          )}
          {ask.status === 'error' && (
            <p role="alert">Could not get an answer. Please try again.</p>
          )}

          <Button variant="outline" className="w-full" onClick={() => void logout()}>
            Sign out
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

export default MessagePage;
