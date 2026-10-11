import { STATUSES, statusLabel } from '../constants.js';

const JOURNEY = ['Pending', 'InTransit', 'OutForDelivery', 'Delivered'];

/**
 * The pipeline bar is the heart of the board: segment width follows how many shipments sit at each
 * stage, and each segment filters the table below when clicked.
 */
export default function Dashboard({ summary, activeStatus, unassignedOnly, onFilterStatus, onToggleUnassigned }) {
  if (!summary) return <section className="dashboard" aria-busy="true" aria-label="Dashboard loading" />;

  const exceptions = STATUSES.filter((s) => !JOURNEY.includes(s.value));

  return (
    <section className="dashboard" aria-label="Shipment overview">
      <div className="dashboard-head">
        <h2>{summary.total} shipments on the board</h2>
        <div className="alerts">
          <span
            className={`alert-chip ${summary.overdue ? 'is-hot' : ''}`}
            title="Open shipments past their estimated delivery date"
          >
            <strong>{summary.overdue}</strong> overdue
          </span>
          <button
            type="button"
            className={`alert-chip ${summary.unassigned ? 'is-warn' : ''} ${unassignedOnly ? 'is-active' : ''}`}
            onClick={onToggleUnassigned}
            aria-pressed={unassignedOnly}
          >
            <strong>{summary.unassigned}</strong> need a driver
          </button>
        </div>
      </div>

      <div className="pipeline" role="group" aria-label="Filter by delivery stage">
        {JOURNEY.map((status) => {
          const count = summary.byStatus[status] ?? 0;
          return (
            <button
              key={status}
              type="button"
              className={`stage stage-${status} ${activeStatus === status ? 'is-active' : ''}`}
              style={{ flexGrow: Math.max(count, 0.6) }}
              onClick={() => onFilterStatus(activeStatus === status ? '' : status)}
              aria-pressed={activeStatus === status}
            >
              <span className="stage-count">{count}</span>
              <span className="stage-name">{statusLabel(status)}</span>
            </button>
          );
        })}
      </div>

      <div className="exceptions">
        {exceptions.map((s) => (
          <button
            key={s.value}
            type="button"
            className={`exception exception-${s.value} ${activeStatus === s.value ? 'is-active' : ''}`}
            onClick={() => onFilterStatus(activeStatus === s.value ? '' : s.value)}
            aria-pressed={activeStatus === s.value}
          >
            <strong>{summary.byStatus[s.value] ?? 0}</strong> {s.label.toLowerCase()}
          </button>
        ))}
      </div>
    </section>
  );
}
