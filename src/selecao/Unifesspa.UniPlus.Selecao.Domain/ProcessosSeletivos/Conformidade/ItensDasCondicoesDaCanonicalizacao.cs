namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel que as condições da canonicalização conferem, na mesma ordem dela.</summary>
internal static class ItensDasCondicoesDaCanonicalizacao
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
    [
        .. ItensDasExigenciasDocumentais.Avaliar(configuracao, cadastros.FatosDeModalidade, cadastros.Protecao),
        .. ItensDaColetaDeFatos.Avaliar(configuracao, cadastros),
        .. ItensDosFormularios.Avaliar(configuracao),
        .. ItensDaDistribuicaoDeVagas.Avaliar(configuracao),
        .. ItensDoDesempate.Avaliar(configuracao),
    ];
}
