namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A distribuição de vagas do grafo restaurado, pelas mesmas invariantes de <c>DefinirDistribuicaoVagas</c> e de
/// <c>DefinirOpcoesDeCurso</c>: a restauração repõe as duas dimensões sem passar por elas, e sem o espelho reintroduziria
/// um estado que a gravação recusa.
/// </summary>
internal static class ValidacaoDasVagasRestauradas
{
    internal static DomainError? Validar(GrafoConfiguracao grafo) =>
        DistribuicaoVazia(grafo)
        ?? OfertaRepetida(grafo)
        ?? NumeroDeOpcoesInvalido(grafo)
        ?? OpcoesAcimaDasOfertas(grafo)
        ?? AcaoQuandoIndeferidoDivergente(grafo)
        ?? RegrasDaDistribuicaoDeVagas.RegraDeAplicabilidadeDivergenteEntreOfertas(grafo.DistribuicaoVagas)
        ?? RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(grafo.DistribuicaoVagas)
        ?? RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(grafo.DistribuicaoVagas);

    private static DomainError? DistribuicaoVazia(GrafoConfiguracao grafo) =>
        grafo.DistribuicaoVagas.Count == 0
            ? new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada.")
            : null;

    private static DomainError? OfertaRepetida(GrafoConfiguracao grafo) =>
        grafo.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId).Distinct().Count() != grafo.DistribuicaoVagas.Count
            ? new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo.")
            : null;

    // Versão publicada sempre declarou o número, na faixa que DefinirOpcoesDeCurso aceita: ausência ou valor fora dela
    // é envelope que o domínio não produziria.
    private static DomainError? NumeroDeOpcoesInvalido(GrafoConfiguracao grafo) =>
        grafo.NumeroDeOpcoesDeCurso is not (>= 1 and <= ProcessoSeletivo.MaximoDeOpcoesDeCurso)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido,
                "O número de opções de curso da configuração restaurada deve ser 1 ou 2.")
            : null;

    private static DomainError? OpcoesAcimaDasOfertas(GrafoConfiguracao grafo) =>
        RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(grafo.NumeroDeOpcoesDeCurso, grafo.DistribuicaoVagas.Count)
            ? RegrasDaDistribuicaoDeVagas.ErroDeOpcoesDeCursoAcimaDasOfertas()
            : null;

    private static DomainError? AcaoQuandoIndeferidoDivergente(GrafoConfiguracao grafo) =>
        RegrasDaDistribuicaoDeVagas.HaAcaoQuandoIndeferidoDivergenteEntreOfertas(grafo.DistribuicaoVagas)
            ? new DomainError(
                "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
                "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.")
            : null;
}
