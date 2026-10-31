import { useRef, useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowRight, CircleCheck, FileSpreadsheet, LoaderCircle, Trash2, TriangleAlert, UploadCloud, X } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import { number } from '../lib/format';
import { CountUp, Modal, PageHeader } from '../components/ui';
import './import.css';

export default function Import() {
  const queryClient = useQueryClient();
  const inputRef = useRef(null);
  const [file, setFile] = useState(null);
  const [dragging, setDragging] = useState(false);
  const [confirming, setConfirming] = useState(false);

  const refreshLists = () => {
    queryClient.invalidateQueries({ queryKey: ['call-list'] });
    queryClient.invalidateQueries({ queryKey: ['do-not-call'] });
    queryClient.invalidateQueries({ queryKey: ['guide'] });
  };

  const upload = useMutation({
    mutationFn: () => api.importProspects(file),
    onSuccess: (result) => {
      refreshLists();
      toast.success(`${number(result.imported)} prospects imported`);
    },
    onError: (error) => toast.error(error.message)
  });

  const wipe = useMutation({
    mutationFn: api.deleteProspects,
    onSuccess: (result) => {
      refreshLists();
      upload.reset();
      setConfirming(false);
      toast.success(`${number(result.deleted)} prospects deleted`);
    },
    onError: (error) => toast.error(error.message)
  });

  function pick(picked) {
    if (!picked) return;
    if (!picked.name.toLowerCase().endsWith('.csv')) {
      toast.error('Please choose a .csv file.');
      return;
    }
    setFile(picked);
    upload.reset();
  }

  const result = upload.data;

  return (
    <>
      <PageHeader title="Import prospects" subtitle="Upload a CSV export. We check every row, rank the good ones, and tell you what we skipped." />

      <div className="import">
        <motion.div
          className={`dropzone ${dragging ? 'is-dragging' : ''} ${file ? 'has-file' : ''}`}
          onDragOver={(e) => {
            e.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragging(false);
            pick(e.dataTransfer.files[0]);
          }}
          onClick={() => inputRef.current?.click()}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && inputRef.current?.click()}
          animate={{ scale: dragging ? 1.015 : 1 }}
          transition={{ type: 'spring', stiffness: 400, damping: 28 }}
        >
          <input ref={inputRef} type="file" accept=".csv,text/csv" hidden onChange={(e) => pick(e.target.files[0])} />

          <motion.span className="dropzone__icon" animate={{ y: dragging ? -6 : 0, rotate: dragging ? -6 : 0 }}>
            <UploadCloud />
          </motion.span>
          <h3>{dragging ? 'Drop it' : 'Drop your CSV here'}</h3>
          <p>
            or <u>browse files</u>. One row per prospect.
          </p>
        </motion.div>

        <AnimatePresence>
          {file && (
            <motion.div
              className="file-pill"
              initial={{ opacity: 0, y: -8, height: 0 }}
              animate={{ opacity: 1, y: 0, height: 'auto' }}
              exit={{ opacity: 0, y: -8, height: 0 }}
            >
              <div className="file-pill__inner">
                <FileSpreadsheet />
                <span>
                  <strong>{file.name}</strong>
                  <small>{(file.size / 1024).toFixed(1)} KB</small>
                </span>
                <button className="icon-btn" onClick={() => setFile(null)} aria-label="Remove file" disabled={upload.isPending}>
                  <X />
                </button>
                <button className="btn btn--accent" onClick={() => upload.mutate()} disabled={upload.isPending}>
                  {upload.isPending ? <LoaderCircle className="spin" /> : <UploadCloud />}
                  {upload.isPending ? 'Importing…' : 'Import'}
                </button>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        <AnimatePresence>{result && <ImportResult result={result} />}</AnimatePresence>

        <section className="danger">
          <div>
            <h3>Start over</h3>
            <p>Delete every prospect and its guides. Use this before importing a fresh list.</p>
          </div>
          <button className="btn btn--danger-ghost" onClick={() => setConfirming(true)}>
            <Trash2 /> Delete all prospects
          </button>
        </section>
      </div>

      <Modal open={confirming} onClose={() => setConfirming(false)} title="Delete all prospects?">
        <p>This removes every prospect and all of their call guides. It can’t be undone.</p>
        <div className="modal__actions">
          <button className="btn btn--ghost" onClick={() => setConfirming(false)}>
            Cancel
          </button>
          <button className="btn btn--danger" onClick={() => wipe.mutate()} disabled={wipe.isPending}>
            {wipe.isPending ? <LoaderCircle className="spin" /> : <Trash2 />} Delete everything
          </button>
        </div>
      </Modal>
    </>
  );
}

function ImportResult({ result }) {
  return (
    <motion.section
      className="result"
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.45, ease: [0.22, 1, 0.36, 1] }}
    >
      <div className="result__stats">
        <div className="stat">
          <span>Rows read</span>
          <strong>
            <CountUp value={result.totalRows} />
          </strong>
        </div>
        <div className="stat stat--good">
          <span>
            <CircleCheck /> Imported
          </span>
          <strong>
            <CountUp value={result.imported} />
          </strong>
        </div>
        <div className="stat stat--bad">
          <span>
            <X /> Skipped
          </span>
          <strong>
            <CountUp value={result.skipped} />
          </strong>
        </div>
        <div className="stat stat--warn">
          <span>
            <TriangleAlert /> Warnings
          </span>
          <strong>
            <CountUp value={result.warnings.length} />
          </strong>
        </div>
      </div>

      {result.imported > 0 && (
        <Link to="/calls" className="btn btn--primary result__cta">
          See your call list <ArrowRight />
        </Link>
      )}

      <div className="result__lists">
        <IssueList title="Skipped rows" tone="bad" issues={result.skippedRows} />
        <IssueList title="Warnings" tone="warn" issues={result.warnings} />
      </div>
    </motion.section>
  );
}

function IssueList({ title, tone, issues }) {
  if (!issues.length) return null;

  return (
    <div className="card issues">
      <div className="card__title">
        {title}
        <span className={`badge badge--${tone}`}>{issues.length}</span>
      </div>
      <ul>
        {issues.map((issue, i) => (
          <li key={`${issue.row}-${i}`}>
            <span className="issues__row mono">Row {issue.row}</span>
            {issue.prospectId && <span className="issues__id mono">{issue.prospectId}</span>}
            <span>{issue.message}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
