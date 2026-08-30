import { HttpInterceptorFn } from '@angular/common/http';

// In the Codespace the dev proxy forwards /api to localhost:5190, so the
// relative path works as-is. Once deployed there is no proxy, and the API
// lives on a different host entirely.
const API_BASE = location.hostname.includes('localhost') || location.hostname.includes('github.dev')
  ? ''
  : 'https://narera-api-g3effxeegcenaqag.centralindia-01.azurewebsites.net';

export const apiBaseInterceptor: HttpInterceptorFn = (req, next) => {
  if (!API_BASE || !req.url.startsWith('/api')) {
    return next(req);
  }
  return next(req.clone({ url: API_BASE + req.url }));
};