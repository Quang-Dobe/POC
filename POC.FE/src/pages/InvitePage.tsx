import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { createInvite, type InviteResult } from '@/api/client';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

type InviteState =
  | { readonly status: 'idle' }
  | { readonly status: 'submitting' }
  | { readonly status: 'success'; readonly result: InviteResult }
  | { readonly status: 'error' };

const ManagerRole = 'manager';

function InvitePage(): JSX.Element {
  const { me, recheck } = useAuth();
  const [email, setEmail] = useState('');
  const [state, setState] = useState<InviteState>({ status: 'idle' });

  const isManager = me?.roles.includes(ManagerRole) ?? false;

  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const trimmed = email.trim();
    if (trimmed === '' || state.status === 'submitting') {
      return;
    }

    setState({ status: 'submitting' });

    void createInvite(trimmed)
      .then((result) => {
        setState({ status: 'success', result });
      })
      .catch((error: unknown) => {
        if (error instanceof Error && error.name === 'UnauthorizedError') {
          void recheck();
        }
        setState({ status: 'error' });
      });
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-4 text-foreground">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Invite a new user</CardTitle>
          <CardDescription>
            Create a new user from their email. A default password is generated automatically.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {!isManager && (
            <p role="alert">You do not have permission to invite users.</p>
          )}

          {isManager && (
            <form onSubmit={handleSubmit} className="space-y-2">
              <label htmlFor="invite-email" className="text-sm font-medium">
                New user email
              </label>
              <input
                id="invite-email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="new.user@example.com"
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              <Button
                type="submit"
                className="w-full"
                disabled={state.status === 'submitting' || email.trim() === ''}
              >
                {state.status === 'submitting' ? 'Inviting...' : 'Invite user'}
              </Button>
            </form>
          )}

          {state.status === 'success' && (
            <div data-testid="invite-success" className="space-y-2 rounded-md bg-muted p-3 text-sm">
              {state.result.alreadyExisted ? (
                <p data-testid="invite-already-existed">
                  A user with <span className="font-medium">{state.result.subject}</span> already
                  existed. No new password was generated.
                </p>
              ) : (
                <>
                  <p>
                    Invited <span className="font-medium">{state.result.subject}</span>.
                  </p>
                  <p>
                    Temporary password:{' '}
                    <code data-testid="generated-password" className="font-mono">
                      {state.result.generatedPassword}
                    </code>
                  </p>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      void navigator.clipboard?.writeText(state.result.generatedPassword)
                    }
                  >
                    Copy password
                  </Button>
                  <p className="text-xs text-muted-foreground">
                    Shown once. Share it securely; the user must change it on first sign-in.
                  </p>
                </>
              )}
            </div>
          )}

          {state.status === 'error' && (
            <p role="alert">Could not invite the user. Please try again.</p>
          )}

          <Button asChild variant="ghost" className="w-full">
            <Link to="/message">Back to message</Link>
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

export default InvitePage;
