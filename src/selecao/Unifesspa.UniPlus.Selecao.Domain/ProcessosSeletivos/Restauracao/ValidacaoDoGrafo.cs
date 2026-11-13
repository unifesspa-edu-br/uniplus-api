namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDoGrafo
{
    /// <summary>
    /// A primeira recusa do grafo congelado, ou <see langword="null"/> quando ele pode ser reposto.
    /// A ordem das conferências é o contrato: ela decide qual causa o operador recebe quando o
    /// grafo tem mais de uma. O teto de itens dos formulários fica entre o teto e as demais
    /// conferências do desempate.
    /// </summary>
    internal static DomainError? Validar(GrafoConfiguracao grafo) =>
        ValidacaoDasEtapasRestauradas.Recusa(grafo)
        ?? ValidacaoDasVagasRestauradas.Recusa(grafo)
        ?? ValidacaoDoDesempateRestaurado.CriteriosEmExcesso(grafo)
        ?? ValidacaoDosFormulariosRestaurados.ItensEmExcesso(grafo)
        ?? ValidacaoDoDesempateRestaurado.RecusaDosCriterios(grafo)
        ?? ValidacaoDaClassificacaoRestaurada.Recusa(grafo)
        ?? ValidacaoDoCronogramaRestaurado.Recusa(grafo)
        ?? ValidacaoDaArvoreRestaurada.Validar(grafo);
}
