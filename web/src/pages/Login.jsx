import { useEffect, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { ArrowRight, Eye, EyeOff, Feather, LoaderCircle } from 'lucide-react';
import Mates from '../components/Mates';
import { useAuth } from '../lib/auth';
import './login.css';

// seeded SDR accounts, offered as one-click fills in development only
const DEMO_ACCOUNTS = ['maya.brennan@quillbridge.com', 'jonas.weber@quillbridge.com', 'elif.demir@quillbridge.com'];
const DEMO_PASSWORD = 'Test1234';

const clamp = (value) => Math.max(-1, Math.min(1, value));

export default function Login() {
  const { login, enter } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(true);
  const [shown, setShown] = useState(false);
  const [focus, setFocus] = useState(null);
  const [caret, setCaret] = useState(0);
  const [status, setStatus] = useState('idle');
  const [error, setError] = useState('');
  const [pointer, setPointer] = useState({ x: 0, y: 0 });

  const matesRef = useRef(null);

  // when nobody is typing, their eyes follow the pointer around the page
  useEffect(() => {
    const onMove = (event) => {
      const box = matesRef.current?.getBoundingClientRect();
      if (!box) return;

      setPointer({
        x: clamp((event.clientX - (box.left + box.width / 2)) / 420),
        y: clamp((event.clientY - (box.top + box.height / 2)) / 320)
      });
    };

    window.addEventListener('pointermove', onMove);
    return () => window.removeEventListener('pointermove', onMove);
  }, []);

  // one derived word is the state of all four of them
  function moodOf() {
    if (status === 'success') return 'happy';
    if (status === 'error') return 'error';
    // the secret is on screen; the caret doesn't matter
    if (shown && password) return 'exposed';
    if (focus === 'password') return 'shy';
    if (focus === 'email') return 'nosy';
    return 'idle';
  }

  const mood = moodOf();

  // while typing the email they read along: eyes slide with the caret
  const look =
    mood === 'nosy'
      ? { x: 0.35 + Math.min(caret, 32) / 32 * 0.65, y: 0.35 }
      : mood === 'exposed'
        ? { x: 1, y: 0.55 }
        : pointer;

  const trackCaret = (event) => setCaret(event.target.selectionStart ?? event.target.value.length);

  async function submit(event) {
    event.preventDefault();
    if (status === 'loading' || status === 'success') return;

    setStatus('loading');
    setError('');
    setShown(false);

    try {
      await login(email.trim(), password, remember);
      setStatus('success');
      // let them celebrate, then step into the app
      setTimeout(enter, 900);
    } catch (err) {
      setStatus('error');
      setError(err.status === 401 || err.status === 400 ? 'That email and password don’t match.' : err.message);
      setTimeout(() => setStatus((s) => (s === 'error' ? 'idle' : s)), 1400);
    }
  }

  function fillDemo(account) {
    setEmail(account);
    setPassword(DEMO_PASSWORD);
    setError('');
  }

  return (
    <main className="login">
      <div className="login__aurora" aria-hidden="true">
        <span />
        <span />
        <span />
      </div>

      <motion.header
        className="login__intro"
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.6, ease: [0.22, 1, 0.36, 1] }}
      >
        <span className="eyebrow">Quillbridge · SDR workspace</span>
        <h1>Every call, already written.</h1>
      </motion.header>

      <motion.section
        className="login__card"
        initial={{ opacity: 0, y: 28, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1], delay: 0.05 }}
      >
        <div className="login__stage" ref={matesRef}>
          <div className="login__stage-grid" aria-hidden="true" />
          <Mates mood={mood} look={look} />
        </div>

        <form className="login__form" onSubmit={submit} noValidate>
          <motion.div
            className="login__mark"
            initial={{ rotate: -30, scale: 0.6, opacity: 0 }}
            animate={{ rotate: 0, scale: 1, opacity: 1 }}
            transition={{ type: 'spring', stiffness: 260, damping: 16, delay: 0.35 }}
          >
            <Feather />
          </motion.div>

          <h2>Welcome back</h2>
          <p className="login__sub">Please enter your details.</p>

          <label className="line-field">
            <span>Email</span>
            <input
              type="email"
              name="email"
              autoComplete="username"
              placeholder="name@quillbridge.com"
              value={email}
              required
              onChange={(e) => {
                setEmail(e.target.value);
                trackCaret(e);
              }}
              onKeyUp={trackCaret}
              onClick={trackCaret}
              onFocus={(e) => {
                setFocus('email');
                trackCaret(e);
              }}
              onBlur={() => setFocus(null)}
            />
          </label>

          <label className="line-field">
            <span>Password</span>
            <input
              type={shown ? 'text' : 'password'}
              name="password"
              autoComplete="current-password"
              placeholder="••••••••"
              value={password}
              required
              onChange={(e) => setPassword(e.target.value)}
              onFocus={() => setFocus('password')}
              onBlur={() => setFocus(null)}
            />
            <button
              type="button"
              className="line-field__eye"
              onClick={() => setShown((s) => !s)}
              aria-label={shown ? 'Hide password' : 'Show password'}
              aria-pressed={shown}
            >
              {shown ? <EyeOff /> : <Eye />}
            </button>
          </label>

          <div className="login__row">
            <label className="check">
              <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
              <span className="check__box" aria-hidden="true">
                <svg viewBox="0 0 12 12">
                  <path d="M2.5 6.2 5 8.5 9.5 3.5" />
                </svg>
              </span>
              Keep me signed in
            </label>
          </div>

          <div className="login__error" role="alert" aria-live="polite">
            {error && (
              <motion.p key={error + status} initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }}>
                {error}
              </motion.p>
            )}
          </div>

          <button className="btn btn--primary login__submit" disabled={!email || !password || status === 'loading'}>
            {status === 'loading' ? (
              <LoaderCircle className="spin" />
            ) : status === 'success' ? (
              'Welcome in'
            ) : (
              <>
                Log in <ArrowRight />
              </>
            )}
          </button>

          <p className="login__foot">Accounts are created by your Quillbridge admin.</p>
        </form>
      </motion.section>

      <motion.footer
        className="login__caption"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.6 }}
      >
        <p>Type your password. Watch them look away.</p>
        {import.meta.env.DEV && (
          <div className="login__demo">
            {DEMO_ACCOUNTS.map((account) => (
              <button key={account} type="button" onClick={() => fillDemo(account)}>
                {account}
              </button>
            ))}
            <span>{DEMO_PASSWORD}</span>
          </div>
        )}
      </motion.footer>
    </main>
  );
}
