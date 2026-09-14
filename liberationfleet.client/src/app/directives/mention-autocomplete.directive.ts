import {
  Directive,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  Renderer2,
  inject
} from '@angular/core';
import { NgControl } from '@angular/forms';
import { Subject, catchError, debounceTime, of, switchMap, takeUntil } from 'rxjs';
import { CrewmateService } from '../services/crewmate.service';
import {
  MentionCandidate,
  buildMentionBackdropHtml,
  collectMentionedUserIds,
  insertMention,
  resolveActiveMentionQuery
} from '../utils/mention.util';
import { beginMentionSelection, isMentionSelectionPending } from '../utils/mention-focus.util';

@Directive({
  selector: 'textarea[appMentionAutocomplete]',
  standalone: true
})
export class MentionAutocompleteDirective implements OnInit, OnDestroy {
  @Input() mentionedUserIds: number[] = [];
  @Output() mentionedUserIdsChange = new EventEmitter<number[]>();

  private host = inject(ElementRef<HTMLTextAreaElement>);
  private renderer = inject(Renderer2);
  private crewmateService = inject(CrewmateService);
  private ngControl = inject(NgControl, { optional: true, self: true });

  private dropdown: HTMLElement | null = null;
  private backdrop: HTMLElement | null = null;
  private candidates: MentionCandidate[] = [];
  private selectedIndex = 0;
  private mentionQueryActive = false;
  private lastQuery = '';
  private usernameToId = new Map<string, number>();
  private search$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private repositionDropdown = () => this.updateDropdownPosition();
  private keydownListener: (() => void) | null = null;
  private scrollListener: (() => void) | null = null;
  private selectionChangeListener: (() => void) | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private classObserver: MutationObserver | null = null;
  private mentionProbeTimer: ReturnType<typeof setTimeout> | null = null;
  private blurHideTimer: ReturnType<typeof setTimeout> | null = null;
  private selectingCandidate = false;

