namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

// A restauração repõe a distribuição de vagas e o número de opções de curso sem passar pelas
// operações que as gravam, então confere as mesmas invariantes delas.
internal static class ValidacaoDasVagasRestauradas
{
    internal static DomainError? Recusa(GrafoConfiguracao grafo) =>
        RegrasDaDistribuicaoDeVagas.RecusaDaColecao(grafo.DistribuicaoVagas)
        ?? NumeroDeOpcoesDeCursoForaDaFaixa(grafo)
        ?? OpcoesDeCursoAcimaDasOfertas(grafo)
        ?? RegrasDaDistribuicaoDeVagas.AcaoQuandoIndeferidoDivergenteEntreOfertas(grafo.DistribuicaoVagas)
        ?? RegrasDaDistribuicaoDeVagas.RegraDeAplicabilidadeDivergenteEntreOfertas(grafo.DistribuicaoVagas)
        ?? RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(grafo.DistribuicaoVagas)
        ?? RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(grafo.DistribuicaoVagas);

    // Versão publicada sempre declarou o número: ausência é envelope que o domínio não produziria.
    private static DomainError? NumeroDeOpcoesDeCursoForaDaFaixa(GrafoConfiguracao grafo) =>
        RegrasDaDistribuicaoDeVagas.NumeroDeOpcoesForaDaFaixa(grafo.NumeroDeOpcoesDeCurso)
            ? RegrasDaDistribuicaoDeVagas.ErroDeOpcoesDeCursoInvalido()
            : null;

    private static DomainError? OpcoesDeCursoAcimaDasOfertas(GrafoConfiguracao grafo) =>
        RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(grafo.NumeroDeOpcoesDeCurso, grafo.DistribuicaoVagas.Count)
            ? RegrasDaDistribuicaoDeVagas.ErroDeOpcoesDeCursoAcimaDasOfertas()
            : null;
}
