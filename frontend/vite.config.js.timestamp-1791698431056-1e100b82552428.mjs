// vite.config.js
import { defineConfig, loadEnv } from "file:///C:/csp%20project/EventPulse/frontend/node_modules/vite/dist/node/index.js";
import react from "file:///C:/csp%20project/EventPulse/frontend/node_modules/@vitejs/plugin-react/dist/index.mjs";
var vite_config_default = defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiBaseUrl = process.env.VITE_API_BASE_URL || env.VITE_API_BASE_URL;
  const isCI = process.env.CI === "true" || process.env.GITHUB_ACTIONS === "true";
  if (mode === "production" && isCI) {
    if (!apiBaseUrl || !apiBaseUrl.trim()) {
      throw new Error(
        "\n====================================================================\n[EventPulse Build Error] VITE_API_BASE_URL is required for CI/Azure\nproduction builds, but was not found or is empty.\nPlease ensure VITE_API_BASE_URL is set in GitHub repository variables.\n====================================================================\n"
      );
    }
  }
  return {
    plugins: [react()]
  };
});
export {
  vite_config_default as default
};
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsidml0ZS5jb25maWcuanMiXSwKICAic291cmNlc0NvbnRlbnQiOiBbImNvbnN0IF9fdml0ZV9pbmplY3RlZF9vcmlnaW5hbF9kaXJuYW1lID0gXCJDOlxcXFxjc3AgcHJvamVjdFxcXFxFdmVudFB1bHNlXFxcXGZyb250ZW5kXCI7Y29uc3QgX192aXRlX2luamVjdGVkX29yaWdpbmFsX2ZpbGVuYW1lID0gXCJDOlxcXFxjc3AgcHJvamVjdFxcXFxFdmVudFB1bHNlXFxcXGZyb250ZW5kXFxcXHZpdGUuY29uZmlnLmpzXCI7Y29uc3QgX192aXRlX2luamVjdGVkX29yaWdpbmFsX2ltcG9ydF9tZXRhX3VybCA9IFwiZmlsZTovLy9DOi9jc3AlMjBwcm9qZWN0L0V2ZW50UHVsc2UvZnJvbnRlbmQvdml0ZS5jb25maWcuanNcIjtpbXBvcnQgeyBkZWZpbmVDb25maWcsIGxvYWRFbnYgfSBmcm9tICd2aXRlJ1xyXG5pbXBvcnQgcmVhY3QgZnJvbSAnQHZpdGVqcy9wbHVnaW4tcmVhY3QnXHJcblxyXG4vLyBodHRwczovL3ZpdGUuZGV2L2NvbmZpZy9cclxuZXhwb3J0IGRlZmF1bHQgZGVmaW5lQ29uZmlnKCh7IG1vZGUgfSkgPT4ge1xyXG4gIGNvbnN0IGVudiA9IGxvYWRFbnYobW9kZSwgcHJvY2Vzcy5jd2QoKSwgJycpO1xyXG4gIGNvbnN0IGFwaUJhc2VVcmwgPSBwcm9jZXNzLmVudi5WSVRFX0FQSV9CQVNFX1VSTCB8fCBlbnYuVklURV9BUElfQkFTRV9VUkw7XHJcblxyXG4gIC8vIFNhZmUgYnVpbGQtdGltZSB2YWxpZGF0aW9uIGZvciBDSSAvIEF6dXJlIHByb2R1Y3Rpb24gYnVpbGRzXHJcbiAgY29uc3QgaXNDSSA9IHByb2Nlc3MuZW52LkNJID09PSAndHJ1ZScgfHwgcHJvY2Vzcy5lbnYuR0lUSFVCX0FDVElPTlMgPT09ICd0cnVlJztcclxuICBpZiAobW9kZSA9PT0gJ3Byb2R1Y3Rpb24nICYmIGlzQ0kpIHtcclxuICAgIGlmICghYXBpQmFzZVVybCB8fCAhYXBpQmFzZVVybC50cmltKCkpIHtcclxuICAgICAgdGhyb3cgbmV3IEVycm9yKFxyXG4gICAgICAgICdcXG49PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PVxcbicgK1xyXG4gICAgICAgICdbRXZlbnRQdWxzZSBCdWlsZCBFcnJvcl0gVklURV9BUElfQkFTRV9VUkwgaXMgcmVxdWlyZWQgZm9yIENJL0F6dXJlXFxuJyArXHJcbiAgICAgICAgJ3Byb2R1Y3Rpb24gYnVpbGRzLCBidXQgd2FzIG5vdCBmb3VuZCBvciBpcyBlbXB0eS5cXG4nICtcclxuICAgICAgICAnUGxlYXNlIGVuc3VyZSBWSVRFX0FQSV9CQVNFX1VSTCBpcyBzZXQgaW4gR2l0SHViIHJlcG9zaXRvcnkgdmFyaWFibGVzLlxcbicgK1xyXG4gICAgICAgICc9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PVxcbidcclxuICAgICAgKTtcclxuICAgIH1cclxuICB9XHJcblxyXG4gIHJldHVybiB7XHJcbiAgICBwbHVnaW5zOiBbcmVhY3QoKV0sXHJcbiAgfTtcclxufSlcclxuXHJcbiJdLAogICJtYXBwaW5ncyI6ICI7QUFBa1MsU0FBUyxjQUFjLGVBQWU7QUFDeFUsT0FBTyxXQUFXO0FBR2xCLElBQU8sc0JBQVEsYUFBYSxDQUFDLEVBQUUsS0FBSyxNQUFNO0FBQ3hDLFFBQU0sTUFBTSxRQUFRLE1BQU0sUUFBUSxJQUFJLEdBQUcsRUFBRTtBQUMzQyxRQUFNLGFBQWEsUUFBUSxJQUFJLHFCQUFxQixJQUFJO0FBR3hELFFBQU0sT0FBTyxRQUFRLElBQUksT0FBTyxVQUFVLFFBQVEsSUFBSSxtQkFBbUI7QUFDekUsTUFBSSxTQUFTLGdCQUFnQixNQUFNO0FBQ2pDLFFBQUksQ0FBQyxjQUFjLENBQUMsV0FBVyxLQUFLLEdBQUc7QUFDckMsWUFBTSxJQUFJO0FBQUEsUUFDUjtBQUFBLE1BS0Y7QUFBQSxJQUNGO0FBQUEsRUFDRjtBQUVBLFNBQU87QUFBQSxJQUNMLFNBQVMsQ0FBQyxNQUFNLENBQUM7QUFBQSxFQUNuQjtBQUNGLENBQUM7IiwKICAibmFtZXMiOiBbXQp9Cg==
