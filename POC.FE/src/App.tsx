import { Route, Routes } from 'react-router-dom';
import LandingPage from '@/pages/LandingPage';
import MessagePage from '@/pages/MessagePage';
import RequireAuth from '@/components/RequireAuth';

function App(): JSX.Element {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />
      <Route
        path="/message"
        element={
          <RequireAuth>
            <MessagePage />
          </RequireAuth>
        }
      />
    </Routes>
  );
}

export default App;
