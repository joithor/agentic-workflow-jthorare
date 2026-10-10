import { describe, expect, it } from "vitest";

import { normalize } from "../src/normalize.js";

describe("normalize", () => {
  it("replaces the sandbox root before any other rule", () => {
    expect(normalize("/tmp/golden-abc/state/judge/x.sqlite", ["sandbox-path"], { sandbox: "/tmp/golden-abc" })).toBe("<SANDBOX>/state/judge/x.sqlite");
  });

  it("replaces a sandbox root reached through a symlinked tmp dir", () => {
    expect(normalize("/private/tmp/golden-abc/x", ["sandbox-path"], { sandbox: "/tmp/golden-abc", sandboxReal: "/private/tmp/golden-abc" })).toBe("<SANDBOX>/x");
  });

  it("replaces uuids, iso timestamps and epoch milliseconds", () => {
    const text = '{"id":"3f2b8c1e-9a4d-4c2e-8f1a-0b9c8d7e6f5a","ts":"2026-10-10T06:26:47.123Z","at":1760077607123}';
    expect(normalize(text, ["uuid", "iso-timestamp", "epoch-ms"], { sandbox: "/nowhere" })).toBe('{"id":"<UUID>","ts":"<TS>","at":<EPOCH_MS>}');
  });

  it("replaces iso timestamps without milliseconds or with an offset", () => {
    expect(normalize("a 2026-10-10T06:26:47Z b 2026-10-10T06:26:47+02:00", ["iso-timestamp"], { sandbox: "/x" })).toBe("a <TS> b <TS>");
  });

  it("replaces numeric duration fields named *_ms or *Ms", () => {
    expect(normalize('{"latency_ms":412,"elapsedMs":7,"count":3}', ["duration-ms"], { sandbox: "/x" })).toBe('{"latency_ms":<MS>,"elapsedMs":<MS>,"count":3}');
  });

  it("reports which declared rules changed something", () => {
    const fired = new Set<string>();
    normalize("2026-10-10T06:26:47Z", ["uuid", "iso-timestamp", "sandbox-path"], { sandbox: "/x" }, fired as Set<never>);
    expect([...fired]).toEqual(["iso-timestamp"]);
  });

  it("only applies the rules a suite declares", () => {
    const text = "3f2b8c1e-9a4d-4c2e-8f1a-0b9c8d7e6f5a 2026-10-10T06:26:47Z";
    expect(normalize(text, [], { sandbox: "/x" })).toBe(text);
    expect(normalize(text, ["uuid"], { sandbox: "/x" })).toBe("<UUID> 2026-10-10T06:26:47Z");
  });

  it("leaves short commit-like hex and 16-hex digests alone (digests are part of the byte contract)", () => {
    expect(normalize("digest 0123456789abcdef sha abc1234", ["uuid", "iso-timestamp", "epoch-ms", "duration-ms"], { sandbox: "/x" })).toBe(
      "digest 0123456789abcdef sha abc1234",
    );
  });
});
