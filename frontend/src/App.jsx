import { useCallback, useEffect, useState } from 'react';
import { shipmentsApi } from './api/shipmentsApi.js';
import Dashboard from './components/Dashboard.jsx';
import ShipmentList from './components/ShipmentList.jsx';
import ShipmentForm from './components/ShipmentForm.jsx';
import { STATUSES } from './constants.js';

export default function App() {
  const [shipments, setShipments] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [slow, setSlow] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [status, setStatus] = useState('');
  const [unassigned, setUnassigned] = useState(false);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState(null);

  // Wait for the person to stop typing before hitting the API.
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(t);
  }, [search]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    // Free hosting puts idle services to sleep; explain a long first load instead of looking broken.
    const slowTimer = setTimeout(() => setSlow(true), 4000);
    try {
      const [list, stats] = await Promise.all([
        shipmentsApi.list({ search: debouncedSearch, status, unassigned }),
        shipmentsApi.summary(),
      ]);
      setShipments(list);
      setSummary(stats);
    } catch (err) {
      setError(err.message);
    } finally {
      clearTimeout(slowTimer);
      setSlow(false);
      setLoading(false);
    }
  }, [debouncedSearch, status, unassigned]);

  useEffect(() => { load(); }, [load]);

  const flash = (message) => {
    setNotice(message);
    setTimeout(() => setNotice(''), 4000);
  };

  async function handleSave(data) {
    if (editing) {
      await shipmentsApi.update(editing.id, data);
      flash('Shipment updated.');
    } else {
      const created = await shipmentsApi.create(data);
      flash(`Shipment ${created.trackingNumber} created.`);
    }
    setFormOpen(false);
    setEditing(null);
    await load();
  }

  async function handleStatusChange(shipment, newStatus) {
    try {
      await shipmentsApi.setStatus(shipment.id, newStatus);
      flash(`${shipment.trackingNumber} marked ${STATUSES.find((s) => s.value === newStatus).label.toLowerCase()}.`);
      await load();
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleDelete(shipment) {
    if (!window.confirm(`Delete shipment ${shipment.trackingNumber}? This cannot be undone.`)) return;
    try {
      await shipmentsApi.remove(shipment.id);
      flash(`Shipment ${shipment.trackingNumber} deleted.`);
      await load();
    } catch (err) {
      setError(err.message);
    }
  }

  const hasFilters = Boolean(search || status || unassigned);
  const clearFilters = () => { setSearch(''); setStatus(''); setUnassigned(false); };

  return (
    <>
      <header className="topbar">
        <div className="brand">
          <h1>Waybill</h1>
          <p>Dispatch board</p>
        </div>
        <button type="button" className="btn btn-primary" onClick={() => { setEditing(null); setFormOpen(true); }}>
          New shipment
        </button>
      </header>

      <main>
        <Dashboard
          summary={summary}
          activeStatus={status}
          unassignedOnly={unassigned}
          onFilterStatus={setStatus}
          onToggleUnassigned={() => setUnassigned((u) => !u)}
        />

        <section className="toolbar" aria-label="Search and filter">
          <div className="field grow">
            <label htmlFor="search">Search</label>
            <input
              id="search"
              type="search"
              placeholder="Tracking number, customer, town or driver"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="statusFilter">Status</label>
            <select id="statusFilter" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">All statuses</option>
              {STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </div>
          {hasFilters && <button type="button" className="btn btn-quiet" onClick={clearFilters}>Clear filters</button>}
        </section>

        <div aria-live="polite">
          {notice && <p className="banner banner-ok">{notice}</p>}
          {slow && <p className="banner">Still connecting. The server may be waking up after a quiet period and can take up to a minute.</p>}
          {error && (
            <p className="banner banner-error" role="alert">
              {error} <button type="button" className="btn btn-quiet" onClick={load}>Try again</button>
            </p>
          )}
        </div>

        <ShipmentList
          shipments={shipments}
          loading={loading}
          hasFilters={hasFilters}
          onClearFilters={clearFilters}
          onEdit={(s) => { setEditing(s); setFormOpen(true); }}
          onDelete={handleDelete}
          onStatusChange={handleStatusChange}
        />
      </main>

      {formOpen && (
        <ShipmentForm
          key={editing?.id ?? 'new'}
          shipment={editing}
          onSave={handleSave}
          onClose={() => { setFormOpen(false); setEditing(null); }}
        />
      )}
    </>
  );
}
