import { Navigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

function LandingPage(): JSX.Element {
  const { isAuthenticated, isLoading, login } = useAuth();
  const [searchParams] = useSearchParams();

  const signInDenied = searchParams.get('error') === 'access_denied';

  if (!isLoading && isAuthenticated) {
    return <Navigate to="/message" replace />;
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-4 text-foreground">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>POC.FE</CardTitle>
          <CardDescription>Sign in to view the protected message.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {signInDenied && (
            <p role="alert">Sign-in was denied. Please try again.</p>
          )}
          <Button className="w-full" onClick={() => login()}>
            Sign in
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

export default LandingPage;
