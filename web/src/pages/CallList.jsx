import { Link, useNavigate } from 'react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { motion } from 'motion/react';
import { ArrowUpRight, Building2, Clock, MapPin, Phone, PhoneCall, Sparkles, Upload, Users, Zap } from 'lucide-react';
import { api } from '../lib/api';
import { usePaging } from '../lib/paging';
import { useAiPreference } from '../lib/aiPreference';
import { PHONE_TYPES, TRIGGERS, label, number, timeAgo } from '../lib/format';
import { Avatar, EmptyState, ErrorState, PageHeader, Pagination } from '../components/ui';
import './lists.css';

export default function CallList() {
  const navigate = useNavigate();
  const { page, size, setPage, setSize } = usePaging();
  const [useAi, setUseAi] = useAiPreference();

  const query = useQuery({
    queryKey: ['call-list', page, size],
    queryFn: () => api.callList(page, size),
    placeholderData: keepPreviousData
  });

  const result = query.data;
  const items = result?.items ?? [];
  const maxScore = Math.max(...items.map((p) => p.priorityScore), 1);
  const [next, ...rest] = items;
  const rankOffset = (page - 1) * size;
  const showHero = page === 1 && next;

  return (
    <>
      <PageHeader
        title="Call list"
        subtitle={
          result ? `${number(result.totalCount)} prospects, ranked by priority. Highest first.` : 'Your prospects, ranked by priority.'
        }
      >
        <label className="switch ai-switch" title="Off: guides are written from the rule-based template, without the AI">
          <input type="checkbox" checked={useAi} onChange={(e) => setUseAi(e.target.checked)} />
          <span>
            Use AI for guides
            <small>{useAi ? 'Guides are written by the AI' : 'Guides use the template'}</small>
          </span>
        </label>
      </PageHeader>

      {query.isPending ? (
        <ListSkeleton />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={query.refetch} />
      ) : items.length === 0 ? (
        <EmptyState
          icon={PhoneCall}
          title="No one to call yet"
          action={
            <Link to="/import" className="btn btn--primary">
              <Upload /> Import prospects
            </Link>
          }
        >
          Import a prospects CSV and your call list will be ranked here.
        </EmptyState>
      ) : (
        <>
          {showHero && <NextUp prospect={next} onOpen={() => navigate(`/calls/${next.prospectId}`)} />}

          <div className={`rows ${query.isPlaceholderData ? 'is-stale' : ''}`} role="list">
            <div className="rows__head" aria-hidden="true">
              <span>#</span>
              <span>Prospect</span>
              <span>Company</span>
              <span>Fit</span>
              <span>Trigger</span>
              <span>Last touch</span>
              <span>Priority</span>
            </div>

            {(showHero ? rest : items).map((prospect, i) => (
              <ProspectRow
                key={prospect.prospectId}
                prospect={prospect}
                rank={rankOffset + i + (showHero ? 2 : 1)}
                index={i}
                maxScore={maxScore}
                onOpen={() => navigate(`/calls/${prospect.prospectId}`)}
              />
            ))}
          </div>

          <Pagination
            page={page}
            size={size}
            totalPages={result.totalPages}
            totalCount={result.totalCount}
            onPage={setPage}
            onSize={setSize}
          />
        </>
      )}
    </>
  );
}

function NextUp({ prospect, onOpen }) {
  return (
    <motion.button
      className="next-up"
      onClick={onOpen}
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
      whileHover={{ y: -2 }}
    >
      <div className="next-up__glow" aria-hidden="true" />
      <div className="next-up__label">
        <Zap /> Next up
      </div>

      <div className="next-up__body">
        <Avatar first={prospect.firstName} last={prospect.lastName} />
        <div className="next-up__who">
          <h2>
            {prospect.firstName} {prospect.lastName}
          </h2>
          <p>
            {prospect.title || 'No title'} · <strong>{prospect.company}</strong>
          </p>
        </div>
        <span className="next-up__cta">
          <Sparkles /> Open call guide <ArrowUpRight />
        </span>
      </div>

      <div className="next-up__facts">
        <span>
          <Building2 /> {prospect.industry} · {number(prospect.employees)} people
        </span>
        <span>
          <MapPin /> {prospect.hq}
        </span>
        <span>
          <Phone /> {label(PHONE_TYPES, prospect.phoneType)}
        </span>
        {prospect.trigger !== 'None' && (
          <span className="badge badge--accent">
            <Zap /> {label(TRIGGERS, prospect.trigger)}
          </span>
        )}
        <span className={`tier tier--${prospect.fitTier}`} title={`Fit tier ${prospect.fitTier}`}>
          {prospect.fitTier === 'None' ? '–' : prospect.fitTier}
        </span>
      </div>
    </motion.button>
  );
}

function ProspectRow({ prospect, rank, index, maxScore, onOpen }) {
  const touched = timeAgo(prospect.lastTouch);

  return (
    <motion.div
      role="listitem"
      className="row"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && (e.preventDefault(), onOpen())}
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: Math.min(index, 14) * 0.025, duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
    >
      <span className="row__rank mono">{rank}</span>

      <span className="row__who">
        <Avatar first={prospect.firstName} last={prospect.lastName} />
        <span>
          <strong>
            {prospect.firstName} {prospect.lastName}
          </strong>
          <small>{prospect.title || 'No title'}</small>
        </span>
      </span>

      <span className="row__company">
        <strong>{prospect.company}</strong>
        <small>
          <Users /> {number(prospect.employees)} · <MapPin /> {prospect.hq}
        </small>
      </span>

      <span className="row__fit">
        <span className={`tier tier--${prospect.fitTier}`} title={`Fit tier ${prospect.fitTier}`}>
          {prospect.fitTier === 'None' ? '–' : prospect.fitTier}
        </span>
      </span>

      <span className="row__trigger">
        {prospect.trigger === 'None' ? (
          <span className="muted">—</span>
        ) : (
          <span className="badge badge--accent">{label(TRIGGERS, prospect.trigger)}</span>
        )}
      </span>

      <span className="row__touch">
        {touched ? (
          <>
            <strong>
              <Clock /> {touched}
            </strong>
            {prospect.lastTouchType && <small>{prospect.lastTouchType}</small>}
          </>
        ) : (
          <span className="muted">Never</span>
        )}
      </span>

      <span className="row__score" title={`Priority score ${prospect.priorityScore.toFixed(1)}`}>
        <span className="score-bar">
          <motion.span
            initial={{ scaleX: 0 }}
            animate={{ scaleX: prospect.priorityScore / maxScore }}
            transition={{ delay: 0.15 + Math.min(index, 14) * 0.025, duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
          />
        </span>
        <span className="mono">{prospect.priorityScore.toFixed(1)}</span>
      </span>
    </motion.div>
  );
}

export function ListSkeleton({ rows = 8 }) {
  return (
    <div className="rows">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="row row--skeleton">
          <span className="skeleton" style={{ width: 18, height: 14 }} />
          <span className="row__who">
            <span className="skeleton" style={{ width: 34, height: 34, borderRadius: 11 }} />
            <span className="skeleton" style={{ width: '60%', height: 14 }} />
          </span>
          <span className="skeleton" style={{ width: '70%', height: 14 }} />
          <span className="skeleton" style={{ width: 26, height: 26 }} />
          <span className="skeleton" style={{ width: 80, height: 20 }} />
          <span className="skeleton" style={{ width: 70, height: 14 }} />
          <span className="skeleton" style={{ width: '80%', height: 8 }} />
        </div>
      ))}
    </div>
  );
}
