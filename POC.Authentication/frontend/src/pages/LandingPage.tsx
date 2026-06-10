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
  const { login } = useAuth();

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-4 text-foreground">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>POC Authentication</CardTitle>
          <CardDescription>Sign in to view the protected message.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button className="w-full" onClick={() => void login()}>
            Sign in
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

export default LandingPage;
