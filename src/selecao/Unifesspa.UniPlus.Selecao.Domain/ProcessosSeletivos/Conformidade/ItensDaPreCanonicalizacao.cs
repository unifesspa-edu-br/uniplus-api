namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel que o gate de pré-canonicalização confere, na mesma ordem dele.</summary>
internal static class ItensDaPreCanonicalizacao
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(
        EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade, ProtecaoVigenteDasExigencias? protecao) =>
    [
        .. ItensDasExigenciasDocumentais.Avaliar(estado, fatosDeModalidade, protecao),
        .. ItensDaColetaDeFatos.Avaliar(estado, fatosDeModalidade),
        .. ItensDosFormularios.Avaliar(estado),
        .. ItensDaDistribuicaoDeVagas.Avaliar(estado),
        .. ItensDoDesempate.Avaliar(estado),
    ];
}
