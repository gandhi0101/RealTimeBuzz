/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.razor",
    "./**/*.html",
    "./**/*.cs"
  ],
  theme: {
    extend: {
      fontFamily: {
        display: ["Manrope", "Sora", "system-ui", "sans-serif"],
        sans: ["Manrope", "Sora", "system-ui", "sans-serif"]
      }
    }
  },
  plugins: []
};
