import './mates.css';

// back to front; each mate has its own ink so faces read on its body color
const CAST = ['purple', 'black', 'yellow', 'orange'];

/**
 * Four little characters that watch the login form.
 * mood: idle | nosy | shy | exposed | error | happy (one word poses the whole cast)
 * look: { x, y } in -1..1, where their eyes point
 */
export default function Mates({ mood = 'idle', look = { x: 0, y: 0 } }) {
  return (
    <div className="mates" data-mood={mood} style={{ '--lx': look.x, '--ly': look.y }} aria-hidden="true">
      {CAST.map((name) => (
        <div key={name} className={`mate mate--${name}`}>
          <Face />
        </div>
      ))}
    </div>
  );
}

// every face a mate can wear, drawn once up front; mood picks what shows
function Face() {
  return (
    <svg className="face" viewBox="0 0 48 28">
      <g className="face__eyes is-open">
        <circle cx="14" cy="9" r="3.4" />
        <circle cx="34" cy="9" r="3.4" />
      </g>
      <g className="face__eyes is-shut">
        <path d="M10 11 q4 -6 8 0" />
        <path d="M30 11 q4 -6 8 0" />
      </g>
      <g className="face__eyes is-peek">
        <path d="M10 9 h8" />
        <circle cx="34" cy="9" r="3.6" />
      </g>
      <g className="face__mouth is-smile">
        <path d="M19 19 q5 4 10 0" />
      </g>
      <g className="face__mouth is-o">
        <ellipse cx="24" cy="20" rx="2.6" ry="3.2" />
      </g>
      <g className="face__mouth is-grin">
        <path d="M16.5 17.5 h15 q-7.5 10 -15 0z" />
      </g>
      <g className="face__mouth is-flat">
        <path d="M19 21 q5 -3 10 0" />
      </g>
    </svg>
  );
}
