import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../api/shipmentsApi.js';
import { STATUSES, toDateInput } from '../constants.js';

const EMPTY = {
  customerName: '',
  origin: '',
  destination: '',
  status: 'Pending',
  dispatchDate: new Date().toISOString().slice(0, 10),
  estimatedDelivery: '',
  driverName: '',
  vehicleRegistration: '',
  weightKg: '',
  notes: '',
};

// Client-side checks mirror the API's rules so people get instant feedback.
// The server still validates everything - these are a convenience, not a security control.
function validate(v) {
  const e = {};
  const len = (s) => s.trim().length;
  if (len(v.customerName) < 2) e.customerName = 'Enter the customer name (at least 2 characters).';
  if (len(v.origin) < 2) e.origin = 'Enter where the shipment is collected from.';
  if (len(v.destination) < 2) e.destination = 'Enter where the shipment is going.';
  if (!v.dispatchDate) e.dispatchDate = 'Choose a dispatch date.';
  if (v.dispatchDate && v.estimatedDelivery && v.estimatedDelivery < v.dispatchDate)
    e.estimatedDelivery = 'Estimated delivery cannot be before the dispatch date.';
  const kg = Number(v.weightKg);
  if (!v.weightKg || Number.isNaN(kg) || kg < 0.1 || kg > 100000) e.weightKg = 'Enter a weight between 0.1 and 100,000 kg.';
  if (v.driverName.length > 80) e.driverName = 'Driver name must be 80 characters or fewer.';
  if (v.vehicleRegistration.length > 20) e.vehicleRegistration = 'Vehicle registration must be 20 characters or fewer.';
  return e;
}

function Field({ id, label, error, hint, children }) {
  return (
    <div className={`field ${error ? 'has-error' : ''}`}>
      <label htmlFor={id}>{label}</label>
      {children}
      {hint && !error && <p className="hint">{hint}</p>}
      {error && <p className="error" id={`${id}-error`} role="alert">{error}</p>}
    </div>
  );
}

export default function ShipmentForm({ shipment, onSave, onClose }) {
  const editing = Boolean(shipment);
  const [values, setValues] = useState(() =>
    shipment
      ? {
          ...EMPTY,
          ...shipment,
          dispatchDate: toDateInput(shipment.dispatchDate),
          estimatedDelivery: toDateInput(shipment.estimatedDelivery),
          driverName: shipment.driverName ?? '',
          vehicleRegistration: shipment.vehicleRegistration ?? '',
          notes: shipment.notes ?? '',
          weightKg: String(shipment.weightKg),
        }
      : EMPTY
  );
  const [errors, setErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);
  const firstField = useRef(null);

  useEffect(() => {
    firstField.current?.focus();
    const onKey = (e) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const set = (name) => (e) => setValues((v) => ({ ...v, [name]: e.target.value }));
  const props = (name) => ({
    id: name,
    value: values[name],
    onChange: set(name),
    'aria-invalid': Boolean(errors[name]),
    'aria-describedby': errors[name] ? `${name}-error` : undefined,
  });

  async function submit(e) {
    e.preventDefault();
    setFormError('');
    const found = validate(values);
    setErrors(found);
    if (Object.keys(found).length) return;

    setSaving(true);
    try {
      await onSave({
        customerName: values.customerName.trim(),
        origin: values.origin.trim(),
        destination: values.destination.trim(),
        status: values.status,
        dispatchDate: `${values.dispatchDate}T00:00:00Z`,
        estimatedDelivery: values.estimatedDelivery ? `${values.estimatedDelivery}T00:00:00Z` : null,
        driverName: values.driverName.trim() || null,
        vehicleRegistration: values.vehicleRegistration.trim() || null,
        weightKg: Number(values.weightKg),
        notes: values.notes.trim() || null,
      });
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length) setErrors(err.fieldErrors);
      setFormError(err.message);
      setSaving(false);
    }
  }

  return (
    <div className="scrim" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <aside className="panel" role="dialog" aria-modal="true" aria-labelledby="form-title">
        <header className="panel-head">
          <div>
            <h2 id="form-title">{editing ? 'Edit shipment' : 'New shipment'}</h2>
            {editing && <p className="tracking">{shipment.trackingNumber}</p>}
          </div>
          <button type="button" className="btn btn-quiet" onClick={onClose}>Close</button>
        </header>

        <form onSubmit={submit} noValidate>
          {formError && <p className="banner banner-error" role="alert">{formError}</p>}

          <Field id="customerName" label="Customer" error={errors.customerName}>
            <input ref={firstField} type="text" maxLength={120} {...props('customerName')} />
          </Field>

          <div className="row">
            <Field id="origin" label="Collect from" error={errors.origin}>
              <input type="text" maxLength={200} {...props('origin')} />
            </Field>
            <Field id="destination" label="Deliver to" error={errors.destination}>
              <input type="text" maxLength={200} {...props('destination')} />
            </Field>
          </div>

          <div className="row">
            <Field id="dispatchDate" label="Dispatch date" error={errors.dispatchDate}>
              <input type="date" {...props('dispatchDate')} />
            </Field>
            <Field id="estimatedDelivery" label="Estimated delivery" error={errors.estimatedDelivery}>
              <input type="date" {...props('estimatedDelivery')} />
            </Field>
          </div>

          <div className="row">
            <Field id="status" label="Status">
              <select {...props('status')}>
                {STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </Field>
            <Field id="weightKg" label="Weight (kg)" error={errors.weightKg}>
              <input type="number" inputMode="decimal" step="0.1" min="0" {...props('weightKg')} />
            </Field>
          </div>

          <div className="row">
            <Field id="driverName" label="Driver" error={errors.driverName} hint="Leave blank if not yet allocated.">
              <input type="text" maxLength={80} {...props('driverName')} />
            </Field>
            <Field id="vehicleRegistration" label="Vehicle registration" error={errors.vehicleRegistration}>
              <input type="text" maxLength={20} {...props('vehicleRegistration')} />
            </Field>
          </div>

          <Field id="notes" label="Notes" error={errors.notes}>
            <textarea rows={3} maxLength={1000} {...props('notes')} />
          </Field>

          <footer className="panel-foot">
            <button type="button" className="btn btn-quiet" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Saving...' : editing ? 'Save changes' : 'Create shipment'}
            </button>
          </footer>
        </form>
      </aside>
    </div>
  );
}
