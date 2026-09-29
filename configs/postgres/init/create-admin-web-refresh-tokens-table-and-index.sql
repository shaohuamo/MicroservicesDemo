-- Switch to the Admin Web logical database created by create-admin-web-database.sql.
\c adminwebdatabase;

CREATE TABLE IF NOT EXISTS public.auth_refresh_tokens
(
    id uuid NOT NULL,
    user_id text NOT NULL,
    refresh_token text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at timestamp with time zone,
    CONSTRAINT auth_refresh_tokens_pkey PRIMARY KEY (id)
);

COMMENT ON COLUMN public.auth_refresh_tokens.expires_at IS
    'Estimated absolute expiration based on the configured refresh token lifetime; IdentityServer is authoritative.';

CREATE INDEX IF NOT EXISTS ix_auth_refresh_tokens_user_id
    ON public.auth_refresh_tokens (user_id);
