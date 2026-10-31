import { NavLink, useLocation, useOutlet } from 'react-router';
import { AnimatePresence, motion } from 'motion/react';
import { ChartColumn, Feather, LogOut, Moon, PhoneCall, PhoneOff, Sun, Upload } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { useTheme } from '../lib/theme';
import { initials } from '../lib/format';
import './shell.css';

const NAV = [
  { to: '/calls', label: 'Call list', icon: PhoneCall },
  { to: '/do-not-call', label: 'Do not call', icon: PhoneOff },
  { to: '/import', label: 'Import', icon: Upload },
  { to: '/insights', label: 'Insights', icon: ChartColumn }
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const [theme, toggleTheme] = useTheme();
  const location = useLocation();
  const outlet = useOutlet();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="sidebar__brand">
          <span className="sidebar__logo">
            <Feather />
          </span>
          <span>Quillbridge</span>
        </div>

        <nav className="sidebar__nav" aria-label="Main">
          {NAV.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className="nav-item">
              {({ isActive }) => (
                <>
                  {isActive && (
                    <motion.span
                      layoutId="nav-pill"
                      className="nav-item__pill"
                      transition={{ type: 'spring', stiffness: 420, damping: 34 }}
                    />
                  )}
                  <Icon />
                  <span>{label}</span>
                </>
              )}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar__foot">
          <button className="btn btn--ghost btn--icon btn--sm" onClick={toggleTheme} aria-label="Toggle theme">
            {theme === 'dark' ? <Sun /> : <Moon />}
          </button>

          {user && (
            <div className="user-chip">
              <span className="avatar avatar--brand">{initials(user.firstName, user.lastName)}</span>
              <span className="user-chip__text">
                <strong>
                  {user.firstName} {user.lastName}
                </strong>
                <small>{user.region}</small>
              </span>
            </div>
          )}

          <button className="btn btn--ghost btn--icon btn--sm" onClick={logout} aria-label="Sign out" title="Sign out">
            <LogOut />
          </button>
        </div>
      </aside>

      <main className="shell__main">
        <AnimatePresence mode="wait" initial={false}>
          <motion.div
            key={location.pathname}
            className="page"
            initial={{ opacity: 0, y: 14, filter: 'blur(6px)' }}
            animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
            exit={{ opacity: 0, y: -8, filter: 'blur(4px)' }}
            transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
          >
            {outlet}
          </motion.div>
        </AnimatePresence>
      </main>
    </div>
  );
}
