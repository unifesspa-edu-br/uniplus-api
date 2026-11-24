#!/bin/bash
export MSBUILDDISABLENODEREUSE=1
for p in Kernel Authorization Autorizacao.Application Autorizacao.Domain Regras Configuracao.Domain Configuracao.Application Selecao.Domain Selecao.Application Selecao.Infrastructure Contratos Infrastructure.Core; do
  echo "== $p"; dotnet test tests/Unifesspa.UniPlus.$p.UnitTests -c Debug -m:1 2>&1 | grep -E "Aprovado!|Falha!|Failed |error " | head -8
done
echo "== ArchTests"; dotnet test tests/Unifesspa.UniPlus.ArchTests -c Debug -m:1 2>&1 | grep -E "Aprovado!|Falha!|Failed |error " | head -8
echo "== Selecao.ArchTests"; dotnet test tests/Unifesspa.UniPlus.Selecao.ArchTests -c Debug -m:1 2>&1 | grep -E "Aprovado!|Falha!|Failed |error " | head -8
