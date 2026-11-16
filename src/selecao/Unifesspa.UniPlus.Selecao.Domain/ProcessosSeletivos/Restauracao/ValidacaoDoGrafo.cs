namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A conferência do grafo restaurado, família por família na ordem em que a gravação as aplica.</summary>
internal static class ValidacaoDoGrafo
{
    internal static DomainError? Validar(GrafoConfiguracao grafo) =>
        ValidacaoDasEtapasRestauradas.Validar(grafo)
        ?? ValidacaoDasVagasRestauradas.Validar(grafo)
        ?? ValidacaoDosFormulariosRestaurados.Validar(grafo)
        ?? ValidacaoDoDesempateRestaurado.Validar(grafo)
        ?? ValidacaoDaClassificacaoRestaurada.Validar(grafo)
        ?? ValidacaoDoCronogramaRestaurado.Validar(grafo)
        ?? ValidacaoDaArvoreRestaurada.Validar(grafo);
}
