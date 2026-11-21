namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Serializacao;

internal static class RepeticaoDoItemBasico
{
    /// <summary>
    /// Recusa o item básico enviado que difere do de <paramref name="referencia"/>: o conjunto
    /// básico não é alterado pela escrita, só repetido ou omitido. A ordem e o rótulo são do
    /// conjunto, não do cliente: o item gravado antes de uma mudança neles continua repetível.
    /// </summary>
    internal static List<FieldError> Conferir(IReadOnlyList<FatoColetadoInput> enviados, IReadOnlyList<FatoColetadoInput> referencia)
    {
        Dictionary<string, string> canonicos = referencia.ToDictionary(static r => r.FatoCodigo, static r => FormaCanonica(SemOrdemNemRotulo(r)), StringComparer.Ordinal);
        return [.. enviados.Index()
            .Where(p => p.Item is { } item && ConjuntoBasicoDaInscricao.Fatos.Contains(item.FatoCodigo) && !RepeteOCanonico(item, canonicos))
            .Select(static p => DadoBasicoAlterado(p.Index, p.Item!.FatoCodigo))];
    }

    private static bool RepeteOCanonico(FatoColetadoInput item, Dictionary<string, string> canonicos) =>
        canonicos.TryGetValue(item.FatoCodigo, out string? canonico)
        && !TemValorAusente(item)
        && string.Equals(canonico, FormaCanonica(SemOrdemNemRotulo(item)), StringComparison.Ordinal);

    private static FieldError DadoBasicoAlterado(int indice, string fatoCodigo) =>
        new($"itens[{indice}]", new DomainError(
            EstruturaFormularioErrorCodes.DadoBasicoAlterado,
            $"O item '{fatoCodigo}' é do conjunto básico de dados do candidato: pode ser repetido como está, ou omitido, mas não alterado."));

    private static FatoColetadoInput SemOrdemNemRotulo(FatoColetadoInput item) =>
        item with { Ordem = 0, Apresentacao = item.Apresentacao is null ? null : item.Apresentacao with { Rotulo = string.Empty } };

    /// <summary>
    /// A forma canônica do item na entrada, para conferir que o item básico enviado é o de
    /// referência: os textos aparados, as listas vazias como nulas e o token da obrigatoriedade.
    /// </summary>
    private static string FormaCanonica(FatoColetadoInput item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return JsonSerializer.Serialize(item with
        {
            EtapaCodigo = string.IsNullOrWhiteSpace(item.EtapaCodigo) ? null : item.EtapaCodigo.Trim(),
            Apresentacao = ApresentacaoCanonica(item.Apresentacao),
            Regras = RegrasCanonicas(item.Regras),
        });
    }

    private static ApresentacaoDoCampoInput? ApresentacaoCanonica(ApresentacaoDoCampoInput? apresentacao) =>
        apresentacao is null
            ? null
            : apresentacao with
            {
                Rotulo = apresentacao.Rotulo?.Trim() ?? string.Empty,
                Ajuda = string.IsNullOrWhiteSpace(apresentacao.Ajuda) ? null : apresentacao.Ajuda.Trim(),
            };

    private static RegrasDoCampoInput RegrasCanonicas(RegrasDoCampoInput? regras) => new(
        regras?.Exibicao is { Count: > 0 } exibicao ? exibicao : null,
        ObrigatoriedadeCanonica(regras?.Obrigatoriedade),
        regras?.Restricoes is { Count: > 0 } restricoes ? [.. restricoes.Select(static r => RestricaoCanonica(r)!)] : null,
        regras?.Impedimento);

    private static ObrigatoriedadeInput ObrigatoriedadeCanonica(ObrigatoriedadeInput? obrigatoriedade) => new(
        obrigatoriedade?.Tipo is { } tipo && !string.IsNullOrWhiteSpace(tipo) ? tipo.Trim() : PredicadoDnfJson.ObrigatoriedadeSempre,
        obrigatoriedade?.Predicado is { Count: > 0 } predicado ? predicado : null);

    /// <summary>
    /// A restrição na forma que a escrita grava: lida pelo domínio e devolvida à entrada, para que o
    /// deslocamento omitido de um limite de data e o explícito em zero sejam a mesma restrição. A
    /// restrição ausente ou que o domínio recusa fica como veio, e a recusa sai na própria escrita.
    /// </summary>
    private static RestricaoValorInput? RestricaoCanonica(RestricaoValorInput? restricao) =>
        restricao is not null && EntradaDeRegras.Restricao(restricao) is { IsSuccess: true } lida
            ? EntradaDeRegras.ParaEntrada(lida.Value!)
            : restricao;

    /// <summary>A condição sem valor não tem forma canônica, e o item que a traz não é o de referência.</summary>
    private static bool TemValorAusente(FatoColetadoInput item) =>
        (item.Regras?.Predicados() ?? [])
            .SelectMany(static predicado => predicado ?? [])
            .SelectMany(static clausula => clausula ?? [])
            .Any(static condicao => condicao is not null && condicao.Valor.ValueKind == JsonValueKind.Undefined);
}
