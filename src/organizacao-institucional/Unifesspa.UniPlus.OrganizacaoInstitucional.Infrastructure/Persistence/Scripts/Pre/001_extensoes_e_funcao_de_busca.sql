-- Extensões são globais por banco; immutable_unaccent vive em public (o mapeamento HasDbFunction fixa HasSchema("public")).
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Wrapper IMMUTABLE de unaccent(), exigido por índices de expressão.
CREATE OR REPLACE FUNCTION public.immutable_unaccent(text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE PARALLEL SAFE STRICT AS
$$ SELECT public.unaccent('public.unaccent', $1) $$;
