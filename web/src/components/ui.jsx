import { useEffect, useId, useState } from 'react';
import { AnimatePresence, animate, motion, useMotionValue, useTransform } from 'motion/react';
import { Check, ChevronLeft, ChevronRight, Copy, RefreshCw, TriangleAlert } from 'lucide-react';
import { hueOf, initials } from '../lib/format';
import './ui.css';

export function PageHeader({ title, subtitle, children }) {
  return (
    <header className="page-header">
      <div>
        <h1>{title}</h1>
        {subtitle && <p>{subtitle}</p>}
      </div>
      {children && <div className="page-header__actions">{children}</div>}
    </header>
  );
}

export function Avatar({ first, last }) {
  return (
    <span className="avatar" style={{ '--hue': hueOf(`${first}${last}`) }} aria-hidden="true">
      {initials(first, last)}
    </span>
  );
}

export function Pagination({ page, totalPages, totalCount, size, onPage, onSize, sizes = [10, 20, 50, 100] }) {
  return (
    <nav className="pagination" aria-label="Pagination">
      <span className="pagination__info">
        Page <strong>{page}</strong> of <strong>{Math.max(totalPages, 1)}</strong>
        <span className="pagination__dot">·</span>
        {totalCount} total
      </span>

      <div className="pagination__controls">
        <label className="pagination__size">
          <span className="sr-only">Rows per page</span>
          <select className="select" value={size} onChange={(e) => onSize(Number(e.target.value))}>
            {sizes.map((s) => (
              <option key={s} value={s}>
                {s} / page
              </option>
            ))}
          </select>
        </label>
        <button className="btn btn--ghost btn--icon btn--sm" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Previous page">
          <ChevronLeft />
        </button>
        <button
          className="btn btn--ghost btn--icon btn--sm"
          disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}
          aria-label="Next page"
        >
          <ChevronRight />
        </button>
      </div>
    </nav>
  );
}

export function CopyButton({ text, label = 'Copy' }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1400);
    } catch {
      // clipboard blocked; nothing useful to show
    }
  }

  return (
    <button className="icon-btn" onClick={copy} aria-label={copied ? 'Copied' : label} title={label}>
      <AnimatePresence mode="wait" initial={false}>
        <motion.span
          key={copied ? 'done' : 'copy'}
          initial={{ scale: 0.4, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.4, opacity: 0 }}
          transition={{ type: 'spring', stiffness: 500, damping: 26 }}
        >
          {copied ? <Check /> : <Copy />}
        </motion.span>
      </AnimatePresence>
    </button>
  );
}

export function Segmented({ value, options, onChange, ariaLabel }) {
  const id = useId();

  return (
    <div className="segmented" role="group" aria-label={ariaLabel}>
      {options.map((option) => (
        <button key={option.value} type="button" aria-pressed={value === option.value} onClick={() => onChange(option.value)}>
          {value === option.value && (
            <motion.span layoutId={`seg-${id}`} className="segmented__pill" transition={{ type: 'spring', stiffness: 500, damping: 36 }} />
          )}
          <span>{option.label}</span>
        </button>
      ))}
    </div>
  );
}

export function EmptyState({ icon: Icon, title, children, action }) {
  return (
    <motion.div className="empty" initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
      {Icon && (
        <span className="empty__icon">
          <Icon />
        </span>
      )}
      <h3>{title}</h3>
      {children && <p>{children}</p>}
      {action}
    </motion.div>
  );
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="empty empty--error">
      <span className="empty__icon">
        <TriangleAlert />
      </span>
      <h3>Couldn’t load this</h3>
      <p>{error?.message ?? 'Something went wrong.'}</p>
      {onRetry && (
        <button className="btn btn--ghost btn--sm" onClick={onRetry}>
          <RefreshCw /> Try again
        </button>
      )}
    </div>
  );
}

// counts up to the value whenever it changes
export function CountUp({ value, format = (v) => Math.round(v).toLocaleString('en') }) {
  const motionValue = useMotionValue(0);
  const text = useTransform(motionValue, format);

  useEffect(() => {
    const controls = animate(motionValue, value ?? 0, { duration: 0.9, ease: [0.22, 1, 0.36, 1] });
    return () => controls.stop();
  }, [value, motionValue]);

  return <motion.span className="mono">{text}</motion.span>;
}

export function Modal({ open, onClose, title, children }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  return (
    <AnimatePresence>
      {open && (
        <motion.div className="modal" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={onClose}>
          <motion.div
            className="modal__panel"
            role="dialog"
            aria-modal="true"
            aria-label={title}
            initial={{ opacity: 0, y: 24, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 12, scale: 0.98 }}
            transition={{ type: 'spring', stiffness: 380, damping: 30 }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3>{title}</h3>
            {children}
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
