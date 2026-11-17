import { Component, inject, input, signal } from '@angular/core';
import { email, form, FormField, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { APP_NAME, safeReturnUrl, SessionStore } from '../../../core';
import { apiFormErrors } from '../../../core/forms';

/** Codes shown above the button: they are about the attempt, not about one field. */
const FORM_CODES = ['auth.invalid_credentials', 'request.too_many'];

@Component({
  selector: 'app-login-page',
  imports: [
    FormField,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  /** Page to open after login (query parameter, bound by the router). */
  readonly returnUrl = input<string>();

  protected readonly appName = APP_NAME;
  protected readonly passwordVisible = signal(false);
  protected readonly showPasswordLabel = $localize`:Button that shows the password:Mostra la password`;
  protected readonly hidePasswordLabel = $localize`:Button that hides the password:Nascondi la password`;
  protected readonly model = signal({ email: '', password: '', rememberMe: true });
  protected readonly loginForm = form(this.model, (path) => {
    required(path.email, { message: $localize`:Login form error:Inserisci l'email.` });
    email(path.email, { message: $localize`:Login form error:Email non valida.` });
    required(path.password, { message: $localize`:Login form error:Inserisci la password.` });
  });

  protected async signIn(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.loginForm, async () => {
      try {
        await this.session.login(this.model());
      } catch (error) {
        const errors = apiFormErrors(this.loginForm, error, { formCodes: FORM_CODES });
        if (errors) {
          return errors;
        }
        throw error;
      }
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
      return undefined;
    });
  }
}
