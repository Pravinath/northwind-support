import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { LoginPage } from './LoginPage';
import type { MemoryRouterProps } from 'react-router-dom';

const { login } = vi.hoisted(() => ({ login: vi.fn() }));
vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({ agent: null, isLoading: false, login, logout: vi.fn() }),
}));

function renderLogin(initialEntry: NonNullable<MemoryRouterProps['initialEntries']>[number] = '/login') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/tickets/new" element={<p>Requested page restored</p>} />
        <Route path="/tickets" element={<p>Ticket list</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  beforeEach(() => vi.resetAllMocks());

  it('validates email and password before calling the login API', async () => {
    renderLogin();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Enter a valid email address and password.');
    expect(login).not.toHaveBeenCalled();
  });

  it('shows a clear error for rejected credentials', async () => {
    login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    renderLogin();
    fireEvent.change(screen.getByLabelText('Work email'), { target: { value: 'agent@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'incorrect' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
  });

  it('returns to the originally requested page after successful login', async () => {
    login.mockResolvedValue(undefined);
    renderLogin({ pathname: '/login', state: { from: '/tickets/new' } });
    fireEvent.change(screen.getByLabelText('Work email'), { target: { value: 'agent@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Requested page restored')).toBeInTheDocument();
  });

  it('lets the agent reveal and hide the password', () => {
    renderLogin();
    const password = screen.getByLabelText('Password');

    expect(password).toHaveAttribute('type', 'password');
    fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(password).toHaveAttribute('type', 'text');
    fireEvent.click(screen.getByRole('button', { name: 'Hide password' }));
    expect(password).toHaveAttribute('type', 'password');
  });
});
