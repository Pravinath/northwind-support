import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearAccessToken, request, setAccessToken } from './client';

describe('API client authentication', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => vi.unstubAllGlobals());

  it('attaches the session token to protected API requests', async () => {
    setAccessToken('signed-token');
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify([]), { status: 200 }));

    await request<unknown[]>('/tickets');

    const init = vi.mocked(fetch).mock.calls[0]?.[1];
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer signed-token');
  });

  it('clears an expired token and signals the app to return to login on 401', async () => {
    setAccessToken('expired-token');
    const unauthorized = vi.fn();
    window.addEventListener('northwind-support:unauthorized', unauthorized);
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ detail: 'Token expired.' }), { status: 401 }));

    await expect(request('/tickets')).rejects.toThrow('Token expired.');

    expect(clearAccessToken).toBeDefined();
    expect(sessionStorage.getItem('northwind-support.access-token')).toBeNull();
    expect(unauthorized).toHaveBeenCalledTimes(1);
    window.removeEventListener('northwind-support:unauthorized', unauthorized);
  });
});
