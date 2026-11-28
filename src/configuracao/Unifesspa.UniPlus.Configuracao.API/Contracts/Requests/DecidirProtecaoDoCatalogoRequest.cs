namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Corpo da decisão do Representante do controlador sobre a proteção de dados de um tipo de documento ou fato do
/// candidato. Declara no contrato o vocabulário do cadastro e da decisão; a recusa do valor fora dele continua sendo do
/// handler.
/// </summary>
/// <param name="Decisao"><c>APROVACAO</c>, <c>AJUSTE_RECOMENDADO</c> ou <c>DEVOLUCAO</c>.</param>
/// <param name="Ciclo">O <c>aprovacao.ciclo</c> lido no item.</param>
/// <param name="ManifestacoesNoCiclo">
/// O <c>aprovacao.manifestacoesNoCiclo</c> lido no item: se outra manifestação técnica foi registrada depois da leitura,
/// a decisão responde 409 e precisa ser refeita depois de reler o item.
/// </param>
/// <param name="Justificativa">
/// Obrigatória em <c>DEVOLUCAO</c>; em <c>APROVACAO</c> e <c>AJUSTE_RECOMENDADO</c>, opcional, salvo diante de manifestação
/// técnica desfavorável vigente, que a exige. Fica registrada no ato da decisão.
/// </param>
public sealed record DecidirProtecaoDoCatalogoRequest(
    [property: VocabularioFechado(CadastrosDaProtecaoDoCatalogo.TipoDocumento, CadastrosDaProtecaoDoCatalogo.Fato)]
    string? Cadastro,
    Guid ItemId,
    [property: VocabularioFechado(
        AtosDaProtecao.TokenAprovacao,
        AtosDaProtecao.TokenAjusteRecomendado,
        AtosDaProtecao.TokenDevolucao)]
    string? Decisao,
    [property: AceitaCodigosDe<ClassificacaoDeProtecao>] string? ClassificacaoProtecao,
    [property: AceitaCodigosDe<HipoteseLegalTratamento>] string? HipoteseLegal,
    string? FinalidadeTratamento,
    int? Ciclo,
    int? ManifestacoesNoCiclo,
    [property: AceitaCodigosDe<ClassificacaoDeProtecao>] string? ClassificacaoRecomendada = null,
    string? Justificativa = null)
{
    // O corpo continua plano, porque é o contrato; o comando recebe as partes agrupadas, ainda não conferidas.
    public DecidirProtecaoDoCatalogoCommand ParaComando() => new(
        new ItemInformado(Cadastro, ItemId),
        new DecisaoInformada(Decisao, ClassificacaoRecomendada, Justificativa),
        new ProtecaoInformada(ClassificacaoProtecao, HipoteseLegal, FinalidadeTratamento),
        new LeituraInformada(Ciclo, ManifestacoesNoCiclo));
}
