import React, { useState, useEffect, useCallback } from 'react';
import {
  Search,
  Filter,
  Eye,
  Lock,
  Unlock,
  UserCog,
  ChevronLeft,
  ChevronRight,
  AlertCircle,
  CheckCircle2,
  Users,
  RotateCw,
} from 'lucide-react';
import { getAdminUsers, getAdminUserDetails, activateUser, deactivateUser, changeUserRole } from '../../services/adminService';
import { formatAdminDate, getUserStatusBadgeConfig, getUserRoleBadgeConfig, canAdminDeactivateUser } from '../../utils/adminDashboardUtils';
import { UserDetailsModal } from './UserDetailsModal';
import { ConfirmActionModal } from './ConfirmActionModal';

export function UserManagementSection({ token, currentAdminId, onUserMutated }) {
  const [users, setUsers] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('All');
  const [statusFilter, setStatusFilter] = useState('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Modals & Feedback
  const [selectedUserDetails, setSelectedUserDetails] = useState(null);
  const [loadingDetailsId, setLoadingDetailsId] = useState(null);
  const [actionModal, setActionModal] = useState(null); // { type, user }
  const [feedback, setFeedback] = useState(null); // { type: 'success' | 'danger', message }

  // Search debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
      setPage(1); // reset to page 1 on new search
    }, 350);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Load users list
  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAdminUsers(
        {
          q: debouncedSearch,
          role: roleFilter,
          status: statusFilter,
          page,
          pageSize,
        },
        token
      );

      setUsers(Array.isArray(data?.items) ? data.items : []);
      setTotalCount(typeof data?.totalCount === 'number' ? data.totalCount : 0);
      setTotalPages(typeof data?.totalPages === 'number' ? Math.max(1, data.totalPages) : 1);
    } catch (err) {
      setError(err.message || 'Unable to load registered users. Please check your network connection.');
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, roleFilter, statusFilter, page, pageSize, token]);

  useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  // Open Details Modal
  const handleOpenDetails = async (user) => {
    setLoadingDetailsId(user.id);
    try {
      const details = await getAdminUserDetails(user.id, token);
      setSelectedUserDetails(details);
    } catch {
      // Fallback to table item if detail fetch fails
      setSelectedUserDetails(user);
    } finally {
      setLoadingDetailsId(null);
    }
  };

  // Execute Action from Modal
  const handleConfirmAction = async ({ type, userId, role, reason }) => {
    if (type === 'activate') {
      const res = await activateUser(userId, reason, token);
      setFeedback({
        type: 'success',
        message: res.message || 'Account activated successfully.',
      });
    } else if (type === 'deactivate') {
      const res = await deactivateUser(userId, reason, token);
      setFeedback({
        type: 'success',
        message: res.message || 'Account deactivated successfully.',
      });
    } else if (type === 'role') {
      const res = await changeUserRole(userId, role, reason, token);
      setFeedback({
        type: 'success',
        message: res.message || `User role updated to ${role} successfully.`,
      });
    }

    // Refresh directory & parent overview metrics
    await loadUsers();
    if (typeof onUserMutated === 'function') {
      onUserMutated();
    }
  };

  return (
    <div
      data-testid="admin-user-management-section"
      style={{
        backgroundColor: '#ffffff',
        borderRadius: 'var(--ep-radius-card)',
        border: '1px solid var(--ep-border)',
        boxShadow: 'var(--ep-shadow-card)',
        padding: '28px',
        marginBottom: '32px',
      }}
    >
      {/* Title & Refresh */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '20px',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <Users size={22} color="var(--ep-primary)" />
          <h2 style={{ fontSize: '20px', fontWeight: 700, margin: 0, color: 'var(--ep-text-primary)' }}>
            User Management Directory
          </h2>
          <span
            data-testid="user-total-badge"
            style={{
              fontSize: '12px',
              fontWeight: 700,
              backgroundColor: 'var(--ep-canvas)',
              border: '1px solid var(--ep-border)',
              borderRadius: 'var(--ep-radius-pill)',
              padding: '2px 10px',
              color: 'var(--ep-text-secondary)',
            }}
          >
            {totalCount} Total
          </span>
        </div>

        <button
          type="button"
          onClick={loadUsers}
          disabled={loading}
          data-testid="refresh-users-btn"
          className="ep-btn-secondary"
          style={{
            fontSize: '13px',
            padding: '7px 14px',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
          }}
        >
          <RotateCw size={14} className={loading ? 'spin' : ''} />
          <span>Refresh</span>
        </button>
      </div>

      {/* Success/Error Feedback Toast */}
      {feedback && (
        <div
          data-testid="admin-feedback-banner"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 16px',
            borderRadius: 'var(--ep-radius-card)',
            marginBottom: '20px',
            backgroundColor: feedback.type === 'success' ? '#E8F5E9' : '#FFF0EF',
            color: feedback.type === 'success' ? '#2E7D32' : 'var(--ep-danger)',
            border: `1px solid ${feedback.type === 'success' ? '#C8E6C9' : '#FFCDD2'}`,
            fontSize: '14px',
            fontWeight: 500,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <CheckCircle2 size={16} />
            <span>{feedback.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedback(null)}
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'inherit', fontWeight: 700 }}
          >
            ×
          </button>
        </div>
      )}

      {/* Filter and Search Bar */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          marginBottom: '20px',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        {/* Search */}
        <div style={{ position: 'relative', flex: '1 1 260px', maxWidth: '400px' }}>
          <Search
            size={16}
            color="var(--ep-text-secondary)"
            style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }}
          />
          <input
            type="text"
            id="admin-user-search-input"
            data-testid="admin-user-search-input"
            placeholder="Search by name or email..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              width: '100%',
              padding: '9px 12px 9px 36px',
              borderRadius: 'var(--ep-radius-pill)',
              border: '1px solid var(--ep-border)',
              fontSize: '13px',
              boxSizing: 'border-box',
            }}
          />
        </div>

        {/* Filters */}
        <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Filter size={14} color="var(--ep-text-secondary)" />
            <label htmlFor="admin-role-filter-select" style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>
              Role:
            </label>
            <select
              id="admin-role-filter-select"
              data-testid="admin-role-filter-select"
              value={roleFilter}
              onChange={(e) => {
                setRoleFilter(e.target.value);
                setPage(1);
              }}
              style={{
                padding: '6px 12px',
                borderRadius: 'var(--ep-radius-pill)',
                border: '1px solid var(--ep-border)',
                fontSize: '13px',
                backgroundColor: '#ffffff',
              }}
            >
              <option value="All">All Roles</option>
              <option value="Customer">Customer</option>
              <option value="Organizer">Organizer</option>
              <option value="Administrator">Administrator</option>
            </select>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <label htmlFor="admin-status-filter-select" style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>
              Status:
            </label>
            <select
              id="admin-status-filter-select"
              data-testid="admin-status-filter-select"
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              style={{
                padding: '6px 12px',
                borderRadius: 'var(--ep-radius-pill)',
                border: '1px solid var(--ep-border)',
                fontSize: '13px',
                backgroundColor: '#ffffff',
              }}
            >
              <option value="All">All Statuses</option>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
              <option value="Unverified">Unverified Email</option>
            </select>
          </div>
        </div>
      </div>

      {/* Directory Table Area */}
      {loading ? (
        <div
          data-testid="admin-users-loading-state"
          style={{ padding: '64px 16px', textAlign: 'center', color: 'var(--ep-text-secondary)' }}
        >
          <div className="spinner-border text-primary" role="status" style={{ width: '2.5rem', height: '2.5rem', marginBottom: '12px' }}>
            <span className="visually-hidden">Loading users...</span>
          </div>
          <p style={{ margin: 0, fontSize: '14px', fontWeight: 500 }}>Loading registered accounts directory...</p>
        </div>
      ) : error ? (
        <div
          data-testid="admin-users-error-state"
          style={{
            padding: '36px 16px',
            textAlign: 'center',
            backgroundColor: '#FFF0EF',
            border: '1px solid #FFCDD2',
            borderRadius: 'var(--ep-radius-card)',
            color: 'var(--ep-danger)',
          }}
        >
          <AlertCircle size={32} style={{ marginBottom: '8px' }} />
          <p style={{ margin: '0 0 16px 0', fontSize: '14px', fontWeight: 600 }}>{error}</p>
          <button type="button" className="ep-btn-secondary" onClick={loadUsers} style={{ fontSize: '13px', padding: '6px 16px' }}>
            Try Again
          </button>
        </div>
      ) : users.length === 0 ? (
        <div
          data-testid="admin-users-empty-state"
          style={{
            padding: '48px 16px',
            textAlign: 'center',
            backgroundColor: 'var(--ep-canvas)',
            borderRadius: 'var(--ep-radius-card)',
            border: '1px dashed var(--ep-border)',
            color: 'var(--ep-text-secondary)',
          }}
        >
          <Users size={36} color="var(--ep-text-secondary)" style={{ marginBottom: '10px' }} />
          <h3 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 6px 0', color: 'var(--ep-text-primary)' }}>
            No user accounts found
          </h3>
          <p style={{ margin: 0, fontSize: '13px' }}>
            No accounts match the current filter or search criteria.
          </p>
        </div>
      ) : (
        <div style={{ overflowX: 'auto', border: '1px solid var(--ep-border)', borderRadius: 'var(--ep-radius-card)' }}>
          <table
            data-testid="admin-users-table"
            style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}
          >
            <thead>
              <tr style={{ backgroundColor: 'var(--ep-canvas)', borderBottom: '1px solid var(--ep-border)' }}>
                <th style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>User</th>
                <th style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Role</th>
                <th style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Status</th>
                <th style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ep-text-secondary)' }}>Registered</th>
                <th style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ep-text-secondary)', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => {
                const statusBadge = getUserStatusBadgeConfig(user.isActive);
                const roleBadge = getUserRoleBadgeConfig(user.role);
                const deactSafety = canAdminDeactivateUser(user, currentAdminId);
                const isSelf = currentAdminId && String(user.id).toLowerCase() === String(currentAdminId).toLowerCase();

                return (
                  <tr
                    key={user.id}
                    data-testid={`admin-user-row-${user.id}`}
                    style={{ borderBottom: '1px solid var(--ep-border)', transition: 'background-color 150ms' }}
                  >
                    {/* User Name & Email */}
                    <td style={{ padding: '12px 16px' }}>
                      <div style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
                        {user.fullName || `${user.firstName} ${user.lastName}`.trim() || '—'}
                        {isSelf && (
                          <span
                            style={{
                              marginLeft: '6px',
                              fontSize: '11px',
                              backgroundColor: '#E0F2FE',
                              color: '#0369A1',
                              padding: '1px 6px',
                              borderRadius: 'var(--ep-radius-pill)',
                              fontWeight: 700,
                            }}
                          >
                            You
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--ep-text-secondary)' }}>
                        {user.email}
                      </div>
                    </td>

                    {/* Role Badge */}
                    <td style={{ padding: '12px 16px' }}>
                      <span
                        data-testid={`role-badge-${user.id}`}
                        style={{
                          display: 'inline-block',
                          backgroundColor: roleBadge.bg,
                          color: roleBadge.color,
                          border: `1px solid ${roleBadge.border}`,
                          padding: '2px 8px',
                          borderRadius: 'var(--ep-radius-pill)',
                          fontSize: '11px',
                          fontWeight: 700,
                        }}
                      >
                        {roleBadge.label}
                      </span>
                    </td>

                    {/* Status Badge */}
                    <td style={{ padding: '12px 16px' }}>
                      <span
                        data-testid={`status-badge-${user.id}`}
                        style={{
                          display: 'inline-block',
                          backgroundColor: statusBadge.bg,
                          color: statusBadge.color,
                          border: `1px solid ${statusBadge.border}`,
                          padding: '2px 8px',
                          borderRadius: 'var(--ep-radius-pill)',
                          fontSize: '11px',
                          fontWeight: 700,
                        }}
                      >
                        {statusBadge.label}
                      </span>
                    </td>

                    {/* Registered Date */}
                    <td style={{ padding: '12px 16px', color: 'var(--ep-text-secondary)' }}>
                      {formatAdminDate(user.createdAt, false)}
                    </td>

                    {/* Actions */}
                    <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: '6px', alignItems: 'center' }}>
                        {/* View Details */}
                        <button
                          type="button"
                          data-testid={`view-user-btn-${user.id}`}
                          onClick={() => handleOpenDetails(user)}
                          disabled={loadingDetailsId === user.id}
                          className="ep-btn-secondary"
                          title="View account operational details"
                          style={{ padding: '5px 8px', fontSize: '12px', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                        >
                          <Eye size={13} />
                          <span>Details</span>
                        </button>

                        {/* Change Role */}
                        <button
                          type="button"
                          data-testid={`role-user-btn-${user.id}`}
                          onClick={() => setActionModal({ type: 'role', user })}
                          className="ep-btn-secondary"
                          title="Change user role permissions"
                          style={{ padding: '5px 8px', fontSize: '12px', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                        >
                          <UserCog size={13} />
                          <span>Role</span>
                        </button>

                        {/* Activate / Deactivate */}
                        {user.isActive ? (
                          <button
                            type="button"
                            data-testid={`deactivate-user-btn-${user.id}`}
                            onClick={() => setActionModal({ type: 'deactivate', user })}
                            disabled={!deactSafety.allowed}
                            title={deactSafety.message || 'Deactivate user account'}
                            style={{
                              padding: '5px 8px',
                              fontSize: '12px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px',
                              backgroundColor: deactSafety.allowed ? '#FFF0EF' : '#F5F5F5',
                              color: deactSafety.allowed ? 'var(--ep-danger)' : '#BDBDBD',
                              border: `1px solid ${deactSafety.allowed ? '#FFCDD2' : '#E0E0E0'}`,
                              borderRadius: 'var(--ep-radius-btn)',
                              cursor: deactSafety.allowed ? 'pointer' : 'not-allowed',
                            }}
                          >
                            <Lock size={13} />
                            <span>Deactivate</span>
                          </button>
                        ) : (
                          <button
                            type="button"
                            data-testid={`activate-user-btn-${user.id}`}
                            onClick={() => setActionModal({ type: 'activate', user })}
                            title="Activate user account"
                            style={{
                              padding: '5px 8px',
                              fontSize: '12px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px',
                              backgroundColor: '#E8F5E9',
                              color: '#2E7D32',
                              border: '1px solid #C8E6C9',
                              borderRadius: 'var(--ep-radius-btn)',
                              cursor: 'pointer',
                            }}
                          >
                            <Unlock size={13} />
                            <span>Activate</span>
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination Footer */}
      {!loading && !error && users.length > 0 && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginTop: '20px',
            flexWrap: 'wrap',
            gap: '12px',
            fontSize: '13px',
            color: 'var(--ep-text-secondary)',
          }}
        >
          <div>
            Showing <strong>{(page - 1) * pageSize + 1}</strong> to{' '}
            <strong>{Math.min(page * pageSize, totalCount)}</strong> of <strong>{totalCount}</strong> users
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <label htmlFor="admin-page-size-select" style={{ fontSize: '13px', color: 'var(--ep-text-secondary)', marginRight: '4px' }}>
              Page size:
            </label>
            <select
              id="admin-page-size-select"
              data-testid="admin-page-size-select"
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
              style={{
                padding: '4px 8px',
                borderRadius: 'var(--ep-radius-btn)',
                border: '1px solid var(--ep-border)',
                backgroundColor: '#ffffff',
                fontSize: '12px',
              }}
            >
              <option value="10">10</option>
              <option value="20">20</option>
              <option value="50">50</option>
            </select>

            <button
              type="button"
              data-testid="admin-pagination-prev"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="ep-btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center' }}
            >
              <ChevronLeft size={14} />
            </button>

            <span style={{ fontWeight: 600, color: 'var(--ep-text-primary)' }}>
              Page {page} of {totalPages}
            </span>

            <button
              type="button"
              data-testid="admin-pagination-next"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="ep-btn-secondary"
              style={{ padding: '6px 10px', fontSize: '12px', display: 'flex', alignItems: 'center' }}
            >
              <ChevronRight size={14} />
            </button>
          </div>
        </div>
      )}

      {/* User Details Modal */}
      {selectedUserDetails && (
        <UserDetailsModal
          user={selectedUserDetails}
          onClose={() => setSelectedUserDetails(null)}
        />
      )}

      {/* Confirmation Action Modal */}
      {actionModal && (
        <ConfirmActionModal
          action={actionModal}
          currentAdminId={currentAdminId}
          onConfirm={handleConfirmAction}
          onClose={() => setActionModal(null)}
        />
      )}
    </div>
  );
}
