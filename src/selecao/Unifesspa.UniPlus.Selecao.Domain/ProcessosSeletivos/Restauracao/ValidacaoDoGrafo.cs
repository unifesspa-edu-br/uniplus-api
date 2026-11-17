namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A conferência de integridade e de referência do grafo restaurado, família por família na ordem em que a gravação as aplica,
/// com todas as causas acumuladas. A completude não entra: o envelope já a atendeu quando foi publicado, e reconferi-la com a
/// regra de hoje reinterpretaria o passado. As regras falam o nome do corpo do pedido, e o campo de cada causa que sai daqui é o
/// do documento da configuração (<see cref="CamposDoDocumento"/>).
/// </summary>
internal static class ValidacaoDoGrafo
{
    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo) =>
        CamposDoDocumento.Traduzir(
        [
            .. ValidacaoDasEtapasRestauradas.Validar(grafo),
            .. ValidacaoDasVagasRestauradas.Validar(grafo),
            .. ValidacaoDosFormulariosRestaurados.Validar(grafo),
            .. ValidacaoDoDesempateRestaurado.Validar(grafo),
            .. ValidacaoDaClassificacaoRestaurada.Validar(grafo),
            .. ValidacaoDoCronogramaRestaurado.Validar(grafo),
            .. ValidacaoDaArvoreRestaurada.Validar(grafo),
        ]);
}
