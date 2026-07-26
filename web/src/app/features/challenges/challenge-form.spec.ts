import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChallengeForm } from './challenge-form';

function flushMicrotasks(): Promise<void> {
  return Promise.resolve().then(() => Promise.resolve());
}

describe('ChallengeForm', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChallengeForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('does not submit and shows an error when the name is blank', async () => {
    const fixture = TestBed.createComponent(ChallengeForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);

    const component = fixture.componentInstance;
    component['model'].set({ name: '', url: '', cadence: 'Daily', color: 'blue' });

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await flushMicrotasks();
    fixture.detectChanges();

    expect(component['challengeForm'].name().invalid()).toBe(true);
    expect(component['challengeForm'].name().touched()).toBe(true);
    httpMock.expectNone('/api/challenges/');
  });

  it('submits a valid form and resets it on success', async () => {
    const fixture = TestBed.createComponent(ChallengeForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);

    const component = fixture.componentInstance;
    component['model'].set({ name: 'Wordle', url: '', cadence: 'Daily', color: 'blue' });

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await flushMicrotasks();

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

    await flushMicrotasks();
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);
    await flushMicrotasks();

    expect(component['model']().name).toBe('');
    expect(component['model']().cadence).toBe('');
  });
});
