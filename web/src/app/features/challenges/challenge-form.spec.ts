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
    component['form'].patchValue({ cadence: 'Daily', color: 'blue' });

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(component['form'].controls.name.invalid).toBe(true);
    expect(component['form'].controls.name.touched).toBe(true);
    httpMock.expectNone('/api/challenges/');
  });

  it('submits a valid form and resets it on success', async () => {
    const fixture = TestBed.createComponent(ChallengeForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);

    const component = fixture.componentInstance;
    component['form'].setValue({ name: 'Wordle', url: '', cadence: 'Daily', color: 'blue' });

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

    await flushMicrotasks();
    fixture.detectChanges();
    httpMock.expectOne('/api/challenges/').flush([]);

    expect(component['form'].controls.name.value).toBe('');
    expect(component['form'].controls.cadence.value).toBeNull();
  });
});
