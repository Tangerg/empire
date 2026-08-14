
/** 16x16 line icons for the HUD. `currentColor` so CSS drives the colour. */
const paths: Record<string, string> = {
  sword: 'M11.5 1.5 14.5 4.5 7 12l-1.5-1.5zM4 11l1 1-2 2-1-1zM3.5 12.5 1.5 14.5',
  shield: 'M8 1.5 13.5 3.5v5c0 3-2.4 5.2-5.5 6-3.1-.8-5.5-3-5.5-6v-5z',
  bow: 'M4 2c6 3 6 9 0 12M4.5 2.2 4.5 13.8M5 8h9M11 5.5 14.5 8 11 10.5',
  move: 'M8 1.5v13M1.5 8h13M8 1.5 5.5 4M8 1.5 10.5 4M8 14.5 5.5 12M8 14.5 10.5 12M1.5 8 4 5.5M1.5 8 4 10.5M14.5 8 12 5.5M14.5 8 12 10.5',
  coin: 'M8 2a6 6 0 1 0 0 12A6 6 0 0 0 8 2zM8 5v6M6 6.5h4M6 9.5h4',
  heart: 'M8 13.5S2 9.8 2 6.2A3.2 3.2 0 0 1 8 4.6a3.2 3.2 0 0 1 6 1.6c0 3.6-6 7.3-6 7.3z',
  flag: 'M4 14V2M4 2.5h8l-2 3 2 3H4',
  hourglass: 'M4 1.5h8M4 14.5h8M5 1.5v3l3 3.5 3-3.5v-3M5 14.5v-3l3-3.5 3 3.5v3',
  undo: 'M3 8a5.5 5.5 0 1 1 5.5 5.5M3 8l3-3M3 8l3 3',
  boot: 'M5 2h3v7h3.5A2.5 2.5 0 0 1 14 11.5V14H5z',
  crosshair: 'M8 2v3M8 11v3M2 8h3M11 8h3M8 5.5A2.5 2.5 0 1 0 8 10.5 2.5 2.5 0 0 0 8 5.5z',
  skull: 'M8 1.5A5.5 5.5 0 0 0 2.5 7c0 2 1 3 1.5 3.5V14h8v-3.5C12.5 10 13.5 9 13.5 7A5.5 5.5 0 0 0 8 1.5zM6 7.5h.01M10 7.5h.01',
  cross: 'M7 2h2v5h5v2H9v5H7V9H2V7h5z',
  play: 'M4.5 2.5 13 8l-8.5 5.5z',
  grid: 'M2 2h12v12H2zM2 6h12M2 10h12M6 2v12M10 2v12',
  save: 'M2.5 2.5h11v11h-11zM5.5 2.5v4h5v-4M5.5 13.5v-4h5v4',
  trash: 'M3 4.5h10M6 4.5V3h4v1.5M4.5 4.5 5 13.5h6l.5-9M7 7v4M9 7v4',
};

export function icon(name: keyof typeof paths | string, size = 16): string {
  const d = paths[name] ?? paths.crosshair;
  return `<svg class="icon" viewBox="0 0 16 16" width="${size}" height="${size}" fill="none"
    stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"
    aria-hidden="true"><path d="${d}"/></svg>`;
}

