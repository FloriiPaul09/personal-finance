import { Component, inject, signal } from '@angular/core';
import { form, FormField, minLength, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Notifier, SessionStore } from '../../../core';
import { apiFormErrors } from '../../../core/forms';

/** Minimum length of a password, as the API requires (docs/backend/security.md). */
export const PASSWORD_MIN_LENGTH = 12;

const EMPTY_PASSWORDS = { currentPassword: '', newPassword: '', confirmation: '' };

@Component({
  selector: 'app-settings-page',
  imports: [FormField, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  private readonly session = inject(SessionStore);
  private readonly notifier = inject(Notifier);

  protected readonly user = this.session.user;
  protected readonly loggingOut = signal(false);
  protected readonly passwords = signal({ ...EMPTY_PASSWORDS });
  protected readonly passwordForm = form(this.passwords, (path) => {
    required(path.currentPassword, {
      message: $localize`:Password form error:Inserisci la password attuale.`,
    });
    required(path.newPassword, {
      message: $localize`:Password form error:Inserisci la nuova password.`,
    });
    minLength(path.newPassword, PASSWORD_MIN_LENGTH, {
      message: $localize`:Password form error:Usa almeno ${PASSWORD_MIN_LENGTH}:count: caratteri.`,
    });
    validate(path.confirmation, ({ value, valueOf }) =>
      value() === valueOf(path.newPassword)
        ? undefined
        : {
            kind: 'mismatch',
            message: $localize`:Password form error:Le due password non coincidono.`,
          },
    );
  });

  protected async changePassword(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.passwordForm, async () => {
      const { currentPassword, newPassword } = this.passwords();
      try {
        await this.session.changePassword({ currentPassword, newPassword });
      } catch (error) {
        const errors = apiFormErrors(this.passwordForm, error, {
          fieldsByCode: {
            'auth.password_incorrect': 'currentPassword',
            'auth.password_rejected': 'newPassword',
          },
        });
        if (errors) {
          return errors;
        }
        throw error;
      }
      this.passwordForm().reset({ ...EMPTY_PASSWORDS });
      void this.notifier.show($localize`:Snackbar after the password change:Password aggiornata.`);
      return undefined;
    });
  }

  protected async logout(): Promise<void> {
    this.loggingOut.set(true);
    try {
      await this.session.logout();
    } finally {
      this.loggingOut.set(false);
    }
  }
}
