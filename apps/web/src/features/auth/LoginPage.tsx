import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { Field } from '../../components/Field';
import { useAuth } from '../../auth/AuthContext';

interface LoginLocationState { from?: string }

export function LoginPage() {
  const { agent, isLoading, login } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const requestedPath = (location.state as LoginLocationState | null)?.from;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [pending, setPending] = useState(false);
  const [validation, setValidation] = useState<string>();
  const [showPassword, setShowPassword] = useState(false);

  if (isLoading) return <main className="app__main" aria-busy="true">Checking session…</main>;
  if (agent) return <Navigate to={requestedPath?.startsWith('/') ? requestedPath : '/tickets'} replace />;

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    if (!email.trim() || !/^\S+@\S+\.\S+$/.test(email.trim()) || !password) {
      setValidation('Enter a valid email address and password.');
      return;
    }
    setValidation(undefined);
    setPending(true);
    try {
      await login(email.trim(), password);
      navigate(requestedPath?.startsWith('/') ? requestedPath : '/tickets', { replace: true });
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 401
        ? 'Email or password is incorrect.'
        : caught instanceof Error ? caught.message : 'Could not sign in. Try again.');
    } finally {
      setPending(false);
    }
  };

  return <main className="app__main auth-page">
    <div className="auth-shell">
      <section className="auth-showcase" aria-label="Northwind Support Desk overview">
        <div className="auth-showcase__topline">
          <span className="auth-brand-mark" aria-hidden="true">
            <svg viewBox="0 0 32 32" fill="none">
              <path d="M16 3.5 27.5 10v12L16 28.5 4.5 22V10L16 3.5Z" stroke="currentColor" strokeWidth="1.8" />
              <path d="M10 19.5 14.2 15l3.2 3 5-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              <circle cx="22.5" cy="12" r="1.5" fill="currentColor" />
            </svg>
          </span>
          <span className="auth-showcase__brand">Northwind <strong>Support</strong></span>
        </div>

        <div className="auth-showcase__copy">
          <p className="auth-eyebrow"><span /> SUPPORT WORKSPACE</p>
          <h1>Every customer issue.<br /><em>One clear view.</em></h1>
          <p className="auth-showcase__description">
            Keep the team in sync, make priorities visible, and move every request forward.
          </p>
        </div>

        <div className="auth-showcase__visual" aria-hidden="true">
          <div className="auth-orbit auth-orbit--outer" />
          <div className="auth-orbit auth-orbit--inner" />
          <div className="auth-visual-card">
            <span className="auth-visual-card__icon">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M5 5.75h14A1.75 1.75 0 0 1 20.75 7.5v9A1.75 1.75 0 0 1 19 18.25H5A1.75 1.75 0 0 1 3.25 16.5v-9A1.75 1.75 0 0 1 5 5.75Z" stroke="currentColor" strokeWidth="1.5" />
                <path d="M7 9h6M7 12h10M7 15h7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
            </span>
            <span className="auth-visual-card__text"><strong>Team workspace</strong><small>Tickets, customers, and SLAs</small></span>
            <span className="auth-visual-card__status">READY</span>
          </div>
          <div className="auth-visual-dot auth-visual-dot--one" />
          <div className="auth-visual-dot auth-visual-dot--two" />
        </div>

        <div className="auth-showcase__footer">
          <span className="auth-live-dot" /> Secure agent access
          <span className="auth-showcase__footer-note">Built for the people who help</span>
        </div>
      </section>

      <section className="auth-panel" aria-labelledby="login-title">
        <div className="auth-panel__content">
          <div className="auth-panel__mobile-brand">
            <span className="auth-brand-mark" aria-hidden="true">
              <svg viewBox="0 0 32 32" fill="none">
                <path d="M16 3.5 27.5 10v12L16 28.5 4.5 22V10L16 3.5Z" stroke="currentColor" strokeWidth="1.8" />
                <path d="M10 19.5 14.2 15l3.2 3 5-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <circle cx="22.5" cy="12" r="1.5" fill="currentColor" />
              </svg>
            </span>
            <span>Northwind <strong>Support</strong></span>
          </div>
          <p className="auth-panel__eyebrow">AGENT PORTAL</p>
          <h2 id="login-title">Welcome back</h2>
          <p className="auth-panel__subtitle">Sign in to pick up where your team left off.</p>

          <form className="auth-form" onSubmit={submit} noValidate>
            <Field id="login-email" label="Work email" hint="Use the email linked to your agent account.">
              {(props) => <div className="auth-input-wrap">
                <svg className="auth-input-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <rect x="3.5" y="5.5" width="17" height="13" rx="2" stroke="currentColor" strokeWidth="1.5" />
                  <path d="m4.5 7 7.5 5.5L19.5 7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <input {...props} className="auth-input" type="email" autoComplete="username" value={email}
                  aria-invalid={Boolean(validation && (!email.trim() || !/^\S+@\S+\.\S+$/.test(email.trim())))}
                  onChange={(event) => { setEmail(event.target.value); setValidation(undefined); setError(undefined); }} />
              </div>}
            </Field>
            <Field id="login-password" label="Password">
              {(props) => <div className="auth-input-wrap">
                <svg className="auth-input-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <rect x="4.5" y="10" width="15" height="10.5" rx="2" stroke="currentColor" strokeWidth="1.5" />
                  <path d="M8 10V7.5a4 4 0 0 1 8 0V10M12 14v2.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                </svg>
                <input {...props} className="auth-input auth-input--password" type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password" value={password}
                  aria-invalid={Boolean(validation && !password)}
                  onChange={(event) => { setPassword(event.target.value); setValidation(undefined); setError(undefined); }} />
                <button className="auth-password-toggle" type="button" aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword} onClick={() => setShowPassword((visible) => !visible)}>
                  {showPassword ? 'Hide' : 'Show'}
                </button>
              </div>}
            </Field>

            {validation && <p className="auth-alert auth-alert--validation" role="alert">{validation}</p>}
            {error && <p className="auth-alert" role="alert">{error}</p>}

            <button className="button button--primary auth-submit" type="submit" disabled={pending}>
              {pending ? <><span className="auth-spinner" aria-hidden="true" /> Signing you in</> : <>Sign in <span aria-hidden="true">→</span></>}
            </button>
          </form>

          <div className="auth-panel__security">
            <svg viewBox="0 0 20 20" fill="none" aria-hidden="true">
              <path d="M10 2.5 16 5v4.2c0 4.1-2.6 6.8-6 8.3-3.4-1.5-6-4.2-6-8.3V5l6-2.5Z" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" />
              <path d="m7.5 9.8 1.7 1.7 3.5-3.7" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            Your workspace is protected. Sign in with your agent account.
          </div>
        </div>
        <p className="auth-panel__copyright">NORTHWIND SUPPORT DESK <span>•</span> AGENT ACCESS</p>
      </section>
    </div>
  </main>;
}
