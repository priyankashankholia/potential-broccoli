import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { LoginResponse } from '../models/auth';

const TOKEN_KEY = 'rentManager.token';
const NAME_KEY = 'rentManager.displayName';

@Injectable({ providedIn: 'root' })
export class AuthService {

  private readonly http = inject(HttpClient);

  // Kept in localStorage so a refresh does not log the landlord out.
  private readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  readonly displayName = signal<string>(
    localStorage.getItem(NAME_KEY) ?? 'Landlord'
  );

  readonly isAuthenticated = computed(() => this.token() !== null);

  getToken(): string | null {
    return this.token();
  }

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/auth/login', { username, password })
      .pipe(
        tap(response => {
          localStorage.setItem(TOKEN_KEY, response.token);
          localStorage.setItem(NAME_KEY, response.displayName || 'Landlord');

          this.token.set(response.token);
          this.displayName.set(response.displayName || 'Landlord');
        })
      );
  }

  // A JWT cannot be revoked from the client, so logging out means dropping
  // it. The server call just records the event.
  logout(): void {
    const hadToken = this.token() !== null;

    this.clearSession();

    if (hadToken) {
      this.http.post('/api/auth/logout', {}).subscribe({
        next: () => undefined,
        error: () => undefined
      });
    }
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/auth/change-password', {
      currentPassword,
      newPassword
    });
  }

  clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(NAME_KEY);

    this.token.set(null);
  }
}
