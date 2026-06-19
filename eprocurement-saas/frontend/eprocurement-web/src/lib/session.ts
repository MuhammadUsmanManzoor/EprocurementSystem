import type { AuthenticatedUser } from "./api";

const tokenKey = "eprocurement.token";
const userKey = "eprocurement.user";

export function saveSession(accessToken: string, user: AuthenticatedUser) {
  localStorage.setItem(tokenKey, accessToken);
  localStorage.setItem(userKey, JSON.stringify(user));
}

export function getCurrentUser(): AuthenticatedUser | null {
  const rawUser = localStorage.getItem(userKey);
  return rawUser ? (JSON.parse(rawUser) as AuthenticatedUser) : null;
}

export function clearSession() {
  localStorage.removeItem(tokenKey);
  localStorage.removeItem(userKey);
}
