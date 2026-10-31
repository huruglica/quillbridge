import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { motion } from 'motion/react';
import { Bot, Download, Layers, LoaderCircle, MessageCircleWarning, Play, Sparkles, Wallet } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import { OPENINGS, PUSHBACKS, ROLES, TRIGGERS, label, number, percent, timeAgo, usd } from '../lib/format';
import { CountUp, ErrorState, PageHeader, Segmented } from '../components/ui';
import './insights.css';

export default function Insights() {
  return (
    <>
      <PageHeader title="Insights" subtitle="What the guides cost, and which ones book meetings.">
        <ExportButton />
      </PageHeader>

      <div className="bento">
        <BudgetCard />
        <BatchCard />
      </div>

      <Report />
    </>
  );
}

function ExportButton() {
  const exporter = useMutation({
    mutationFn: api.exportGuides,
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const link = Object.assign(document.createElement('a'), { href: url, download: 'quillbridge-guides.md' });
      link.click();
      URL.revokeObjectURL(url);
    },
    onError: (error) => toast.error(error.message)
  });

  return (
    <button className="btn btn--ghost" onClick={() => exporter.mutate()} disabled={exporter.isPending}>
      {exporter.isPending ? <LoaderCircle className="spin" /> : <Download />} Export guides
    </button>
  );
}

