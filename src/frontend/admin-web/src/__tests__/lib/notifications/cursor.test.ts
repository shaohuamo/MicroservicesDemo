import { beforeEach, describe, expect, it } from "vitest";
import {
  persistAcknowledgedCursor,
  readStoredCursor,
} from "@/lib/notifications/cursor";

describe("notification replay cursor", () => {
  beforeEach(() => sessionStorage.clear());

  it("keeps each user's acknowledged sequence across account switches", () => {
    persistAcknowledgedCursor("user-a", 120);
    expect(readStoredCursor("user-b")).toBe(0);

    persistAcknowledgedCursor("user-b", 7);
    expect(readStoredCursor("user-a")).toBe(120);
    expect(readStoredCursor("user-b")).toBe(7);
  });

  it("keeps the last acknowledged sequence for a reconnect", () => {
    persistAcknowledgedCursor("user-a", 42);
    persistAcknowledgedCursor("user-a", 30);
    expect(readStoredCursor("user-a")).toBe(42);
  });

  it("ignores the legacy shared cursor", () => {
    sessionStorage.setItem("notifications:last-acknowledged-sequence", "500");
    expect(readStoredCursor("user-a")).toBe(0);
  });
});
