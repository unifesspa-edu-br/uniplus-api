namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os tipos de decisão que uma fase ou etapa declara produzir, no envelope: o mesmo bloco na fase e na etapa.
/// A chave está sempre presente, vazia quando o lugar não declara tipo, e os códigos seguem a ordem alfabética
/// ordinal, de modo que a mesma declaração em outra ordem produz os mesmos bytes.
/// </summary>
internal static class TiposDeDecisaoNoEnvelope
{
    internal static JsonArray Serializar(IReadOnlyList<TipoMotivoDeDecisao> tipos) =>
        new([.. EmOrdemDoEnvelope(tipos).Select(static tipo => (JsonNode)tipo.Codigo())]);

    /// <summary>Grava no contexto do lugar os rótulos dos tipos, na ordem dos códigos escritos por <see cref="Serializar"/>.</summary>
    internal static void AcrescentarContexto(JsonObject lugar, string campo, IReadOnlyList<TipoMotivoDeDecisao> tipos) =>
        ContextoDosRotulos.AcrescentarLista(lugar, campo, EmOrdemDoEnvelope(tipos));

    private static IEnumerable<TipoMotivoDeDecisao> EmOrdemDoEnvelope(IReadOnlyList<TipoMotivoDeDecisao> tipos) =>
        tipos.OrderBy(static tipo => tipo.Codigo(), StringComparer.Ordinal);

    /// <summary>
    /// Lê os tipos como o envelope os escreve: do vocabulário, sem repetição e em ordem. A forma que a escrita
    /// nunca produz é recusada, porque reordenar ou descartar tipo na leitura mudaria o que foi congelado. O mesmo bloco vale
    /// para a lista que a cópia do tipo de banca guarda, sob a chave <paramref name="campo"/>.
    /// </summary>
    internal static IReadOnlyList<string> Ler(LeitorEnvelope leitor, JsonObject lugar, string pathDoLugar, string campo = TiposDeDecisaoDeclarados.Campo)
    {
        IReadOnlyList<string> lidos = leitor.Textos(lugar, campo, pathDoLugar);
        if (leitor.Falhou || lidos.SequenceEqual(TiposDeDecisaoDeclarados.Canonicos(lidos).Select(static tipo => tipo.Codigo()), StringComparer.Ordinal))
        {
            return lidos;
        }

        return leitor.Malformado<IReadOnlyList<string>>(
            $"{pathDoLugar}.{campo}",
            "os tipos de decisão devem ser do vocabulário, sem repetição e em ordem alfabética.") ?? [];
    }
}
