namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Corpo da decisão da proteção de dados sobre um tipo de documento ou fato do candidato. Declara no contrato o
/// vocabulário do cadastro e da decisão; a recusa do valor fora dele continua sendo do handler.
/// </summary>
/// <param name="Justificativa">
/// Obrigatória em <c>REPROVAR</c>; em <c>APROVAR</c> e <c>RECOMENDAR_AJUSTE</c>, opcional, salvo diante de manifestação
/// técnica desfavorável vigente, que a exige. Fica registrada no ato da decisão.
/// </param>
public sealed record DecidirProtecaoDoCatalogoRequest(
    [property: VocabularioFechado(CadastrosDaProtecaoDoCatalogo.TipoDocumento, CadastrosDaProtecaoDoCatalogo.Fato)]
    string? Cadastro,
    Guid ItemId,
    [property: VocabularioFechado(
        DecisoesDaProtecaoDoCatalogo.Aprovar,
        DecisoesDaProtecaoDoCatalogo.RecomendarAjuste,
        DecisoesDaProtecaoDoCatalogo.Reprovar)]
    string? Decisao,
    string? ClassificacaoProtecao,
    string? HipoteseLegal,
    string? FinalidadeTratamento,
    string? ClassificacaoRecomendada = null,
    string? Justificativa = null)
{
    public DecidirProtecaoDoCatalogoCommand ParaComando() => new(
        Cadastro, ItemId, Decisao, ClassificacaoProtecao, HipoteseLegal, FinalidadeTratamento, ClassificacaoRecomendada, Justificativa);
}
