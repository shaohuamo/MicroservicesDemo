import axios from "axios";

export function isServiceUnavailableError(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return true;
  return !error.response || error.response.status >= 500;
}