function BudgetCard() {
  const query = useQuery({ queryKey: ['budget'], queryFn: api.budget, refetchInterval: 15000 });
  const budget = query.data;
  const used = budget && budget.limitUsd > 0 ? Math.min(budget.spentUsd / budget.limitUsd, 1) : 0;

  return (
    <section className="card budget">
      <div className="card__title">
        <Wallet /> AI budget
        <span className="spacer" />
        {budget &&
          (budget.exhausted ? (
            <span className="badge badge--bad">Exhausted</span>
          ) : budget.aiConfigured ? (
            <span className="badge badge--good">Active</span>
          ) : (
            <span className="badge badge--warn">Not configured</span>
          ))}
      </div>

      {query.isError ? (
        <ErrorState error={query.error} onRetry={query.refetch} />
      ) : (
        <div className="budget__body">
          <Ring fraction={used} />
          <div className="budget__numbers">
            <strong>
              <CountUp value={budget?.spentUsd ?? 0} format={(v) => usd(v)} />
            </strong>
            <span>of {usd(budget?.limitUsd ?? 0)} spent</span>
            {budget?.model && (
              <span className="badge">
                <Bot /> {budget.model}
              </span>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

// a single-value meter; the number beside it carries the reading
function Ring({ fraction }) {
  const radius = 42;
  const length = 2 * Math.PI * radius;
  const tone = fraction >= 0.9 ? 'var(--bad)' : fraction >= 0.7 ? 'var(--warn)' : 'var(--accent)';

  return (
    <svg className="ring" viewBox="0 0 100 100" role="img" aria-label={`${percent(fraction)} of budget used`}>
      <circle cx="50" cy="50" r={radius} className="ring__track" />
      <motion.circle
        cx="50"
        cy="50"
        r={radius}
        className="ring__value"
        style={{ stroke: tone }}
        strokeDasharray={length}
        initial={{ strokeDashoffset: length }}
        animate={{ strokeDashoffset: length * (1 - fraction) }}
        transition={{ duration: 1.1, ease: [0.22, 1, 0.36, 1] }}
      />
      <text x="50" y="55" textAnchor="middle" className="ring__label">
        {Math.round(fraction * 100)}%
      </text>
    </svg>
  );
}

function BatchCard() {
  const queryClient = useQueryClient();
  const [limit, setLimit] = useState('');

  const status = useQuery({
    queryKey: ['batch'],
    queryFn: api.batchStatus,
    // poll while a batch runs, then stop
    refetchInterval: (query) => (query.state.data?.running ? 1500 : false)
  });

  const start = useMutation({
    mutationFn: () => api.startBatch(limit ? Number(limit) : null),
    onSuccess: (data) => {
      queryClient.setQueryData(['batch'], data);
      toast.success('Batch started');
    },
    onError: (error) => toast.error(error.message),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['budget'] })
  });

  const batch = status.data;
  const progress = batch?.total ? batch.done / batch.total : 0;

  return (
    <section className="card batch">
      <div className="card__title">
        <Layers /> Batch write guides
        <span className="spacer" />
        {batch?.running && (
          <span className="badge badge--accent">
            <span className="pulse" /> Running
          </span>
        )}
      </div>

      <p className="batch__hint">Write AI guides ahead of time for prospects that don’t have one yet, highest priority first.</p>

      <form
        className="batch__form"
        onSubmit={(e) => {
          e.preventDefault();
          start.mutate();
        }}
      >
        <label className="field">
          <span>How many</span>
          <input
            className="input"
            type="number"
            min="1"
            placeholder="All"
            value={limit}
            onChange={(e) => setLimit(e.target.value)}
            disabled={batch?.running}
          />
        </label>
        <button className="btn btn--primary" disabled={batch?.running || start.isPending}>
          {start.isPending ? <LoaderCircle className="spin" /> : <Play />} Start
        </button>
      </form>

      {batch && batch.total > 0 && (
        <div className="batch__status">
          <div className="progress" role="progressbar" aria-valuenow={batch.done} aria-valuemin={0} aria-valuemax={batch.total}>
            <motion.span animate={{ scaleX: progress }} transition={{ duration: 0.6, ease: [0.22, 1, 0.36, 1] }} />
          </div>
          <div className="batch__numbers">
            <span>
              <strong className="mono">
                {batch.done}/{batch.total}
              </strong>{' '}
              done
            </span>
            <span>
              <Sparkles /> {batch.ai} AI
            </span>
            <span>
              <Bot /> {batch.template} template
            </span>
            {batch.skipped > 0 && <span>{batch.skipped} skipped</span>}
            <span className="mono">{usd(batch.costUsd, 4)}</span>
          </div>
          {batch.finishedAt && !batch.running && <p className="batch__done">Finished {timeAgo(batch.finishedAt)}.</p>}
        </div>
      )}
    </section>
  );
}

const GROUPS = [
  { value: 'byOpening', label: 'Opening', names: OPENINGS },
  { value: 'byRole', label: 'Role', names: ROLES },
  { value: 'byTrigger', label: 'Trigger', names: TRIGGERS }
];

function Report() {
  const [group, setGroup] = useState('byOpening');
  const query = useQuery({ queryKey: ['report'], queryFn: api.report });
  const report = query.data;

  if (query.isError) return <ErrorState error={query.error} onRetry={query.refetch} />;

  const active = GROUPS.find((g) => g.value === group);
  const rows = report?.[group] ?? [];
  const pushbacks = report?.mostCommonPushbacks ?? [];
  const maxPushback = Math.max(...pushbacks.map((p) => p.value), 1);

  return (
    <div className="report">
      <section className="card report__hero">
        <div className="card__title">
          <MessageCircleWarning /> Calls with feedback
        </div>
        <strong className="hero-number">
          <CountUp value={report?.guidesWithFeedback ?? 0} />
        </strong>
        <p>Guides an SDR rated after the call. Every rate below is built from these.</p>
      </section>

      <section className="card report__pushbacks">
        <div className="card__title">Most common pushbacks</div>
        {pushbacks.length === 0 ? (
          <p className="muted-text">No pushbacks logged yet.</p>
        ) : (
          <ul className="hbars">
            {pushbacks.map((p, i) => (
              <li key={p.key} title={`${label(PUSHBACKS, p.key)}: ${p.value}`}>
                <span className="hbars__label">{label(PUSHBACKS, p.key)}</span>
                <span className="hbars__track">
                  <motion.span
                    initial={{ scaleX: 0 }}
                    animate={{ scaleX: p.value / maxPushback }}
                    transition={{ delay: i * 0.05, duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
                  />
                </span>
                <span className="hbars__value mono">{p.value}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card report__rates">
        <div className="card__title">
          Meeting rate by
          <span className="spacer" />
          <Segmented value={group} onChange={setGroup} options={GROUPS} ariaLabel="Group meeting rate by" />
        </div>

        {rows.length === 0 ? (
          <p className="muted-text">Log call outcomes on guides to see which approaches book meetings.</p>
        ) : (
          <table className="rates">
            <thead>
              <tr>
                <th>{active.label}</th>
                <th className="num">Calls</th>
                <th className="num">Meetings</th>
                <th className="rates__bar-col">Meeting rate</th>
                <th className="num">Marked helpful</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <motion.tr key={`${group}-${row.group}`} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: i * 0.04 }}>
                  <td>{label(active.names, row.group)}</td>
                  <td className="num mono">{number(row.calls)}</td>
                  <td className="num mono">{number(row.meetingsBooked)}</td>
                  <td className="rates__bar-col">
                    <span className="rate">
                      <span className="rate__track">
                        <motion.span
                          initial={{ scaleX: 0 }}
                          animate={{ scaleX: row.meetingRate }}
                          transition={{ delay: i * 0.04, duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
                        />
                      </span>
                      <span className="mono">{percent(row.meetingRate)}</span>
                    </span>
                  </td>
                  <td className="num mono">{number(row.markedWorked)}</td>
                </motion.tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}
