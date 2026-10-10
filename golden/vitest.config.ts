import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    globals: false,
    testTimeout: 10_000,
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      // Thin entry points; their behaviour is covered end to end by the goldens replayed in tests/goldens.test.ts.
      exclude: ["src/cli.ts", "src/fake-model-bin.ts", "src/judge-redact.ts"],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
});
