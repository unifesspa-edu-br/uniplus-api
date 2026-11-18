#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

verificar_gate() {
    local projeto="$1"
    local esperado="$2"
    local atual
    atual="$(dotnet msbuild "$projeto" -nologo -getProperty:MetricasBarradas)"
    if [[ "$atual" != "$esperado" ]]; then
        printf 'Gate incorreto em %s: esperado "%s", obtido "%s"\n' "$projeto" "$esperado" "$atual" >&2
        exit 1
    fi
}

for projeto in src/ingresso/Unifesspa.UniPlus.Ingresso.*/*.csproj tests/Unifesspa.UniPlus.Ingresso.*/*.csproj; do
    verificar_gate "$projeto" ""
done

verificar_gate src/portal/Unifesspa.UniPlus.Portal.Domain/Unifesspa.UniPlus.Portal.Domain.csproj true
verificar_gate src/shared/Unifesspa.UniPlus.Kernel/Unifesspa.UniPlus.Kernel.csproj ""
printf 'Gate de métricas caracterizado com sucesso.\n'
