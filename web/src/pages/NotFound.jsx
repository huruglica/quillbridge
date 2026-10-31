import { Link } from 'react-router';
import { Compass } from 'lucide-react';
import { EmptyState } from '../components/ui';

export default function NotFound() {
  return (
    <EmptyState
      icon={Compass}
      title="This page doesn’t exist"
      action={
        <Link to="/calls" className="btn btn--primary">
          Back to the call list
        </Link>
      }
    >
      The link may be old, or the address has a typo.
    </EmptyState>
  );
}
