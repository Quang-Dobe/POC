import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/auth/AuthContext';

function CallbackPage(): JSX.Element {
  const { completeLogin } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    let active = true;
    void completeLogin()
      .then(() => {
        if (active) {
          navigate('/message', { replace: true });
        }
      })
      .catch(() => {
        if (active) {
          navigate('/', { replace: true });
        }
      });
    return () => {
      active = false;
    };
  }, [completeLogin, navigate]);

  return (
    <main className="flex min-h-screen items-center justify-center bg-background text-foreground">
      <p role="status">Completing sign-in...</p>
    </main>
  );
}

export default CallbackPage;
