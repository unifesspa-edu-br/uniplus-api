namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que vale entre as ofertas do processo, sem consultar a configuração declarada: o processo tem
/// ofertas, cada curso aparece uma vez, a mesma modalidade tem a mesma ação quando indeferida e a mesma
/// regra de aplicabilidade em todas as ofertas, e o número de opções de curso cabe nas ofertas. Todas
/// as recusas se acumulam (ADR-0125), nessa ordem.
/// </summary>
public sealed class ConferenciaDaOfertaDeVagasTests
{
    private static ModalidadeSelecionada Modalidade(
        string codigo, NaturezaLegalModalidade natureza = NaturezaLegalModalidade.AcaoAfirmativa, string? acaoQuandoIndeferido = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), acaoQuandoIndeferido, "base legal", quantidadeDeclarada: 1).Value!;

    private static DistribuicaoDeVagasDaOferta Oferta(Guid cursoId, params ModalidadeSelecionada[] modalidades) =>
        new(cursoId, modalidades);

    private static IReadOnlyList<string> Causas(IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertas, int? opcoesDeCurso = null) =>
        [.. ConferenciaDaOfertaDeVagas.Conferir(ofertas, opcoesDeCurso).Select(static recusa => recusa.Error.Code)];

    [Fact(DisplayName = "Duas ofertas de cursos diferentes, com as mesmas modalidades iguais, passam sem recusa")]
    public void OfertasCoerentes_SemRecusa() =>
        Causas([Oferta(Guid.CreateVersion7(), Modalidade("IND")), Oferta(Guid.CreateVersion7(), Modalidade("IND"))], opcoesDeCurso: 2)
            .Should().BeEmpty();

    [Fact(DisplayName = "O processo sem oferta nenhuma é recusado, sem campo")]
    public void SemOfertas_Recusa()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDaOfertaDeVagas.Conferir([], numeroDeOpcoesDeCurso: null);

        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == null && recusa.Error.Code == DistribuicaoDeVagasErrorCodes.DistribuicaoVaziaNoProcesso);
    }

    [Fact(DisplayName = "O curso com duas distribuições no processo é recusado")]
    public void CursoRepetido_Recusa()
    {
        Guid curso = Guid.CreateVersion7();

        Causas([Oferta(curso, Modalidade("IND")), Oferta(curso, Modalidade("IND"))])
            .Should().Equal(DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada);
    }

    [Fact(DisplayName = "A mesma modalidade com ações divergentes quando indeferida em ofertas distintas é recusada")]
    public void AcaoQuandoIndeferidoDivergente_Recusa() =>
        Causas(
            [
                Oferta(Guid.CreateVersion7(), Modalidade("IND", acaoQuandoIndeferido: "RECLASSIFICAR_AC")),
                Oferta(Guid.CreateVersion7(), Modalidade("IND", acaoQuandoIndeferido: null)),
            ]).Should().Equal(DistribuicaoDeVagasErrorCodes.AcaoQuandoIndeferidoDivergente);

    [Fact(DisplayName = "A mesma modalidade com regras de aplicabilidade diferentes em ofertas distintas é recusada")]
    public void RegraDeAplicabilidadeDivergente_Recusa() =>
        Causas(
            [
                Oferta(Guid.CreateVersion7(), Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa)),
                Oferta(Guid.CreateVersion7(), Modalidade("IND", NaturezaLegalModalidade.Ampla)),
            ]).Should().Equal(ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente);

    [Theory(DisplayName = "As duas opções de curso com uma oferta só são recusadas; com duas ou mais, aceitas")]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void OpcoesDeCurso_AcimaDasOfertas(int ofertas, bool recusa)
    {
        DistribuicaoDeVagasDaOferta[] distribuicao =
            [.. Enumerable.Range(0, ofertas).Select(static _ => Oferta(Guid.CreateVersion7(), Modalidade("IND")))];

        Causas(distribuicao, opcoesDeCurso: 2).Should().Equal(recusa ? [ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas] : []);
    }

    [Fact(DisplayName = "Uma opção de curso, ou nenhuma declarada, cabe em qualquer número de ofertas")]
    public void OpcoesDeCurso_NaoDeclaradasOuUma_Cabem()
    {
        DistribuicaoDeVagasDaOferta[] umaOferta = [Oferta(Guid.CreateVersion7(), Modalidade("IND"))];

        Causas(umaOferta, opcoesDeCurso: 1).Should().BeEmpty();
        Causas(umaOferta, opcoesDeCurso: null).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas se acumulam, uma por causa, na ordem das conferências")]
    public void Recusas_Acumulam_NaOrdemDasConferencias()
    {
        Guid curso = Guid.CreateVersion7();

        Causas(
            [
                Oferta(curso, Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, "RECLASSIFICAR_AC")),
                Oferta(curso, Modalidade("IND", NaturezaLegalModalidade.Ampla)),
            ])
            .Should().Equal(
                DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada,
                DistribuicaoDeVagasErrorCodes.AcaoQuandoIndeferidoDivergente,
                ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente);
    }
}
