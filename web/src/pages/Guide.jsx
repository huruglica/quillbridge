import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AnimatePresence, motion } from 'motion/react';
import {
  ArrowLeft,
  Bot,
  CalendarCheck,
  ChevronDown,
  Feather,
  Flag,
  History,
  Info,
  Lightbulb,
  ListChecks,
  LoaderCircle,
  MessageSquareQuote,
  RefreshCw,
  Route,
  Save,
  ShieldQuestion,
  Sparkles,
  Target,
  ThumbsDown,
  ThumbsUp,
  Trophy
} from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import { useAiPreference } from '../lib/aiPreference';
import { OPENINGS, OUTCOMES, PLAN_B, PUSHBACKS, ROLES, formatDate, label, languageName, number, usd } from '../lib/format';
import { CopyButton, ErrorState, Segmented } from '../components/ui';
import './guide.css';

const WRITING_STEPS = ['Reading the notes…', 'Picking an opening…', 'Choosing the success story…', 'Drafting pushback answers…', 'Checking every fact…'];

export default function Guide() {
  const { prospectId } = useParams();
  const queryClient = useQueryClient();
  const [useAi] = useAiPreference();

  const queryKey = ['guide', prospectId, useAi];
  const query = useQuery({
    queryKey,
    queryFn: () => api.guide(prospectId, useAi),
    staleTime: Infinity,
    retry: false
  });

  const regenerate = useMutation({
    mutationFn: () => api.regenerateGuide(prospectId, useAi),
    onSuccess: (guide) => {
      queryClient.setQueryData(queryKey, guide);
      toast.success(guide.source === 'Ai' ? 'Fresh guide written' : 'Template guide ready');
    },
    onError: (error) => toast.error(error.message)
  });

  const guide = query.data;
  const busy = query.isFetching || regenerate.isPending;

  return (
    <div className="guide">
      <Link to="/calls" className="back-link">
        <ArrowLeft /> Call list
      </Link>

      {query.isPending ? (
        <Writing />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={query.refetch} />
      ) : (
        <>
          <header className="guide__header">
            <div>
              <motion.h1 initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
                {guide.prospectName}
              </motion.h1>
              <p>
                {guide.title || 'No title'} · <strong>{guide.company}</strong>
              </p>
              <div className="guide__chips">
                <SourceBadge source={guide.source} />
                <span className="badge">{label(ROLES, guide.role)}</span>
                <span className="badge">{label(OPENINGS, guide.openingAngle)} opening</span>
                <span className="badge">{languageName(guide.language)}</span>
                {guide.historyCount > 0 && (
                  <span className="badge" title="Earlier guides kept for this prospect">
                    <History /> {guide.historyCount} earlier
                  </span>
                )}
              </div>
            </div>

            <div className="guide__actions">
              <button className="btn btn--ghost" onClick={() => regenerate.mutate()} disabled={busy}>
                <RefreshCw className={regenerate.isPending ? 'spin' : ''} />
                {regenerate.isPending ? 'Writing…' : 'Regenerate'}
              </button>
            </div>
          </header>

          {guide.fallbackReason && (
            <div className="callout callout--info">
              <Info />
              <p>
                <strong>Template guide.</strong> {guide.fallbackReason}
              </p>
            </div>
          )}

          {guide.flags.length > 0 && (
            <div className="callout callout--warn">
              <Flag />
              <ul>
                {guide.flags.map((flag) => (
                  <li key={flag}>{flag}</li>
                ))}
              </ul>
            </div>
          )}

          <AnimatePresence mode="wait">
            <motion.div
              key={guide.guideId}
              className={`guide__grid ${regenerate.isPending ? 'is-stale' : ''}`}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
            >
              <div className="guide__main">
                <Opening guide={guide} />
                <Block icon={Lightbulb} title="Why now" text={guide.content.whyNow} />
                <Questions items={guide.content.discoveryQuestions} />
                <Block icon={Target} title="Core message" text={guide.content.coreMessage} />
                <Block icon={Trophy} title="Success story" text={guide.content.successStory} />
                <Block icon={CalendarCheck} title="Meeting ask" text={guide.content.meetingAsk} accent />
                <Pushbacks items={guide.content.pushbacks} />
                <PlanB planB={guide.content.planB} />
              </div>

              <aside className="guide__side">
                {guide.content.keyFacts.length > 0 && (
                  <Card icon={Sparkles} title="Key facts">
                    <ul className="facts">
                      {guide.content.keyFacts.map((fact) => (
                        <li key={fact}>{fact}</li>
                      ))}
                    </ul>
                  </Card>
                )}

                <Card icon={History} title="Last touch">
                  <p className="prose">{guide.content.lastTouch || 'We have never contacted them.'}</p>
                </Card>

                <Feedback guide={guide} onSaved={(updated) => queryClient.setQueryData(queryKey, updated)} />

                <Meta guide={guide} />
              </aside>
            </motion.div>
          </AnimatePresence>
        </>
      )}
    </div>
  );
}

