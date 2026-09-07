import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const apiBaseUrl = process.env.VITE_API_BASE_URL || env.VITE_API_BASE_URL;

  // Safe build-time validation for CI / Azure production builds
  const isCI = process.env.CI === 'true' || process.env.GITHUB_ACTIONS === 'true';
  if (mode === 'production' && isCI) {
    if (!apiBaseUrl || !apiBaseUrl.trim()) {
      throw new Error(
        '\n====================================================================\n' +
        '[EventPulse Build Error] VITE_API_BASE_URL is required for CI/Azure\n' +
        'production builds, but was not found or is empty.\n' +
        'Please ensure VITE_API_BASE_URL is set in GitHub repository variables.\n' +
        '====================================================================\n'
      );
    }
  }

  return {
    plugins: [react()],
  };
})

