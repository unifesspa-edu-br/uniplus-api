namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os tipos de decisão que uma fase ou etapa declara produzir, no envelope: o mesmo bloco na fase e na etapa.
/// A chave está sempre presente, vazia quando o lugar não declara tipo, e os códigos seguem a ordem alfabética
/// ordinal, de modo que a mesma declaração em outra ordem produz os mesmos bytes.
/// </summary>
internal static class TiposDeDecisaoNoEnvelope
{
    internal static JsonArray Serializar(IReadOnlyList<string> tipos) =>
        new([.. tipos.Order(StringComparer.Ordinal).Select(static tipo => (JsonNode)tipo)]);

    /// <summary>
    /// Lê os tipos como o envelope os escreve: do vocabulário, sem repetição e em ordem. A forma que a escrita
    /// nunca produz é recusada, porque reordenar ou descartar tipo na leitura mudaria o que foi congelado.
    /// </summary>
    internal static IReadOnlyList<string> Ler(LeitorEnvelope leitor, JsonObject lugar, string pathDoLugar)
    {
        IReadOnlyList<string> lidos = leitor.Textos(lugar, TiposDeDecisaoDeclarados.Campo, pathDoLugar);
        if (leitor.Falhou || lidos.SequenceEqual(TiposDeDecisaoDeclarados.Canonicos(lidos), StringComparer.Ordinal))
        {
            return lidos;
        }

        return leitor.Malformado<IReadOnlyList<string>>(
            $"{pathDoLugar}.{TiposDeDecisaoDeclarados.Campo}",
            "os tipos de decisão devem ser do vocabulário, sem repetição e em ordem alfabética.") ?? [];
    }
}
