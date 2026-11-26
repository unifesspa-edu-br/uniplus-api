namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

internal static class LeituraDaManifestacaoDeInteresse
{
    internal static ManifestacaoDeInteresse? Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        string? codigo = leitor.TextoOpcional(payload, DeclaracaoDaManifestacaoDeInteresse.Campo, "$", LimitesDoEnvelope.Token);
        return leitor.Falhou ? null : Declarar(leitor, codigo);
    }

    private static ManifestacaoDeInteresse? Declarar(LeitorEnvelope leitor, string? codigo)
    {
        Result<ManifestacaoDeInteresse?> resultado = DeclaracaoDaManifestacaoDeInteresse.Declarar(codigo);
        return resultado.IsFailure ? leitor.Propagar<ManifestacaoDeInteresse?>(resultado.Errors, "") : resultado.Value;
    }
}
