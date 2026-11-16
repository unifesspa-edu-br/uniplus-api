namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas das regras do certame, reunidas por família: distribuição e quadro de vagas,
/// modalidades ofertadas, desempate, bônus, cascata de remanejamento e classificação. A ordem vale: o
/// registro global guarda o último mapeamento de um código repetido.
/// </summary>
internal static class ErrosDaOfertaEDaClassificacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. ErrosDaDistribuicaoDeVagas.Mapeamentos(),
        .. ErrosDoQuadroDeVagas.Mapeamentos(),
        .. ErrosDasModalidadesOfertadas.Mapeamentos(),
        .. ErrosDoDesempate.Mapeamentos(),
        .. ErrosDoBonusRegional.Mapeamentos(),
        .. ErrosDaCascataDeRemanejamento.Mapeamentos(),
        .. ErrosDaClassificacao.Mapeamentos(),
    ];
}
