namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Corpo da manifestação técnica sobre a proteção de um item com aprovação solicitada. Declara no contrato o
/// vocabulário do cadastro e do parecer; a recusa do valor fora dele continua sendo do handler.
/// </summary>
/// <param name="Ciclo">O ciclo de aprovação lido em <c>aprovacao.ciclo</c> do item.</param>
/// <param name="Texto">Até 2.000 caracteres; obrigatório em <c>RECOMENDA_AJUSTE</c> e <c>INADEQUADA</c>.</param>
public sealed record RegistrarManifestacaoDaProtecaoRequest(
    [property: VocabularioFechado(CadastrosDaProtecaoDoCatalogo.TipoDocumento, CadastrosDaProtecaoDoCatalogo.Fato)]
    string? Cadastro,
    Guid ItemId,
    int? Ciclo,
    [property: VocabularioFechado(
        PareceresDaManifestacaoDoCatalogo.Adequada,
        PareceresDaManifestacaoDoCatalogo.RecomendaAjuste,
        PareceresDaManifestacaoDoCatalogo.Inadequada)]
    string? Parecer,
    string? Texto = null)
{
    public RegistrarManifestacaoDaProtecaoCommand ParaComando() => new(Cadastro, ItemId, Ciclo, Parecer, Texto);
}
