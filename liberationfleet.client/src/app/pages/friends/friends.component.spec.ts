import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { FriendsComponent } from './friends.component';

describe('FriendsComponent', () => {
  let fixture: ComponentFixture<FriendsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FriendsComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(FriendsComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
