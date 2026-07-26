import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { Challenge } from './challenge.model';
import { ChallengeList } from './challenge-list';
import { ChallengesService } from './challenges.service';

describe('ChallengeList', () => {
  const challenge: Challenge = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Wordle',
    url: null,
    cadence: 'Daily',
    targetCount: 1,
    startsOn: '2026-07-26',
    archivedAt: null,
    color: 'blue',
    sortOrder: 0,
  };

  function fakeChallengesService(challenges: Challenge[]) {
    const value = signal(challenges);
    return {
      challenges: {
        isLoading: () => false,
        error: () => undefined,
        hasValue: () => true,
        value,
      },
      archive: vi.fn().mockResolvedValue(challenge),
    };
  }

  it('renders a card per challenge', async () => {
    const fake = fakeChallengesService([challenge]);
    await TestBed.configureTestingModule({
      imports: [ChallengeList],
      providers: [provideRouter([]), { provide: ChallengesService, useValue: fake }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChallengeList);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Wordle');
  });

  it('archives only after the user confirms', async () => {
    const fake = fakeChallengesService([challenge]);
    await TestBed.configureTestingModule({
      imports: [ChallengeList],
      providers: [provideRouter([]), { provide: ChallengesService, useValue: fake }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChallengeList);
    fixture.detectChanges();

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const archiveButton = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.includes('Archivieren')) as HTMLButtonElement;

    archiveButton.click();
    expect(confirmSpy).toHaveBeenCalled();
    expect(fake.archive).not.toHaveBeenCalled();

    confirmSpy.mockReturnValue(true);
    archiveButton.click();
    expect(fake.archive).toHaveBeenCalledWith(challenge.id);
  });
});
