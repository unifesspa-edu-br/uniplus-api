-- Pré-requisito das exclusion constraints de vigência e de ordem, que comparam uuid, inteiro e boolean com `=` em índice
-- GiST. Extensões são globais por banco: existe aqui, uma vez, e nenhum módulo a repete.
CREATE EXTENSION IF NOT EXISTS btree_gist;
