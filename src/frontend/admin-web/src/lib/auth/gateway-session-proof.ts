import { createHash, createHmac } from "node:crypto";
import { getToken } from "next-auth/jwt";

export const GATEWAY_PROOF_HEADERS = [
  "x-admin-session-id",
  "x-admin-proof-iat",
  "x-admin-proof-kid",
  "x-admin-proof-sig",
] as const;

export async function getRefreshTokenRecordId(request: Request): Promise<string | null> {
  const cookie = request.headers.get("cookie");
  if (!cookie || !process.env.AUTH_SECRET) return null;

  // Only the Auth.js cookie is considered. getToken must never read a client
  // Authorization header when producing a proof for a forwarded request.
  const cookieRequest = new Request(request.url, { headers: { cookie } });
  const publicUrl = process.env.AUTH_URL ?? process.env.FRONTEND_PUBLIC_URL ?? request.url;
  const token = await getToken({
    req: cookieRequest,
    secret: process.env.AUTH_SECRET,
    secureCookie: new URL(publicUrl).protocol === "https:",
  });
  const id = token?.refreshTokenRecordId;
  return typeof id === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
    ? id.toLowerCase() : null;
}

export function createGatewaySessionProof(recordId: string, accessToken: string): Headers {
  const keyId = process.env.AUTH_GATEWAY_PROOF_KEY_ID;
  const secret = process.env.AUTH_GATEWAY_PROOF_KEY;
  if (!keyId || !secret || !/^[\w-]+$/.test(keyId)) {
    throw new Error("Gateway session proof is not configured.");
  }
  const key = Buffer.from(secret, "base64");
  if (key.length < 32) throw new Error("Gateway session proof key must be at least 32 bytes.");
  const timestamp = String(Math.floor(Date.now() / 1000));
  const tokenHash = createHash("sha256").update(accessToken).digest("base64url");
  const payload = `${keyId}\n${recordId}\n${timestamp}\n${tokenHash}`;
  const signature = createHmac("sha256", key).update(payload).digest("base64url");
  return new Headers({
    "x-admin-session-id": recordId,
    "x-admin-proof-iat": timestamp,
    "x-admin-proof-kid": keyId,
    "x-admin-proof-sig": signature,
  });
}

export function applyGatewaySessionProof(headers: Headers, recordId: string, accessToken: string) {
  for (const name of GATEWAY_PROOF_HEADERS) headers.delete(name);
  createGatewaySessionProof(recordId, accessToken).forEach((value, name) => headers.set(name, value));
}
