import { useEffect } from 'react';
import { BrowserRouter, Route, Routes, useLocation } from 'react-router';
import { JsonLd } from './components/JsonLd.tsx';
import { SiteHeader } from './components/SiteHeader.tsx';
import { SkipLink } from './components/SkipLink.tsx';
import { HomePage } from './pages/HomePage.tsx';
import { NotFoundPage } from './pages/NotFoundPage.tsx';
import { ProjectPage } from './pages/ProjectPage.tsx';
import { ThemeProvider } from './theme/ThemeProvider.tsx';

function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [pathname]);
  return null;
}

/** Layout plus routes. Tests render this inside their own router. */
export function AppShell() {
  return (
    <>
      <SkipLink />
      <JsonLd />
      <div className="mx-auto min-h-svh w-full max-w-[76rem] lg:grid lg:grid-cols-[minmax(18rem,26rem)_minmax(0,1fr)]">
        <SiteHeader />
        <div className="min-w-0 px-5 pt-8 pb-16 sm:px-8 lg:px-16 lg:pt-20 lg:pb-24">
          <div className="mx-auto w-full max-w-[40rem]">
            <ScrollToTop />
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/projects/:slug" element={<ProjectPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </div>
        </div>
      </div>
    </>
  );
}

export function App() {
  return (
    <ThemeProvider>
      <BrowserRouter>
        <AppShell />
      </BrowserRouter>
    </ThemeProvider>
  );
}
