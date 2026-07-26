import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChallengeForm } from './challenge-form';

function setInputValue(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function clickToggle(fixture: ComponentFixture<unknown>, labelOrAriaLabel: string): void {
  const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
  const button = buttons.find(
    (b) => b.textContent?.trim() === labelOrAriaLabel || b.getAttribute('aria-label') === labelOrAriaLabel,
  );
  if (!button) {
    throw new Error(`No toggle button found for "${labelOrAriaLabel}"`);
  }
  button.click();
}

describe('ChallengeForm', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<ChallengeForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChallengeForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(ChallengeForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('does not submit and shows an error when the name is blank', () => {
    clickToggle(fixture, 'Täglich');
    clickToggle(fixture, 'Blau');
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Pflichtfeld.');
    httpMock.expectNone('/api/challenges/');
  });

  it('submits a valid form and resets it on success', async () => {
    const nameInput = fixture.nativeElement.querySelector('#name') as HTMLInputElement;
    setInputValue(nameInput, 'Wordle');
    clickToggle(fixture, 'Täglich');
    clickToggle(fixture, 'Blau');
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const postReq = httpMock.expectOne('/api/challenges/');
    expect(postReq.request.body).toEqual({ name: 'Wordle', url: null, cadence: 'Daily', color: 'blue' });
    postReq.flush({
      id: '1',
      name: 'Wordle',
      url: null,
      cadence: 'Daily',
      targetCount: 1,
      startsOn: '2026-07-26',
      archivedAt: null,
      color: 'blue',
      sortOrder: 0,
    });

    await fixture.whenStable();
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);
    await fixture.whenStable();

    expect(nameInput.value).toBe('');
  });
});
