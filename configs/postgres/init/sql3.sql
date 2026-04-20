-- Enable extension
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- Create monitoring user
CREATE USER exporter WITH PASSWORD 'exporterpass';

-- Grant monitoring role
GRANT pg_monitor TO exporter;