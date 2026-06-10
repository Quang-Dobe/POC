import { UserManager, WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts';
import { config } from '@/config';

const settings: UserManagerSettings = {
  authority: config.authority,
  client_id: config.clientId,
  redirect_uri: config.redirectUri,
  post_logout_redirect_uri: window.location.origin + '/',
  response_type: 'code',
  scope: config.scope,

  userStore: new WebStorageStateStore({ store: window.sessionStorage }),

  automaticSilentRenew: false,
  monitorSession: false,
};

export const userManager = new UserManager(settings);
