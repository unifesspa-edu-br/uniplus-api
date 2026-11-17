namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O que vale entre as ofertas de um processo, sem consultar a configuração declarada: ao menos uma
/// oferta, cada curso uma vez só, a mesma ação quando indeferido e a mesma regra de aplicabilidade para
/// a mesma modalidade em todas as ofertas, e o número de opções de curso que as ofertas comportam. A
/// escrita e a reidratação do envelope congelado perguntam isto sobre a mesma declaração e recebem as
/// mesmas causas. As recusas não têm campo (o corpo da operação é só a lista de ofertas) e se acumulam
/// (ADR-0125), na ordem em que as conferências estão acima.
/// </summary>
public static class ConferenciaDaOfertaDeVagas
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas, int? numeroDeOpcoesDeCurso)
    {
        ArgumentNullException.ThrowIfNull(ofertas);

        return
        [
            .. new[]
            {
                OfertasAusentes(ofertas),
                CursoRepetido(ofertas),
                AcaoQuandoIndeferidoDivergente(ofertas),
                RegraDeAplicabilidadeDivergente(ofertas),
                OpcoesDeCursoAcimaDasOfertas(ofertas, numeroDeOpcoesDeCurso),
            }.OfType<DomainError>().Select(static erro => new FieldError(null, erro)),
        ];
    }

    private static DomainError? OfertasAusentes(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas) =>
        ofertas.Count == 0
            ? new DomainError(
                DistribuicaoDeVagasErrorCodes.DistribuicaoVaziaNoProcesso,
                "O processo deve ter ao menos uma distribuição de vagas configurada.")
            : null;

    private static DomainError? CursoRepetido(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas) =>
        ofertas.Select(static oferta => oferta.OfertaCursoOrigemId).Distinct().Count() != ofertas.Count
            ? new DomainError(
                DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada,
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo.")
            : null;

    // O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas
    // distintas.
    private static DomainError? AcaoQuandoIndeferidoDivergente(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas) =>
        ModalidadesPorCodigo(ofertas).Any(static grupo => grupo.Select(static m => m.AcaoQuandoIndeferido).Distinct().Count() > 1)
            ? new DomainError(
                DistribuicaoDeVagasErrorCodes.AcaoQuandoIndeferidoDivergente,
                "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.")
            : null;

    // A modalidade tem uma regra de aplicabilidade só no processo, porque é dela que sai quem concorre a
    // ela (ADR-0144). Todas as distribuições são copiadas do cadastro na mesma gravação, então duas cópias
    // diferentes da mesma modalidade só aparecem se o cadastro mudou no meio dela, ou num envelope adulterado.
    private static DomainError? RegraDeAplicabilidadeDivergente(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas) =>
        ModalidadesPorCodigo(ofertas)
            .FirstOrDefault(static grupo => grupo.Skip(1).Any(m => !m.RegraDeAplicabilidade.MesmoConteudo(grupo.First().RegraDeAplicabilidade)))
            is { } divergente
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente,
                $"A modalidade {divergente.Key} foi copiada com regras de aplicabilidade diferentes em ofertas do processo; grave a distribuição de vagas de novo.")
            : null;

    private static DomainError? OpcoesDeCursoAcimaDasOfertas(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas, int? numeroDeOpcoesDeCurso) =>
        RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(numeroDeOpcoesDeCurso, ofertas.Count)
            ? RegrasDaDistribuicaoDeVagas.ErroDeOpcoesDeCursoAcimaDasOfertas()
            : null;

    private static IEnumerable<IGrouping<string, ModalidadeSelecionada>> ModalidadesPorCodigo(
        IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas) =>
        ofertas.SelectMany(static oferta => oferta.Modalidades).GroupBy(static m => m.Codigo, StringComparer.Ordinal);
}
