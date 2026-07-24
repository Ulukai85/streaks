# ADR 0006 — Tailwind CSS + spartan-ng for styling and UI components

The frontend needs a styling approach and a base set of accessible UI
components rather than hand-rolled CSS and bespoke widgets for every
control. Angular Material was considered but rejected — it imposes a
Material Design look that would need heavy overriding to match a custom
brand, and it's a monolithic runtime dependency rather than owned code.
Tailwind CSS (utility-first, v4 CSS-first config, no separate config file)
paired with spartan-ng (unstyled accessible primitives + copy-paste styled
components you own in-repo, no black-box component runtime) keeps full
control over markup and styling while still giving accessible primitives
for free.
