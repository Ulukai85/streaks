import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { DashboardItem, DashboardResponse } from './dashboard.model';
import { DashboardPage } from './dashboard-page';
import { DashboardService } from './dashboard.service';

describe('DashboardPage', () => {
  const openItem: DashboardItem = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Wordle',
    url: null,
    cadence: 'Daily',
    color: 'blue',
    sortOrder: 0,
    streak: { length: 2, isAlive: true, lastCompletedPeriod: '2026-07-25' },
  };

  const doneItem: DashboardItem = {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'Yoga',
    url: null,
    cadence: 'Weekly',
    color: 'green',
    sortOrder: 0,
    streak: { length: 1, isAlive: true, lastCompletedPeriod: '2026-07-20' },
  };

  function fakeDashboardService(response: DashboardResponse, completeChallenge = vi.fn()) {
    const value = signal(response);
    return {
      dashboard: {
        isLoading: () => false,
        error: () => undefined,
        hasValue: () => true,
        value,
        reload: vi.fn(),
      },
      completeChallenge,
    };
  }

  async function setup(fake: ReturnType<typeof fakeDashboardService>): Promise<ComponentFixture<DashboardPage>> {
    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideRouter([]), { provide: DashboardService, useValue: fake }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    return fixture;
  }

  function findButton(fixture: ComponentFixture<unknown>, text: string): HTMLButtonElement {
    const button = Array.from(fixture.nativeElement.querySelectorAll('button')).find((b) =>
      (b as HTMLButtonElement).textContent?.trim().includes(text),
    ) as HTMLButtonElement | undefined;
    if (!button) {
      throw new Error(`No button found containing "${text}"`);
    }
    return button;
  }

  it('reloads the dashboard resource on creation, so revisiting the page shows fresh data', async () => {
    const fake = fakeDashboardService({ open: [openItem], doneThisPeriod: [] });
    await setup(fake);

    expect(fake.dashboard.reload).toHaveBeenCalled();
  });

  it('renders open and done items and toggles the done section', async () => {
    const fake = fakeDashboardService({ open: [openItem], doneThisPeriod: [doneItem] });
    const fixture = await setup(fake);

    expect(fixture.nativeElement.textContent).toContain('Wordle');
    expect(fixture.nativeElement.textContent).toContain('Erledigt anzeigen (1)');

    findButton(fixture, 'Erledigt anzeigen').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Erledigt ausblenden');
  });

  it('ticks off an item, disabling its button while the request is in flight', async () => {
    let resolveComplete!: () => void;
    const completeChallenge = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveComplete = resolve;
        }),
    );
    const fake = fakeDashboardService({ open: [openItem], doneThisPeriod: [] }, completeChallenge);
    const fixture = await setup(fake);

    const tickOffButton = findButton(fixture, 'Erledigt');
    tickOffButton.click();
    fixture.detectChanges();

    expect(completeChallenge).toHaveBeenCalledWith(openItem.id);
    expect(tickOffButton.disabled).toBe(true);

    resolveComplete();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(tickOffButton.disabled).toBe(false);
  });

  it('shows the problem-details message inline when tick-off fails', async () => {
    const completeChallenge = vi
      .fn()
      .mockRejectedValue({ title: 'Conflict', detail: 'This period has already been completed.' });
    const fake = fakeDashboardService({ open: [openItem], doneThisPeriod: [] }, completeChallenge);
    const fixture = await setup(fake);

    findButton(fixture, 'Erledigt').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('This period has already been completed.');
  });
});
