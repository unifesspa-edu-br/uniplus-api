namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Corpo da decisão do Representante do controlador sobre a proteção de dados de um tipo de documento ou fato do
/// candidato. Declara no contrato o vocabulário do cadastro e da decisão; a recusa do valor fora dele continua sendo do
/// handler.
/// </summary>
/// <param name="Ciclo">O <c>aprovacao.ciclo</c> lido no item.</param>
/// <param name="ManifestacoesNoCiclo">
/// O <c>aprovacao.manifestacoesNoCiclo</c> lido no item: se outra manifestação técnica foi registrada depois da leitura,
/// a decisão responde 409 e precisa ser refeita depois de reler o item.
/// </param>
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
    int? Ciclo,
    int? ManifestacoesNoCiclo,
    string? ClassificacaoRecomendada = null,
    string? Justificativa = null)
{
    public DecidirProtecaoDoCatalogoCommand ParaComando() => new(
        Cadastro, ItemId, Decisao, ClassificacaoProtecao, HipoteseLegal, FinalidadeTratamento, Ciclo, ManifestacoesNoCiclo,
        ClassificacaoRecomendada, Justificativa);
}
