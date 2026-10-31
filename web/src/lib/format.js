// labels for the API's enum names and keys; unknown values fall back to humanize()

export const TRIGGERS = {
  None: 'No trigger',
  Expansion: 'Expansion',
  Acquisition: 'Acquisition',
  Audit: 'Audit',
  Hiring: 'Hiring AP staff',
  Funding: 'New funding',
  NewLeader: 'New finance lead',
  NewSystem: 'New accounting system'
};

export const PHONE_TYPES = {
  Unknown: 'Unknown',
  Direct: 'Direct line',
  Mobile: 'Mobile',
  Switchboard: 'Switchboard'
};

export const ROLES = {
  Other: 'Other',
  FinanceLeader: 'Finance leader',
  FinanceOperations: 'Finance operations',
  It: 'IT',
  NonBuyer: 'Non-buyer'
};

export const OUTCOMES = {
  MeetingBooked: 'Meeting booked',
  ObjectionRaised: 'Objection',
  Refused: 'Refused',
  NoAnswer: 'No answer'
};

export const OPENINGS = {
  cold: 'Cold',
  follow_up: 'Follow-up',
  referral: 'Referral',
  trigger: 'Trigger',
  event: 'Event'
};

// mirrors SalesFacts.Pushbacks in the API
export const PUSHBACKS = {
  already_have_tool: 'We already have a tool',
  no_budget: 'No budget until next year',
  send_email: 'Just send me an email',
  switching_accounting: 'Switching accounting software',
  how_got_number: 'How did you get my number?',
  not_interested: 'Not interested',
  integration: 'Does it work with our software?',
  pricing: 'What does it cost?',
  outside_accountant: 'Outside accountant handles it',
  not_my_area: "That's not my area"
};

export const PLAN_B = {
  switchboard: 'Switchboard',
  voicemail: 'Voicemail',
  sendEmail: 'Send email',
  gatekeeper: 'Gatekeeper',
  wrongPerson: 'Wrong person'
};

export function humanize(value) {
  if (!value) return '';
  return value
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, (c) => c.toUpperCase());
}

export const label = (map, value) => map[value] ?? humanize(value);

const languageNames = new Intl.DisplayNames(['en'], { type: 'language' });

export function languageName(code) {
  try {
    return languageNames.of(code) ?? code;
  } catch {
    return code;
  }
}

const relative = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

export function timeAgo(value) {
  if (!value) return null;

  const seconds = (new Date(value).getTime() - Date.now()) / 1000;
  const units = [
    ['year', 31536000],
    ['month', 2592000],
    ['week', 604800],
    ['day', 86400],
    ['hour', 3600],
    ['minute', 60]
  ];

  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) {
      return relative.format(Math.round(seconds / size), unit);
    }
  }

  return 'just now';
}

export const formatDate = (value) =>
  value ? new Date(value).toLocaleDateString('en', { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

export const usd = (value, digits = 2) =>
  new Intl.NumberFormat('en', { style: 'currency', currency: 'USD', minimumFractionDigits: digits, maximumFractionDigits: 4 }).format(value ?? 0);

export const number = (value) => new Intl.NumberFormat('en').format(value ?? 0);

export const percent = (fraction) =>
  new Intl.NumberFormat('en', { style: 'percent', maximumFractionDigits: 1 }).format(fraction ?? 0);

export const initials = (first = '', last = '') => `${first[0] ?? ''}${last[0] ?? ''}`.toUpperCase();

// a stable hue per name so each avatar keeps its color across pages
export function hueOf(text = '') {
  let hash = 0;
  for (const char of text) hash = (hash * 31 + char.charCodeAt(0)) % 360;
  return hash;
}
