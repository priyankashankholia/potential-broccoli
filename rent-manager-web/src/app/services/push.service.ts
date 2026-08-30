import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PushService {

  private readonly http = inject(HttpClient);

  readonly supported = 'serviceWorker' in navigator && 'PushManager' in window;
  readonly enabled = signal(false);
  readonly busy = signal(false);

  // Called after login so the button shows the right state on reload.
  async refresh(): Promise<void> {
    if (!this.supported) {
      return;
    }

    try {
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.getSubscription();

      this.enabled.set(sub !== null);
    } catch {
      this.enabled.set(false);
    }
  }

  // Returns an error message, or null on success.
  async enable(): Promise<string | null> {
    if (!this.supported) {
      return 'This browser does not support notifications.';
    }

    this.busy.set(true);

    try {
      const permission = await Notification.requestPermission();

      if (permission !== 'granted') {
        // Once denied the browser will not ask again; it has to be changed
        // in site settings.
        return 'Notifications were blocked. Turn them on in your browser settings for this site.';
      }

      const { publicKey } = await firstValueFrom(
        this.http.get<{ publicKey: string }>('/api/push/key')
      );

      const reg = await navigator.serviceWorker.ready;

      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: this.toUint8(publicKey)
      });

      const json = sub.toJSON();

      await firstValueFrom(this.http.post('/api/push/subscribe', {
        endpoint: sub.endpoint,
        p256dh: json.keys?.['p256dh'],
        auth: json.keys?.['auth']
      }));

      this.enabled.set(true);

      return null;

    } catch {
      return 'Could not turn on notifications. Please try again.';
    } finally {
      this.busy.set(false);
    }
  }

  async disable(): Promise<void> {
    if (!this.supported) {
      return;
    }

    this.busy.set(true);

    try {
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.getSubscription();

      if (!sub) {
        this.enabled.set(false);
        return;
      }

      await firstValueFrom(
        this.http.post('/api/push/unsubscribe', { endpoint: sub.endpoint })
      );

      await sub.unsubscribe();

      this.enabled.set(false);
    } catch {
      // Leave the flag alone; refresh() will correct it next load.
    } finally {
      this.busy.set(false);
    }
  }

  // The VAPID key arrives base64url encoded; the browser wants raw bytes.
  private toUint8(base64: string): Uint8Array {
    const padded = (base64 + '='.repeat((4 - base64.length % 4) % 4))
      .replace(/-/g, '+')
      .replace(/_/g, '/');

    const raw = atob(padded);

    return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
  }
}