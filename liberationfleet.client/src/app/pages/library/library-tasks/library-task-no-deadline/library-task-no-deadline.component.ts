import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

/** Legacy URL — quest board now hosts both tabs. */
@Component({
  selector: 'app-library-task-no-deadline',
  standalone: true,
  template: ''
})
export class LibraryTaskNoDeadlineComponent implements OnInit {
  private router = inject(Router);

  ngOnInit() {
    void this.router.navigate(['/app/crew/library-of-things/tasks'], {
      queryParams: { tab: 'no-deadline' },
      replaceUrl: true
    });
  }
}
