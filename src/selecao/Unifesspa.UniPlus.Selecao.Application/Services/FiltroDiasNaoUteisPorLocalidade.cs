namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Regra de abrangência decidida em 12/08/2026 para contagem de dias úteis (issue
/// #1113): a localidade que governa a contagem de um Processo Seletivo é a da
/// <b>unidade administradora</b> — não a de cada campus de oferta, que pode variar
/// dentro do mesmo processo.
/// </summary>
/// <remarks>
/// <c>NACIONAL</c> e <c>INSTITUCIONAL</c> contam sempre — nenhum dos dois é escopado
/// por município/UF no cadastro de origem (<c>Abrangencia</c>, módulo Configuração);
/// <c>INSTITUCIONAL</c> é o recesso da própria Unifesspa, sem correspondência em
/// calendário civil, então "vale para toda a instituição" é a única leitura
/// coerente com o modelo — decisão registrada aqui à falta de deliberação formal
/// separada, revisável se o PO/jurídico decidir diferente. <c>ESTADUAL</c> conta
/// quando a UF bate; <c>MUNICIPAL</c>, quando o código IBGE bate. Sem cidade
/// cadastrada na unidade administradora (snapshot congelado antes da issue #1114),
/// só <c>NACIONAL</c>/<c>INSTITUCIONAL</c> contam — mas
/// <c>DefinirCronogramaFasesCommandHandler</c> recusa declarar <c>DiasUteis</c>
/// nesse caso (ver <c>RegraRecursoFase.PrazoEmDiasUteisSemLocalidade</c>), então esse
/// fallback nunca é exercido pelo caminho produtivo — só documenta o comportamento
/// do filtro em isolamento.
/// </remarks>
internal static class FiltroDiasNaoUteisPorLocalidade
{
    public static IReadOnlyCollection<DateOnly> Filtrar(
        IReadOnlyList<DiaNaoUtilView> diasNaoUteis, string? cidadeCodigoIbge, string? cidadeUf)
    {
        ArgumentNullException.ThrowIfNull(diasNaoUteis);
        return [.. diasNaoUteis
            .Where(dia => AplicaLocalidade(dia, cidadeCodigoIbge, cidadeUf))
            .Select(dia => dia.Data)];
    }

    private static bool AplicaLocalidade(DiaNaoUtilView dia, string? cidadeCodigoIbge, string? cidadeUf) =>
        dia.Abrangencia switch
        {
            "NACIONAL" or "INSTITUCIONAL" => true,
            "ESTADUAL" => cidadeUf is not null && string.Equals(dia.Uf, cidadeUf, StringComparison.OrdinalIgnoreCase),
            "MUNICIPAL" => cidadeCodigoIbge is not null && string.Equals(dia.MunicipioIbge, cidadeCodigoIbge, StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Abrangência de dia não útil desconhecida: '{dia.Abrangencia}'."),
        };
}
