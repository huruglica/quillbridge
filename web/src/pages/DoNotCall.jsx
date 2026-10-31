import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { motion } from 'motion/react';
import { MapPin, ShieldCheck } from 'lucide-react';
import { api } from '../lib/api';
import { number } from '../lib/format';
import { Avatar, EmptyState, ErrorState, PageHeader, Pagination } from '../components/ui';
import { usePaging } from '../lib/paging';
import { ListSkeleton } from './CallList';
import './lists.css';

export default function DoNotCall() {
  const { page, size, setPage, setSize } = usePaging();

  const query = useQuery({
    queryKey: ['do-not-call', page, size],
    queryFn: () => api.doNotCall(page, size),
    placeholderData: keepPreviousData
  });

  const result = query.data;
  const items = result?.items ?? [];

  return (
    <>
      <PageHeader
        title="Do not call"
        subtitle={
          result
            ? `${number(result.totalCount)} prospects kept off your call list, and why.`
            : 'Prospects kept off your call list, and why.'
        }
      />

      {query.isPending ? (
        <ListSkeleton rows={6} />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={query.refetch} />
      ) : items.length === 0 ? (
        <EmptyState icon={ShieldCheck} title="Nobody is blocked">
          Prospects marked do-not-call in your import will show up here.
        </EmptyState>
      ) : (
        <>
          <div className={`rows rows--dnc ${query.isPlaceholderData ? 'is-stale' : ''}`} role="list">
            <div className="rows__head" aria-hidden="true">
              <span>Prospect</span>
              <span>Company</span>
              <span>Reason</span>
            </div>

            {items.map((prospect, i) => (
              <motion.div
                key={prospect.prospectId}
                role="listitem"
                className="row"
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(i, 14) * 0.025, duration: 0.35 }}
              >
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
                    <MapPin /> {prospect.hq}
                  </small>
                </span>
                <span className="row__reason">
                  {prospect.doNotCallReason ? (
                    <span className="badge badge--bad">{prospect.doNotCallReason}</span>
                  ) : (
                    <span className="muted">No reason given</span>
                  )}
                </span>
              </motion.div>
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
