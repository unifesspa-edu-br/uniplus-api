namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// O motivo publicado não sai; o motivo novo entra com justificativa e antes de começar a avaliação do tipo
/// (UNI-REQ-0124, UNI-REQ-0285).
/// </summary>
public sealed class ConferenciaDosMotivosDaRetificacaoTests
{
    private const string Justificativa = "Ajuste pedido pela comissão.";

    private static MotivoDoDocumento Motivo(string codigo, TipoMotivoDeDecisao tipo = TipoMotivoDeDecisao.IsencaoDaTaxa, string? justificativa = null) =>
        new(Guid.CreateVersion7(), tipo, codigo, justificativa);

    private static MotivoDoDocumento ComoNovo(MotivoDoDocumento motivo, string? justificativa) => motivo with { Justificativa = justificativa };

    private static AndamentoDasFases Andamento(
        DateTimeOffset inicioDaIsencao, Dictionary<TipoMotivoDeDecisao, DateTimeOffset>? registros = null, params EtapaProcesso[] etapas)
    {
        FaseCronograma isencao = Fase(1, "ISENCAO", (inicioDaIsencao, inicioDaIsencao.AddDays(7)), coletaIsencao: true);

        return new AndamentoDasFases(
            [isencao], etapas, new FatosDaExecucao { Instante = Instante, FasesIniciadas = FasesIniciadas.Nenhuma, PrimeiroRegistroDeDecisaoPorTipo = registros ?? [] });
    }

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static r => (r.Field, r.Error.Code));

    [Fact(DisplayName = "O motivo publicado que sai do documento é recusado, no campo dele")]
    public void MotivoPublicado_Retirado_Recusa()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [], Andamento(Instante.AddDays(5)))).Should().Equal(
            ("motivosDeDecisao[ISENCAO_NAO_COMPROVOU]", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao));
    }

    [Fact(DisplayName = "O motivo publicado mantido, com ou sem avaliação iniciada, não é recusado")]
    public void MotivoPublicado_Mantido_NaoRecusa()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [publicado], Andamento(Instante.AddDays(-3))).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo com justificativa, antes de começar a avaliação do tipo, entra")]
    public void MotivoNovo_AntesDaAvaliacao_Entra()
    {
        ConferenciaDosMotivosDaRetificacao.Conferir([], [Motivo("NOVO", justificativa: Justificativa)], Andamento(Instante.AddDays(5))).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo sem justificativa é recusado, e a justificativa precede o início da avaliação")]
    public void MotivoNovo_SemJustificativa_Recusa()
    {
        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [Motivo("NOVO"), Motivo("OUTRO")], Andamento(Instante.AddDays(-3)))).Should().Equal(
            ("motivosDeDecisao[NOVO]", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria),
            ("motivosDeDecisao[OUTRO]", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria));
    }

    [Fact(DisplayName = "O motivo novo, depois de a janela da fase que avalia o tipo abrir, é recusado; no instante exato também")]
    public void MotivoNovo_AposInicioDaJanela_Recusa()
    {
        MotivoDoDocumento novo = Motivo("NOVO", justificativa: Justificativa);

        foreach (DateTimeOffset inicio in new[] { Instante.AddDays(-3), Instante })
        {
            Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [novo], Andamento(inicio))).Should().Equal(
                ("motivosDeDecisao[NOVO]", MotivoDeDecisaoErrorCodes.AvaliacaoIniciada));
        }
    }

    [Fact(DisplayName = "O primeiro registro de decisão do tipo marca o início da avaliação, ainda que a janela seja futura; o de outro tipo não")]
    public void MotivoNovo_ComRegistroDoTipo_Recusa()
    {
        MotivoDoDocumento novo = Motivo("NOVO", justificativa: Justificativa);
        Dictionary<TipoMotivoDeDecisao, DateTimeOffset> doTipo = new() { [TipoMotivoDeDecisao.IsencaoDaTaxa] = Instante.AddDays(-1) };
        Dictionary<TipoMotivoDeDecisao, DateTimeOffset> deOutroTipo = new() { [TipoMotivoDeDecisao.Classificacao] = Instante.AddDays(-1) };

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [novo], Andamento(Instante.AddDays(5), doTipo))).Should().Equal(
            ("motivosDeDecisao[NOVO]", MotivoDeDecisaoErrorCodes.AvaliacaoIniciada));
        ConferenciaDosMotivosDaRetificacao.Conferir([], [novo], Andamento(Instante.AddDays(5), deOutroTipo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As retiradas vêm antes das inclusões, cada grupo em ordem de código, e acumulam")]
    public void Recusas_AcumulamEmOrdem()
    {
        MotivoDoDocumento b = Motivo("B_PUBLICADO");
        MotivoDoDocumento a = Motivo("A_PUBLICADO");

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([b, a], [Motivo("Z_NOVO"), ComoNovo(Motivo("M_NOVO"), null)], Andamento(Instante.AddDays(5)))).Should().Equal(
            ("motivosDeDecisao[A_PUBLICADO]", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao),
            ("motivosDeDecisao[B_PUBLICADO]", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao),
            ("motivosDeDecisao[M_NOVO]", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria),
            ("motivosDeDecisao[Z_NOVO]", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria));
    }

    [Fact(DisplayName = "A etapa da fase que avalia o tipo, iniciada antes da janela da fase, marca o início da avaliação, no instante exato inclusive")]
    public void MotivoNovo_ComEtapaIniciada_Recusa()
    {
        MotivoDoDocumento novo = Motivo("NOVO", justificativa: Justificativa);

        foreach (DateTimeOffset inicioDaEtapa in new[] { Instante.AddDays(-1), Instante })
        {
            Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [novo], Andamento(Instante.AddDays(5), null, EtapaComJanela("Análise", inicioDaEtapa, "ISENCAO")))).Should().Equal(
                ("motivosDeDecisao[NOVO]", MotivoDeDecisaoErrorCodes.AvaliacaoIniciada));
        }

        ConferenciaDosMotivosDaRetificacao.Conferir([], [novo], Andamento(Instante.AddDays(5), null, EtapaComJanela("Análise", Instante.AddDays(-1), "OUTRA_FASE"))).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo se identifica pela origem: a mesma origem com outro código e outra justificativa é o mesmo motivo")]
    public void Identidade_PelaOrigem_MesmaOrigemOutrosDados()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [publicado with { Codigo = "RENOMEADO", Justificativa = Justificativa }], Andamento(Instante.AddDays(-3))).Should().BeEmpty();
    }

    [Fact(DisplayName = "Origens distintas com o mesmo código são motivos distintos: um sai e outro entra")]
    public void Identidade_PelaOrigem_MesmoCodigoOrigensDistintas()
    {
        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([Motivo("MESMO")], [Motivo("MESMO", justificativa: Justificativa)], Andamento(Instante.AddDays(5)))).Should().Equal(
            ("motivosDeDecisao[MESMO]", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao));
    }
}
