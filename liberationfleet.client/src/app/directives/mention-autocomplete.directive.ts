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
import { Subject, debounceTime, switchMap, takeUntil } from 'rxjs';
import { CrewmateService } from '../services/crewmate.service';
import {
  MentionCandidate,
  buildMentionBackdropHtml,
  collectMentionedUserIds,
  findActiveMentionQuery,
  insertMention
} from '../utils/mention.util';

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
  private usernameToId = new Map<string, number>();
  private search$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private repositionDropdown = () => this.updateDropdownPosition();
  private keydownListener: (() => void) | null = null;
  private scrollListener: (() => void) | null = null;
  private resizeObserver: ResizeObserver | null = null;

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
      }
    });

    this.search$
      .pipe(
        debounceTime(150),
        switchMap(query => this.crewmateService.searchForMention(query)),
        takeUntil(this.destroy$)
      )
      .subscribe(response => {
        this.candidates = response.success ? (response.items ?? []).slice(0, 3) : [];
        this.selectedIndex = 0;
        if (this.candidates.length > 0 && this.mentionQueryActive) {
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
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.removeRepositionListeners();
    this.hideDropdown();
  }

  @HostListener('input')
  onInput() {
    const textarea = this.host.nativeElement;
    this.syncComposerFromValue(textarea.value);

    const query = findActiveMentionQuery(textarea.value, textarea.selectionStart ?? textarea.value.length);
    if (query === null || query.length === 0) {
      this.mentionQueryActive = false;
      this.hideDropdown();
      return;
    }

    this.mentionQueryActive = true;
    this.search$.next(query);
  }

  @HostListener('blur')
  onBlur() {
    setTimeout(() => this.hideDropdown(), 150);
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
      'lineHeight',
      'padding',
      'letterSpacing',
      'wordSpacing',
      'textIndent',
      'boxSizing',
      'borderRadius'
    ] as const;

    props.forEach(prop => {
      this.renderer.setStyle(this.backdrop, prop, styles[prop]);
    });
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

  private isMenuOpen(): boolean {
    return this.mentionQueryActive && this.candidates.length > 0;
  }

  private selectCandidate(candidate: MentionCandidate) {
    const textarea = this.host.nativeElement;
    const cursorIndex = textarea.selectionStart ?? textarea.value.length;
    const result = insertMention(textarea.value, cursorIndex, candidate.username);
    textarea.value = result.text;
    textarea.setSelectionRange(result.cursorIndex, result.cursorIndex);
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    this.usernameToId.set(candidate.username.toLowerCase(), candidate.userId);
    this.mentionQueryActive = false;
    this.hideDropdown();
    this.syncComposerFromValue(textarea.value);
    textarea.focus();
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

    this.candidates.forEach((candidate, index) => {
      const item = this.renderer.createElement('li');
      this.renderer.addClass(item, 'mention-dropdown-item');
      if (index === this.selectedIndex) {
        this.renderer.addClass(item, 'active');
      }

      const text = this.renderer.createText(`@${candidate.username}`);
      this.renderer.appendChild(item, text);
      this.renderer.listen(item, 'mousedown', (event: Event) => {
        event.preventDefault();
        this.selectCandidate(candidate);
      });
      this.renderer.appendChild(this.dropdown, item);
    });

    this.renderer.appendChild(document.body, this.dropdown);
    this.addRepositionListeners();
    requestAnimationFrame(() => {
      this.updateDropdownPosition();
      this.scrollActiveItemIntoView();
    });
  }

  private updateDropdownPosition() {
    if (!this.dropdown) {
      return;
    }

    const textarea = this.host.nativeElement;
    const rect = textarea.getBoundingClientRect();
    const gap = 6;
    const viewportPadding = 8;

    this.renderer.setStyle(this.dropdown, 'position', 'fixed');
    this.renderer.setStyle(this.dropdown, 'left', `${Math.max(viewportPadding, rect.left)}px`);
    this.renderer.setStyle(this.dropdown, 'width', `${Math.max(rect.width, 180)}px`);
    this.renderer.setStyle(this.dropdown, 'right', 'auto');
    this.renderer.setStyle(this.dropdown, 'bottom', 'auto');
    this.renderer.setStyle(this.dropdown, 'max-height', 'none');

    const dropdownHeight = this.dropdown.offsetHeight;
    let top = rect.top - dropdownHeight - gap;

    if (top < viewportPadding) {
      top = rect.bottom + gap;
      this.renderer.addClass(this.dropdown, 'mention-dropdown-below');
      this.renderer.removeClass(this.dropdown, 'mention-dropdown-above');
    } else {
      this.renderer.addClass(this.dropdown, 'mention-dropdown-above');
      this.renderer.removeClass(this.dropdown, 'mention-dropdown-below');
    }

    const maxTop = window.innerHeight - dropdownHeight - viewportPadding;
    top = Math.min(Math.max(viewportPadding, top), maxTop);
    this.renderer.setStyle(this.dropdown, 'top', `${top}px`);
  }

  private scrollActiveItemIntoView() {
    if (!this.dropdown) {
      return;
    }

    const active = this.dropdown.querySelector('.mention-dropdown-item.active');
    active?.scrollIntoView({ block: 'nearest' });
  }

  private addRepositionListeners() {
    window.addEventListener('scroll', this.repositionDropdown, true);
    window.addEventListener('resize', this.repositionDropdown);
  }

  private removeRepositionListeners() {
    window.removeEventListener('scroll', this.repositionDropdown, true);
    window.removeEventListener('resize', this.repositionDropdown);
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
