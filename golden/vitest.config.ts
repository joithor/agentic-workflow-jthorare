import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    globals: false,
    testTimeout: 10_000,
    globalSetup: ["tests/global-setup.ts"],
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // Thin entry points; their behaviour is covered end to end by the built CLI in tests/cli.test.ts and by the committed goldens.
      exclude: ["src/cli.ts", "src/fake-model-bin.ts", "src/judge-redact.ts"],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
});
