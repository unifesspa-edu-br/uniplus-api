namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel que as condições da canonicalização conferem, na mesma ordem dela.</summary>
internal static class ItensDasCondicoesDaCanonicalizacao
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, CadastrosDaConfiguracao cadastros) =>
    [
        .. ItensDasExigenciasDocumentais.Avaliar(estado, cadastros.FatosDeModalidade, cadastros.Protecao),
        .. ItensDaColetaDeFatos.Avaliar(estado, cadastros.FatosDeModalidade),
        .. ItensDosFormularios.Avaliar(estado),
        .. ItensDaDistribuicaoDeVagas.Avaliar(estado),
        .. ItensDoDesempate.Avaliar(estado),
    ];
}
