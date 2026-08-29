import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

// Attaches the token to every API call so no component builds headers by
// hand. A 401 drops the stored token, which flips the app back to the
// login screen on its own.
export const authInterceptor: HttpInterceptorFn = (request, next) => {

  const auth = inject(AuthService);
  const token = auth.getToken();

  const isLogin = request.url.includes('/api/auth/login');

  const authorised = token && !isLogin
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorised).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isLogin) {
        auth.clearSession();
      }

      return throwError(() => error);
    })
  );
};
