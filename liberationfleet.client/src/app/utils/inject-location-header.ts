import { inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocationHeaderInfo, resolveLocationHeader } from './location-header.util';

/** Resolve fixed location header from the current route tree. */
export function injectLocationHeaderInfo(): LocationHeaderInfo | null {
  const route = inject(ActivatedRoute);
  return resolveLocationHeader(route.snapshot.root);
}
