namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Toda modalidade que cita outra pelo código — origem da retirada, destino, par ou fallback do
/// remanejamento, ampla concorrência da reclassificação — só vale se a citada está no mesmo conjunto
/// ofertado, e o par cruzado aponta de volta. Todas as recusas se acumulam (ADR-0125).
/// </summary>
public sealed class ConferenciaDasReferenciasEntreModalidadesTests
{
    private static ModalidadeSelecionada Montar(
        string codigo,
        ComposicaoVagasModalidade composicao,
        string? origem,
        RegraRemanejamentoModalidade? remanejamento,
        (string? Destino, string? Par, string? Fallback) alvo,
        string? acaoQuandoIndeferido) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.AcaoAfirmativa, composicao) with { ComposicaoOrigemCodigo = origem, RegraRemanejamento = remanejamento, RemanejamentoDestino = alvo.Destino, RemanejamentoPar = alvo.Par, RemanejamentoFallback = alvo.Fallback, AcaoQuandoIndeferido = acaoQuandoIndeferido, BaseLegal = "base legal" }, 1).Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo,
        ComposicaoVagasModalidade composicao = ComposicaoVagasModalidade.SuplementarAoTotal,
        string? origem = null,
        string? acaoQuandoIndeferido = null) =>
        Montar(codigo, composicao, origem, null, (null, null, null), acaoQuandoIndeferido);

    private static ModalidadeSelecionada ComDestino(string codigo, string destino) =>
        Montar(codigo, ComposicaoVagasModalidade.SuplementarAoTotal, null, RegraRemanejamentoModalidade.DestinoUnico, (destino, null, null), null);

    private static ModalidadeSelecionada Cruzada(string codigo, string par, string? fallback = null) =>
        Montar(codigo, ComposicaoVagasModalidade.SuplementarAoTotal, null, RegraRemanejamentoModalidade.Cruzado, (null, par, fallback), null);

    private static IReadOnlyList<FieldError> Conferir(params ModalidadeSelecionada[] modalidades) =>
        ConferenciaDasReferenciasEntreModalidades.Conferir(DistribuicaoDeVagasDeTeste.FixadaNoEdital(modalidades));

    private static IReadOnlyList<FieldError> ConferirComUmaRecusada(params ModalidadeSelecionada[] aceitas) =>
        ConferenciaDasReferenciasEntreModalidades.Conferir(DistribuicaoDeVagasDeTeste.FixadaNoEdital(aceitas) with
        {
            Recusados = DistribuicaoDeVagasDeTeste.Recusando(DadoDaDistribuicaoDeVagas.Modalidades),
        });

    private static void DeveRecusarSo(IReadOnlyList<FieldError> recusas, string codigo, string trecho) =>
        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == "modalidades" && recusa.Error.Code == codigo && recusa.Error.Message.Contains(trecho, StringComparison.Ordinal));

    [Fact(DisplayName = "As modalidades que só citam outras do mesmo conjunto passam sem recusa")]
    public void ReferenciasNoConjunto_SemRecusa() =>
        Conferir(
            Modalidade("AC", ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
            Modalidade("PCD", ComposicaoVagasModalidade.RetiradaDeOutraModalidade, origem: "AC"),
            ComDestino("IND", "AC"),
            Cruzada("QUI", par: "IND2"),
            Cruzada("IND2", par: "QUI", fallback: "AC")).Should().BeEmpty();

    [Fact(DisplayName = "A origem da retirada fora do conjunto é recusada")]
    public void OrigemForaDoConjunto_Recusa() =>
        DeveRecusarSo(
            Conferir(Modalidade("PCD", ComposicaoVagasModalidade.RetiradaDeOutraModalidade, origem: "AC")),
            DistribuicaoDeVagasErrorCodes.ComposicaoOrigemNaoSelecionada, "PCD referencia a origem AC");

    [Fact(DisplayName = "O destino do remanejamento fora do conjunto é recusado")]
    public void DestinoForaDoConjunto_Recusa() =>
        DeveRecusarSo(
            Conferir(ComDestino("IND", "AC")),
            DistribuicaoDeVagasErrorCodes.RemanejamentoDestinoNaoSelecionado, "IND referencia o destino de remanejamento AC");

    [Fact(DisplayName = "O par do remanejamento fora do conjunto é recusado")]
    public void ParForaDoConjunto_Recusa() =>
        DeveRecusarSo(
            Conferir(Cruzada("QUI", par: "IND")),
            DistribuicaoDeVagasErrorCodes.RemanejamentoParNaoSelecionado, "QUI referencia o par de remanejamento IND");

    [Fact(DisplayName = "O fallback do remanejamento fora do conjunto é recusado")]
    public void FallbackForaDoConjunto_Recusa() =>
        DeveRecusarSo(
            Conferir(Cruzada("QUI", par: "IND", fallback: "AC"), Cruzada("IND", par: "QUI")),
            DistribuicaoDeVagasErrorCodes.RemanejamentoFallbackNaoSelecionado, "QUI referencia o fallback de remanejamento AC");

    [Fact(DisplayName = "O par cruzado consigo mesmo é recusado")]
    public void ParConsigoMesmo_Recusa() =>
        DeveRecusarSo(
            Conferir(Cruzada("QUI", par: "QUI")),
            DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "forma par cruzado consigo mesma");

    [Fact(DisplayName = "O par cruzado cujo par não aponta de volta é recusado")]
    public void ParNaoReciproco_Recusa() =>
        DeveRecusarSo(
            Conferir(Cruzada("QUI", par: "IND"), Modalidade("IND")),
            DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "QUI forma par cruzado com IND, que não aponta de volta");

    [Fact(DisplayName = "O par cruzado que aponta para outra modalidade que não a de volta é recusado nas duas pontas")]
    public void ParApontaParaOutra_RecusaNasDuasPontas() =>
        Conferir(Cruzada("QUI", par: "IND"), Cruzada("IND", par: "AC"), Modalidade("AC"))
            .Select(static r => r.Error.Message).Should().Equal(
                "Modalidade QUI forma par cruzado com IND, que não aponta de volta para ela.",
                "Modalidade IND forma par cruzado com AC, que não aponta de volta para ela.");

    [Fact(DisplayName = "A reclassificação em ampla concorrência sem ampla concorrência ofertada é recusada")]
    public void ReclassificarSemAmpla_Recusa() =>
        DeveRecusarSo(
            Conferir(Modalidade("IND", acaoQuandoIndeferido: "RECLASSIFICAR_AC")),
            DistribuicaoDeVagasErrorCodes.ReclassificacaoParaAmplaSemAmplaOfertada, "IND reclassifica em AC");

    [Fact(DisplayName = "A reclassificação em ampla concorrência com ela ofertada passa sem recusa")]
    public void ReclassificarComAmpla_SemRecusa() =>
        Conferir(Modalidade("IND", acaoQuandoIndeferido: "RECLASSIFICAR_AC"), Modalidade("AC")).Should().BeEmpty();

    [Fact(DisplayName = "O par e o fallback fora do conjunto, na mesma modalidade, são recusados os dois, nessa ordem")]
    public void ParEFallbackForaDoConjunto_RecusaOsDois() =>
        Conferir(Cruzada("QUI", par: "IND", fallback: "AC")).Select(static r => r.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.RemanejamentoParNaoSelecionado,
            DistribuicaoDeVagasErrorCodes.RemanejamentoFallbackNaoSelecionado);

    [Fact(DisplayName = "As recusas se acumulam por modalidade, na ordem das modalidades e das referências")]
    public void Recusas_Acumulam_NaOrdemDasModalidades()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            Modalidade("PCD", ComposicaoVagasModalidade.RetiradaDeOutraModalidade, origem: "AC", acaoQuandoIndeferido: "RECLASSIFICAR_AC"),
            Cruzada("QUI", par: "QUI"));

        recusas.Select(static r => r.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.ComposicaoOrigemNaoSelecionada,
            DistribuicaoDeVagasErrorCodes.ReclassificacaoParaAmplaSemAmplaOfertada,
            DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco);
    }

    [Fact(DisplayName = "Com uma modalidade recusada, a citação a código fora das aceitas não é recusada")]
    public void ModalidadeRecusada_CitacaoForaDasAceitas_NaoRecusa() =>
        ConferirComUmaRecusada(ComDestino("IND", "AC"), Cruzada("QUI", par: "PCD", fallback: "AC")).Should().BeEmpty();

    [Fact(DisplayName = "Com uma modalidade recusada, a citação a código aceito continua conferida sem recusa")]
    public void ModalidadeRecusada_CitacaoAAceita_SemRecusa() =>
        ConferirComUmaRecusada(Modalidade("AC"), ComDestino("IND", "AC")).Should().BeEmpty();

    [Fact(DisplayName = "Com uma modalidade recusada, a reclassificação sem a ampla concorrência entre as aceitas não é recusada")]
    public void ModalidadeRecusada_ReclassificarSemAmpla_NaoRecusa() =>
        ConferirComUmaRecusada(Modalidade("IND", acaoQuandoIndeferido: "RECLASSIFICAR_AC")).Should().BeEmpty();

    [Fact(DisplayName = "Com uma modalidade recusada, o par cruzado consigo mesmo continua recusado")]
    public void ModalidadeRecusada_ParConsigoMesmo_Recusa() =>
        DeveRecusarSo(
            ConferirComUmaRecusada(Cruzada("QUI", par: "QUI")),
            DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "forma par cruzado consigo mesma");

    [Fact(DisplayName = "Com uma modalidade recusada, o par cruzado não recíproco entre duas aceitas continua recusado")]
    public void ModalidadeRecusada_ParNaoReciprocoEntreAceitas_Recusa() =>
        DeveRecusarSo(
            ConferirComUmaRecusada(Cruzada("QUI", par: "IND"), Modalidade("IND")),
            DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "QUI forma par cruzado com IND, que não aponta de volta");
}
