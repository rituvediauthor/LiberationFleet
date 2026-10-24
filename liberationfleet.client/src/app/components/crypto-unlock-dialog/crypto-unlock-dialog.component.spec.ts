import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CryptoUnlockDialogComponent } from './crypto-unlock-dialog.component';
import { AuthService } from '../../services/auth.service';
import { SavedRecoveryPhraseService } from '../../services/saved-recovery-phrase.service';
import { ToastService } from '../toast/toast.component';

describe('CryptoUnlockDialogComponent', () => {
  let fixture: ComponentFixture<CryptoUnlockDialogComponent>;
  let component: CryptoUnlockDialogComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CryptoUnlockDialogComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { unlockWithRecoveryPhrase: jasmine.createSpy('unlock') } },
        { provide: SavedRecoveryPhraseService, useValue: { isSaveEnabled: () => false } },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CryptoUnlockDialogComponent);
    component = fixture.componentInstance;
    component.visible = true;
    fixture.detectChanges();
  });

  it('blurs the recovery field on Enter so the keyboard can dismiss', () => {
    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.focus();
    expect(document.activeElement).toBe(textarea);

    const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    spyOn(event, 'preventDefault');
    component.onPhraseEnter(event);
    expect(event.preventDefault).toHaveBeenCalled();
    expect(document.activeElement === textarea).toBeFalse();
  });

  it('dismisses the keyboard when tapping dialog chrome, not the field', () => {
    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.focus();
    component.onChromePointer({ target: fixture.nativeElement.querySelector('h2') } as unknown as Event);
    expect(document.activeElement === textarea).toBeFalse();
  });
});
