import { STATUSES, formatDate } from '../constants.js';
import StatusBadge from './StatusBadge.jsx';

const CLOSED = ['Delivered', 'Cancelled'];

export default function ShipmentList({ shipments, loading, onEdit, onDelete, onStatusChange, hasFilters, onClearFilters }) {
  if (!loading && shipments.length === 0) {
    return (
      <div className="empty">
        <h3>{hasFilters ? 'No shipments match these filters' : 'No shipments yet'}</h3>
        <p>{hasFilters ? 'Try a different search term or clear the filters.' : 'Add your first shipment to start tracking deliveries.'}</p>
        {hasFilters && (
          <button type="button" className="btn btn-quiet" onClick={onClearFilters}>
            Clear filters
          </button>
        )}
      </div>
    );
  }

  const today = new Date().toISOString().slice(0, 10);

  return (
    <div className="table-wrap" aria-busy={loading}>
      <table>
        <thead>
          <tr>
            <th scope="col">Tracking no.</th>
            <th scope="col">Customer</th>
            <th scope="col">Route</th>
            <th scope="col">Driver</th>
            <th scope="col">Dispatched</th>
            <th scope="col">Due</th>
            <th scope="col">Status</th>
            <th scope="col" className="num">Weight</th>
            <th scope="col"><span className="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          {shipments.map((s) => {
            const overdue = !CLOSED.includes(s.status) && s.estimatedDelivery && s.estimatedDelivery.slice(0, 10) < today;
            return (
              <tr key={s.id}>
                <td className="tracking">{s.trackingNumber}</td>
                <td>{s.customerName}</td>
                <td>
                  {s.origin} <span className="arrow" aria-label="to">to</span> {s.destination}
                </td>
                <td>
                  {s.driverName ? (
                    <>
                      {s.driverName}
                      {s.vehicleRegistration && <small className="sub">{s.vehicleRegistration}</small>}
                    </>
                  ) : CLOSED.includes(s.status) ? (
                    <span className="muted">-</span>
                  ) : (
                    <span className="unassigned">Unassigned</span>
                  )}
                </td>
                <td>{formatDate(s.dispatchDate)}</td>
                <td className={overdue ? 'overdue' : ''}>
                  {formatDate(s.estimatedDelivery)}
                  {overdue && <small className="sub">Overdue</small>}
                </td>
                <td>
                  <StatusBadge status={s.status} />
                  <select
                    className="status-select"
                    aria-label={`Change status of ${s.trackingNumber}`}
                    value={s.status}
                    onChange={(e) => onStatusChange(s, e.target.value)}
                  >
                    {STATUSES.map((st) => (
                      <option key={st.value} value={st.value}>{st.label}</option>
                    ))}
                  </select>
                </td>
                <td className="num">{Number(s.weightKg).toLocaleString('en-ZA')} kg</td>
                <td className="actions">
                  <button type="button" className="btn btn-quiet" onClick={() => onEdit(s)}>Edit</button>
                  <button type="button" className="btn btn-quiet btn-danger" onClick={() => onDelete(s)}>Delete</button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
