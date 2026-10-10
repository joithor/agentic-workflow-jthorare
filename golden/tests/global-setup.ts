import { execFileSync } from "node:child_process";
import path from "node:path";

// The sandbox execs the built fake-model-bin.js, so the runner tests need dist/.
export default function setup(): void {
  execFileSync(process.execPath, [path.join("node_modules", "typescript", "bin", "tsc"), "-p", "tsconfig.json"], { stdio: "inherit" });
}
