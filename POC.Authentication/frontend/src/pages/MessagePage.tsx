import { useEffect, useState, type FormEvent } from 'react';
import { askAgent, fetchMessage } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

type MessageState =
  | { readonly status: 'loading' }
  | { readonly status: 'success'; readonly message: string }
  | { readonly status: 'error' };

type AskState =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'success'; readonly answer: string }
  | { readonly status: 'error' };

function MessagePage(): JSX.Element {
  const { logout } = useAuth();
  const [state, setState] = useState<MessageState>({ status: 'loading' });
  const [question, setQuestion] = useState('');
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [ask, setAsk] = useState<AskState>({ status: 'idle' });

  function handleAsk(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const trimmed = question.trim();
    if (trimmed === '' || ask.status === 'loading') {
      return;
    }
    setAsk({ status: 'loading' });
    void askAgent(trimmed, sessionId)
      .then((result) => {
        setSessionId(result.sessionId);
        setAsk({ status: 'success', answer: result.answer });
      })
      .catch(() => {
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
      .catch(() => {
        if (active) {
          setState({ status: 'error' });
        }
      });
    return () => {
      active = false;
    };
  }, []);

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
              disabled={ask.status === 'loading' || question.trim() === ''}
            >
              {ask.status === 'loading' ? 'Asking...' : 'Ask'}
            </Button>
          </form>

          {ask.status === 'success' && (
            <p data-testid="answer" className="whitespace-pre-wrap rounded-md bg-muted p-3 text-sm">
              {ask.answer}
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
