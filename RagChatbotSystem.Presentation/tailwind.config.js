/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Pages/**/*.cshtml',
    './Views/**/*.cshtml',
    './wwwroot/**/*.html',
    './**/*.razor',
  ],
  theme: {
    extend: {
      colors: {
        ink: '#111827',
        'accent-strong': '#4f46e5',
        'accent-light': '#818cf8',
        surface: '#ffffff',
        'surface-muted': '#f3f4f6',
        border: '#e5e7eb',
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        grotesk: ['Space Grotesk', 'sans-serif'],
      },
      boxShadow: {
        'soft': '0 4px 20px -2px rgba(0, 0, 0, 0.05)',
        'glow': '0 0 15px rgba(79, 70, 229, 0.3)',
      }
    },
  },
  plugins: [],
}
