// Order matters: the first four are the forward journey shown on the dashboard pipeline.
export const STATUSES = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InTransit', label: 'In transit' },
  { value: 'OutForDelivery', label: 'Out for delivery' },
  { value: 'Delivered', label: 'Delivered' },
  { value: 'Delayed', label: 'Delayed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export const statusLabel = (value) => STATUSES.find((s) => s.value === value)?.label ?? value;

export const formatDate = (iso) =>
  iso ? new Date(iso).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }) : '-';

export const toDateInput = (iso) => (iso ? iso.slice(0, 10) : '');