function Card({ icon: Icon, title, copy, accent, className = '', children }) {
  return (
    <section className={`card ${accent ? 'card--accent' : ''} ${className}`}>
      <div className="card__title">
        <Icon />
        {title}
        <span className="spacer" />
        {copy && <CopyButton text={copy} label={`Copy ${title.toLowerCase()}`} />}
      </div>
      {children}
    </section>
  );
}

function Block({ icon, title, text, accent }) {
  if (!text) return null;

  return (
    <Card icon={icon} title={title} copy={text} accent={accent}>
      <p className="prose">{text}</p>
    </Card>
  );
}

function Opening({ guide }) {
  return (
    <Card icon={MessageSquareQuote} title="Opening line" copy={guide.content.opening} className="opening">
      <blockquote>{guide.content.opening}</blockquote>
    </Card>
  );
}

function Questions({ items }) {
  if (!items.length) return null;

  return (
    <Card icon={ListChecks} title="Discovery questions" copy={items.map((q, i) => `${i + 1}. ${q}`).join('\n')}>
      <ol className="questions">
        {items.map((question) => (
          <li key={question}>{question}</li>
        ))}
      </ol>
    </Card>
  );
}

function Pushbacks({ items }) {
  const [open, setOpen] = useState(items[0]?.key ?? null);

  if (!items.length) return null;

  return (
    <Card icon={ShieldQuestion} title="If they push back">
      <div className="pushbacks">
        {items.map((pushback) => {
          const isOpen = open === pushback.key;

          return (
            <div key={pushback.key} className={`pushback ${isOpen ? 'is-open' : ''}`}>
              <button className="pushback__q" onClick={() => setOpen(isOpen ? null : pushback.key)} aria-expanded={isOpen}>
                <span>“{pushback.objection}”</span>
                <ChevronDown />
              </button>
              <AnimatePresence initial={false}>
                {isOpen && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: 'auto', opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
                    style={{ overflow: 'hidden' }}
                  >
                    <div className="pushback__a">
                      <p>{pushback.answer}</p>
                      <CopyButton text={pushback.answer} label="Copy answer" />
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function PlanB({ planB }) {
  const keys = Object.keys(PLAN_B).filter((key) => planB[key]);
  const [tab, setTab] = useState(keys[0]);

  if (!keys.length) return null;

  return (
    <Card icon={Route} title="Plan B" copy={planB[tab]}>
      <Segmented value={tab} onChange={setTab} ariaLabel="Plan B scenario" options={keys.map((key) => ({ value: key, label: PLAN_B[key] }))} />
      <AnimatePresence mode="wait">
        <motion.p
          key={tab}
          className="prose planb__text"
          initial={{ opacity: 0, x: 8 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -8 }}
          transition={{ duration: 0.2 }}
        >
          {planB[tab]}
        </motion.p>
      </AnimatePresence>
    </Card>
  );
}

function Feedback({ guide, onSaved }) {
  const initial = () => ({
    worked: guide.worked ?? null,
    outcome: guide.outcome ?? null,
    pushbackHeard: guide.pushbackHeard ?? '',
    note: guide.feedbackNote ?? ''
  });

  const [form, setForm] = useState(initial);
  useEffect(() => setForm(initial()), [guide.guideId]); // eslint-disable-line react-hooks/exhaustive-deps

  const set = (patch) => setForm((current) => ({ ...current, ...patch }));

  const save = useMutation({
    mutationFn: () =>
      api.saveFeedback(guide.guideId, {
        worked: form.worked,
        outcome: form.outcome,
        pushbackHeard: form.pushbackHeard || null,
        note: form.note.trim() || null
      }),
    onSuccess: (updated) => {
      onSaved(updated);
      toast.success('Feedback saved. Thanks!');
    },
    onError: (error) => toast.error(error.message)
  });

  // the guide's own pushbacks first, then every other known one
  const ownKeys = guide.content.pushbacks.map((p) => p.key);
  const otherKeys = Object.keys(PUSHBACKS).filter((key) => !ownKeys.includes(key));

  return (
    <Card icon={Feather} title="After the call" className="feedback">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate();
        }}
      >
        <div className="field">
          <span>Did the guide help?</span>
          <div className="thumbs">
            <button
              type="button"
              className={form.worked === true ? 'is-on is-good' : ''}
              onClick={() => set({ worked: form.worked === true ? null : true })}
              aria-pressed={form.worked === true}
            >
              <ThumbsUp /> Yes
            </button>
            <button
              type="button"
              className={form.worked === false ? 'is-on is-bad' : ''}
              onClick={() => set({ worked: form.worked === false ? null : false })}
              aria-pressed={form.worked === false}
            >
              <ThumbsDown /> No
            </button>
          </div>
        </div>

        <div className="field">
          <span>Outcome</span>
          <div className="outcomes">
            {Object.entries(OUTCOMES).map(([value, text]) => (
              <button
                key={value}
                type="button"
                className={form.outcome === value ? 'is-on' : ''}
                onClick={() => set({ outcome: form.outcome === value ? null : value })}
                aria-pressed={form.outcome === value}
              >
                {text}
              </button>
            ))}
          </div>
        </div>

        <label className="field">
          <span>Pushback heard</span>
          <select className="select" value={form.pushbackHeard} onChange={(e) => set({ pushbackHeard: e.target.value })}>
            <option value="">None</option>
            <optgroup label="In this guide">
              {ownKeys.map((key) => (
                <option key={key} value={key}>
                  {label(PUSHBACKS, key)}
                </option>
              ))}
            </optgroup>
            <optgroup label="Other">
              {otherKeys.map((key) => (
                <option key={key} value={key}>
                  {label(PUSHBACKS, key)}
                </option>
              ))}
            </optgroup>
          </select>
        </label>

        <label className="field">
          <span>Note</span>
          <textarea
            className="textarea"
            rows={3}
            placeholder="What happened on the call?"
            value={form.note}
            onChange={(e) => set({ note: e.target.value })}
          />
        </label>

        <button className="btn btn--primary" disabled={save.isPending}>
          {save.isPending ? <LoaderCircle className="spin" /> : <Save />} Save feedback
        </button>
      </form>
    </Card>
  );
}

function Meta({ guide }) {
  return (
    <dl className="meta">
      <div>
        <dt>Created</dt>
        <dd>{formatDate(guide.createdAt)}</dd>
      </div>
      <div>
        <dt>Tokens</dt>
        <dd className="mono">
          {number(guide.inputTokens)} in · {number(guide.outputTokens)} out
        </dd>
      </div>
      <div>
        <dt>Cost</dt>
        <dd className="mono">{usd(guide.costUsd, 4)}</dd>
      </div>
      {guide.sdrName && (
        <div>
          <dt>SDR</dt>
          <dd>{guide.sdrName}</dd>
        </div>
      )}
    </dl>
  );
}

function SourceBadge({ source }) {
  return source === 'Ai' ? (
    <span className="badge badge--accent">
      <Sparkles /> AI guide
    </span>
  ) : (
    <span className="badge badge--warn">
      <Bot /> Template
    </span>
  );
}

// shown while the AI writes; a guide can take a while
function Writing() {
  const [step, setStep] = useState(0);

  useEffect(() => {
    const timer = setInterval(() => setStep((s) => (s + 1) % WRITING_STEPS.length), 1800);
    return () => clearInterval(timer);
  }, []);

  return (
    <div className="writing">
      <div className="writing__pen" aria-hidden="true">
        <Feather />
        <span className="writing__line" />
      </div>
      <AnimatePresence mode="wait">
        <motion.p
          key={step}
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -6 }}
          transition={{ duration: 0.25 }}
          aria-live="polite"
        >
          {WRITING_STEPS[step]}
        </motion.p>
      </AnimatePresence>

      <div className="writing__ghost">
        <div className="skeleton" style={{ height: 120 }} />
        <div className="skeleton" style={{ height: 80 }} />
        <div className="skeleton" style={{ height: 140 }} />
      </div>
    </div>
  );
}
