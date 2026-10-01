-- Run on fresh Compose initialization or explicitly against existing clusters.
\c adminwebdatabase;
\getenv gateway_auth_password GATEWAY_DB_PASSWORD

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gateway_auth_reader') THEN
        CREATE ROLE gateway_auth_reader LOGIN;
    END IF;
END $$;
ALTER ROLE gateway_auth_reader PASSWORD :'gateway_auth_password';
GRANT CONNECT ON DATABASE adminwebdatabase TO gateway_auth_reader;
GRANT USAGE ON SCHEMA public TO gateway_auth_reader;
GRANT SELECT (id) ON public.auth_refresh_tokens TO gateway_auth_reader;