  ngOnInit() {
    this.setupComposerHighlight();

    this.crewmateService.getCrewmates().subscribe({
      next: response => {
        if (!response.success) {
          return;
        }

        this.usernameToId = new Map(
          (response.items ?? [])
            .filter(item => !item.isSelf)
            .map(item => [item.username.toLowerCase(), item.userId])
        );
        this.syncMentionedUserIds();
        this.updateBackdrop();

        // Crewmate list can land after the first `@` on slow mobile networks.
        if (this.mentionQueryActive) {
          this.refreshMentionCandidates(this.lastQuery);
        }
      }
    });

    this.search$
      .pipe(
        debounceTime(150),
        switchMap(query =>
          this.crewmateService.searchForMention(query).pipe(
            // Keep the stream alive — a single 4xx/5xx must not disable @mentions for the session.
            catchError(() => of({ success: false, message: '', items: [] as MentionCandidate[] }))
          )
        ),
        takeUntil(this.destroy$)
      )
      .subscribe(response => {
        if (!this.mentionQueryActive) {
          return;
        }

        const remote = response.success ? (response.items ?? []).slice(0, 3) : [];
        // Prefer API results; fall back to the already-loaded crewmate list.
        this.candidates = remote.length > 0 ? remote : this.localCandidates(this.lastQuery);
        this.selectedIndex = 0;
        if (this.candidates.length > 0) {
          this.renderDropdown();
        } else {
          this.hideDropdown();
        }
      });

    const textarea = this.host.nativeElement;
    this.keydownListener = this.renderer.listen(
      textarea,
      'keydown',
      (event: KeyboardEvent) => this.handleKeyDown(event)
    );

    // iOS/Android often update the caret after `input`; selectionchange catches that.
    this.selectionChangeListener = this.renderer.listen(document, 'selectionchange', () => {
      if (document.activeElement !== textarea) {
        return;
      }
      this.scheduleMentionProbe();
    });

    this.ngControl?.valueChanges?.pipe(takeUntil(this.destroy$)).subscribe(value => {
      this.syncComposerFromValue(value == null ? '' : String(value));
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    this.keydownListener?.();
    this.keydownListener = null;
    this.scrollListener?.();
    this.scrollListener = null;
    this.selectionChangeListener?.();
    this.selectionChangeListener = null;
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.classObserver?.disconnect();
    this.classObserver = null;
    if (this.mentionProbeTimer != null) {
      clearTimeout(this.mentionProbeTimer);
      this.mentionProbeTimer = null;
    }
    if (this.blurHideTimer != null) {
      clearTimeout(this.blurHideTimer);
      this.blurHideTimer = null;
    }
    this.removeRepositionListeners();
    this.hideDropdown();
  }

  @HostListener('input')
  onInput() {
    const textarea = this.host.nativeElement;
    this.syncComposerFromValue(textarea.value);
    this.scheduleMentionProbe();
  }

  @HostListener('keyup')
  onKeyUp() {
    this.scheduleMentionProbe();
  }

  @HostListener('blur')
  onBlur() {
    if (this.blurHideTimer != null) {
      clearTimeout(this.blurHideTimer);
    }
    // Delay so pointer/touch selection on the dropdown can run first.
    this.blurHideTimer = setTimeout(() => {
      this.blurHideTimer = null;
      if (isMentionSelectionPending() || document.activeElement === this.host.nativeElement) {
        return;
      }
      this.hideDropdown();
    }, 200);
  }

  private scheduleMentionProbe() {
    if (this.mentionProbeTimer != null) {
      clearTimeout(this.mentionProbeTimer);
    }
    // Double rAF + timeout: caret is often wrong on the same turn as `input` on mobile.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        this.mentionProbeTimer = setTimeout(() => {
          this.mentionProbeTimer = null;
          this.probeActiveMention();
        }, 0);
      });
    });
  }

  private probeActiveMention() {
    const textarea = this.host.nativeElement;
    const query = resolveActiveMentionQuery(textarea.value, textarea.selectionStart);
    if (query === null) {
      this.mentionQueryActive = false;
      this.lastQuery = '';
      this.hideDropdown();
      return;
    }

    // Empty string after `@` is still an active mention (show suggestions).
    const queryChanged = !this.mentionQueryActive || query !== this.lastQuery;
    this.mentionQueryActive = true;
    this.lastQuery = query;
    this.refreshMentionCandidates(query, queryChanged);
  }

  private refreshMentionCandidates(query: string, kickSearch = true) {
    this.candidates = this.localCandidates(query);
    this.selectedIndex = 0;
    if (this.candidates.length > 0) {
      this.renderDropdown();
    } else {
      this.hideDropdown(false);
    }
    if (kickSearch) {
      this.search$.next(query);
    }
  }

  private setupComposerHighlight() {
    const textarea = this.host.nativeElement;
    const parent = textarea.parentElement;
    if (!parent || parent.classList.contains('mention-composer-shell')) {
      return;
    }

    const shell = this.renderer.createElement('div');
    this.renderer.addClass(shell, 'mention-composer-shell');

    this.backdrop = this.renderer.createElement('div');
    this.renderer.addClass(this.backdrop, 'mention-composer-backdrop');
    this.renderer.setAttribute(this.backdrop, 'aria-hidden', 'true');

    this.renderer.insertBefore(parent, shell, textarea);
    this.renderer.appendChild(shell, this.backdrop);
    this.renderer.appendChild(shell, textarea);
    this.renderer.addClass(textarea, 'mention-composer-input');

    this.syncBackdropStyles();
    this.updateBackdrop();

    this.scrollListener = this.renderer.listen(textarea, 'scroll', () => {
      if (!this.backdrop) {
        return;
      }
      this.backdrop.scrollTop = textarea.scrollTop;
      this.backdrop.scrollLeft = textarea.scrollLeft;
    });

    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => this.syncBackdropStyles());
      this.resizeObserver.observe(textarea);
    }

    if (typeof MutationObserver !== 'undefined') {
      this.classObserver = new MutationObserver(() => this.syncBackdropStyles());
      this.classObserver.observe(textarea, { attributes: true, attributeFilter: ['class', 'style'] });
    }
  }

  private syncBackdropStyles() {
    if (!this.backdrop) {
      return;
    }

    const textarea = this.host.nativeElement;
    const styles = getComputedStyle(textarea);
    const props = [
      'font',
      'fontSize',
      'fontFamily',
      'fontWeight',
      'fontStyle',
      'lineHeight',
      'padding',
      'paddingTop',
      'paddingRight',
      'paddingBottom',
      'paddingLeft',
      'borderWidth',
      'borderStyle',
      'letterSpacing',
      'wordSpacing',
      'textIndent',
      'boxSizing',
      'borderRadius',
      'whiteSpace',
      'overflow',
      'overflowX',
      'overflowY',
      'textOverflow',
      'wordBreak',
      'overflowWrap'
    ] as const;

    props.forEach(prop => {
      this.renderer.setStyle(this.backdrop, prop, styles[prop]);
    });
    // Keep border transparent so only the textarea chrome shows, but widths must match.
    this.renderer.setStyle(this.backdrop, 'borderColor', 'transparent');
    this.renderer.setStyle(this.backdrop, 'background', 'transparent');

    this.backdrop.scrollTop = textarea.scrollTop;
    this.backdrop.scrollLeft = textarea.scrollLeft;
  }

  private syncComposerFromValue(value: string) {
    this.updateBackdrop(value);
    this.syncMentionedUserIds(value);

    if (!value) {
      this.mentionQueryActive = false;
      this.hideDropdown();
    }
  }

  private updateBackdrop(value = this.host.nativeElement.value) {
    if (!this.backdrop) {
      return;
    }

    const knownUsernames = new Set(this.usernameToId.keys());
    this.backdrop.innerHTML = buildMentionBackdropHtml(value, knownUsernames);
  }

  private handleKeyDown(event: KeyboardEvent) {
    if (!this.isMenuOpen()) {
      return;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      event.stopPropagation();
      this.selectedIndex = (this.selectedIndex + 1) % this.candidates.length;
      this.renderDropdown();
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      event.stopPropagation();
      this.selectedIndex = (this.selectedIndex - 1 + this.candidates.length) % this.candidates.length;
      this.renderDropdown();
      return;
    }

    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      event.stopPropagation();
      const candidate = this.candidates[this.selectedIndex];
      if (candidate) {
        this.selectCandidate(candidate);
      }
      return;
    }

    if (event.key === 'Tab') {
      event.preventDefault();
      event.stopPropagation();
      const candidate = this.candidates[this.selectedIndex];
      if (candidate) {
        this.selectCandidate(candidate);
      }
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.mentionQueryActive = false;
      this.hideDropdown();
    }
  }

  private localCandidates(query: string): MentionCandidate[] {
    const q = query.trim().toLowerCase();
    const ranked = [...this.usernameToId.entries()]
      .map(([username, userId]) => ({ username, userId, lower: username.toLowerCase() }))
      .filter(item => !q || item.lower.startsWith(q) || item.lower.includes(q))
      .sort((a, b) => {
        if (!q) {
          return a.lower.localeCompare(b.lower);
        }
        const aStarts = a.lower.startsWith(q) ? 0 : 1;
        const bStarts = b.lower.startsWith(q) ? 0 : 1;
        if (aStarts !== bStarts) {
          return aStarts - bStarts;
        }
        return a.lower.localeCompare(b.lower);
      })
      .slice(0, 3);

    return ranked.map(({ username, userId }) => ({ username, userId }));
  }

  private isMenuOpen(): boolean {
    return this.mentionQueryActive && this.candidates.length > 0;
  }

  private selectCandidate(candidate: MentionCandidate) {
    if (this.selectingCandidate) {
      return;
    }
    this.selectingCandidate = true;
    beginMentionSelection();
    if (this.blurHideTimer != null) {
      clearTimeout(this.blurHideTimer);
      this.blurHideTimer = null;
    }

    const textarea = this.host.nativeElement;
    // Prefer caret if it sits in an active @token; otherwise insert at end (stale caret).
    const insertAt =
      findActiveAtIndex(textarea.value, textarea.selectionStart) ?? textarea.value.length;
    const result = insertMention(textarea.value, insertAt, candidate.username);
    textarea.value = result.text;
    textarea.setSelectionRange(result.cursorIndex, result.cursorIndex);
    this.ngControl?.control?.setValue(result.text, { emitEvent: true });
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    this.usernameToId.set(candidate.username.toLowerCase(), candidate.userId);
    this.mentionQueryActive = false;
    this.hideDropdown();
    this.syncComposerFromValue(textarea.value);
    this.restoreTextareaFocus(textarea);
    setTimeout(() => {
      this.selectingCandidate = false;
    }, 300);
  }

  private restoreTextareaFocus(textarea: HTMLTextAreaElement) {
    const focus = () => {
      beginMentionSelection();
      textarea.focus({ preventScroll: true });
      // Re-assert caret after focus; iOS often drops it.
      try {
        const pos = textarea.selectionStart ?? textarea.value.length;
        textarea.setSelectionRange(pos, pos);
      } catch {
        // Ignore selection errors on unsupported hosts.
      }
    };
    focus();
    setTimeout(focus, 0);
    setTimeout(focus, 50);
  }

  private syncMentionedUserIds(value = this.host.nativeElement.value) {
    const ids = collectMentionedUserIds(value, this.usernameToId);
    this.mentionedUserIds = ids;
    this.mentionedUserIdsChange.emit(ids);
  }

  private renderDropdown() {
    this.hideDropdown(false);
    if (this.candidates.length === 0) {
      return;
    }

    this.dropdown = this.renderer.createElement('ul');
    this.renderer.addClass(this.dropdown, 'mention-dropdown');
    this.renderer.setAttribute(this.dropdown, 'role', 'listbox');

    this.candidates.forEach((candidate, index) => {
      const item = this.renderer.createElement('li');
      this.renderer.addClass(item, 'mention-dropdown-item');
      if (index === this.selectedIndex) {
        this.renderer.addClass(item, 'active');
      }

      const text = this.renderer.createText(`@${candidate.username}`);
      this.renderer.appendChild(item, text);
      // Prevent textarea blur (same pattern as composer chrome buttons). touchstart is
      // needed on iOS, where blur can fire before pointerdown.
      const pick = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
        beginMentionSelection();
        this.selectCandidate(candidate);
      };
      this.renderer.listen(item, 'touchstart', pick);
      this.renderer.listen(item, 'mousedown', pick);
      this.renderer.listen(item, 'pointerdown', pick);
      this.renderer.appendChild(this.dropdown, item);
    });

    this.renderer.appendChild(document.body, this.dropdown);
    this.addRepositionListeners();
    requestAnimationFrame(() => this.updateDropdownPosition());
  }

  private updateDropdownPosition() {
    if (!this.dropdown) {
      return;
    }

    const textarea = this.host.nativeElement;
    const anchor =
      (textarea.closest('.mention-composer-shell') as HTMLElement | null) ??
      (textarea.closest('.composer') as HTMLElement | null) ??
      textarea;
    const rect = anchor.getBoundingClientRect();
    const gap = 6;
    const viewportPadding = 8;
    const vv = window.visualViewport;
    const viewTop = vv?.offsetTop ?? 0;
    const viewLeft = vv?.offsetLeft ?? 0;
    const viewWidth = vv?.width ?? window.innerWidth;
    // position:fixed is laid out in the layout viewport; match getBoundingClientRect.
    const layoutHeight = window.innerHeight;

    this.renderer.setStyle(this.dropdown, 'position', 'fixed');
    this.renderer.setStyle(
      this.dropdown,
      'left',
      `${Math.max(viewLeft + viewportPadding, rect.left)}px`
    );
    this.renderer.setStyle(
      this.dropdown,
      'width',
      `${Math.min(Math.max(rect.width, 180), viewWidth - viewportPadding * 2)}px`
    );
    this.renderer.setStyle(this.dropdown, 'right', 'auto');
    this.renderer.setStyle(this.dropdown, 'top', 'auto');

    // Keep the menu in the visible band above the composer (keyboard / visualViewport).
    const spaceAbove = Math.max(0, rect.top - viewTop - viewportPadding);
    const maxHeight = Math.max(48, spaceAbove - gap);
    this.renderer.setStyle(this.dropdown, 'max-height', `${maxHeight}px`);
    this.renderer.setStyle(this.dropdown, 'overflow-y', 'auto');

    // Hug the top of the composer: pin the dropdown's bottom edge just above the anchor.
    const bottom = Math.max(gap, layoutHeight - rect.top + gap);
    this.renderer.setStyle(this.dropdown, 'bottom', `${bottom}px`);
    this.renderer.addClass(this.dropdown, 'mention-dropdown-above');
    this.renderer.removeClass(this.dropdown, 'mention-dropdown-below');
  }

  private addRepositionListeners() {
    window.addEventListener('scroll', this.repositionDropdown, true);
    window.addEventListener('resize', this.repositionDropdown);
    window.visualViewport?.addEventListener('resize', this.repositionDropdown);
    window.visualViewport?.addEventListener('scroll', this.repositionDropdown);
  }

  private removeRepositionListeners() {
    window.removeEventListener('scroll', this.repositionDropdown, true);
    window.removeEventListener('resize', this.repositionDropdown);
    window.visualViewport?.removeEventListener('resize', this.repositionDropdown);
    window.visualViewport?.removeEventListener('scroll', this.repositionDropdown);
  }

  private hideDropdown(clearCandidates = true) {
    if (this.dropdown?.parentElement) {
      this.renderer.removeChild(document.body, this.dropdown);
    }
    this.dropdown = null;
    this.removeRepositionListeners();
    if (clearCandidates) {
      this.candidates = [];
      this.selectedIndex = 0;
    }
  }
}

function findActiveAtIndex(text: string, cursorIndex: number | null | undefined): number | null {
  const len = text.length;
  const clamped =
    typeof cursorIndex === 'number' && Number.isFinite(cursorIndex)
      ? Math.max(0, Math.min(len, cursorIndex))
      : len;
  const before = text.slice(0, clamped);
  if (/@([A-Za-z0-9_]*)$/.test(before)) {
    return clamped;
  }
  if (clamped !== len && /@([A-Za-z0-9_]*)$/.test(text)) {
    return len;
  }
  return /@([A-Za-z0-9_]*)$/.test(text) ? len : null;
}
