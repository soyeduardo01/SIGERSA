/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#eefbf5',
          100: '#d7f5e8',
          200: '#a9e8d0',
          500: '#16835f',
          600: '#0f6b4d',
          700: '#0d563f',
          900: '#0a3328',
        },
        bpm: {
          c: '#18794e',
          cp: '#b45309',
          it: '#b42318',
        },
        risk: {
          low: '#18794e',
          medium: '#b45309',
          high: '#b42318',
        },
        surface: {
          canvas: '#f5f7f6',
          card: '#ffffff',
          muted: '#e9efec',
          inverse: '#10231d',
        },
        ink: {
          strong: '#14211d',
          body: '#3f514a',
          muted: '#66756f',
        },
      },
      spacing: {
        18: '4.5rem',
        22: '5.5rem',
        30: '7.5rem',
      },
      borderRadius: {
        card: '1.25rem',
      },
      boxShadow: {
        card: '0 1px 2px rgb(16 35 29 / 0.06), 0 10px 24px rgb(16 35 29 / 0.06)',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
    },
  },
}
