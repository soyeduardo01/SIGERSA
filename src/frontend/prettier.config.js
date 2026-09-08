/** @type {import('prettier').Config & import('prettier-plugin-tailwindcss').PluginOptions} */
export default {
  plugins: ['prettier-plugin-tailwindcss'],
  tailwindStylesheet: './src/index.css',
  printWidth: 100,
  semi: false,
  singleQuote: true,
}
