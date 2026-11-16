import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Root component: every page, the shell included, is chosen by the router (app.routes.ts). */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
