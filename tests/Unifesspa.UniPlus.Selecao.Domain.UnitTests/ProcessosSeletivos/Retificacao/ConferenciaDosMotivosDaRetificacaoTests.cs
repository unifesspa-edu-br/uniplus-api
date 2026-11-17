namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// O motivo publicado não sai; o motivo novo entra com justificativa e antes de começar a avaliação no lugar dele, a
/// fase ou a etapa (UNI-REQ-0124, UNI-REQ-0285).
/// </summary>
public sealed class ConferenciaDosMotivosDaRetificacaoTests
{
    private const string Justificativa = "Ajuste pedido pela comissão.";

    private static readonly Guid FaseDeIsencao = Guid.CreateVersion7();
    private static readonly Guid OutraFase = Guid.CreateVersion7();
    private static readonly Guid EtapaDeAnalise = Guid.CreateVersion7();
    private static readonly Guid EtapaIrma = Guid.CreateVersion7();

    private static MotivoDoDocumento Motivo(string codigo, LugarDaDecisao? lugar = null, string? justificativa = null) =>
        new(Guid.CreateVersion7(), lugar ?? LugarDaDecisao.DaFase(FaseDeIsencao), TipoMotivoDeDecisao.IsencaoDaTaxa, codigo, justificativa);

    private static MotivoDoDocumento ComoNovo(MotivoDoDocumento motivo, string? justificativa) => motivo with { Justificativa = justificativa };

    private static AndamentoDasFases Andamento(FasesIniciadas? iniciadas = null) =>
        new([], [], new FatosDaExecucao { Instante = Instante, FasesIniciadas = iniciadas ?? FasesIniciadas.Nenhuma });

    private static FasesIniciadas ComAAvaliacaoIniciadaNaFase(Guid faseCanonicaOrigemId) =>
        new([KeyValuePair.Create(faseCanonicaOrigemId, (Guid?)null)], fasesProdutoras: [faseCanonicaOrigemId]);

    private static FasesIniciadas ComAEtapaIniciada(Guid etapaId) =>
        new([], etapas: [new EtapaIniciada(etapaId, "Análise", NaVersaoVigente: true)]);

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static r => (r.Field, r.Error.Code));

    [Fact(DisplayName = "O motivo publicado que sai do documento é recusado, no campo dele")]
    public void MotivoPublicado_Retirado_Recusa()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [], Andamento())).Should().Equal(
            ("motivos", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao));
    }

    [Fact(DisplayName = "O motivo publicado mantido, com ou sem avaliação iniciada, não é recusado")]
    public void MotivoPublicado_Mantido_NaoRecusa()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [publicado], Andamento(ComAAvaliacaoIniciadaNaFase(FaseDeIsencao))).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo com justificativa, antes de começar a avaliação no lugar, entra")]
    public void MotivoNovo_AntesDaAvaliacao_Entra()
    {
        ConferenciaDosMotivosDaRetificacao.Conferir([], [Motivo("NOVO", justificativa: Justificativa)], Andamento()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo sem justificativa é recusado, e a justificativa precede o início da avaliação")]
    public void MotivoNovo_SemJustificativa_Recusa()
    {
        FasesIniciadas iniciadas = ComAAvaliacaoIniciadaNaFase(FaseDeIsencao);

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [Motivo("NOVO"), Motivo("OUTRO")], Andamento(iniciadas))).Should().Equal(
            ("motivos[0].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria),
            ("motivos[1].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria));
    }

    [Fact(DisplayName = "A justificativa com caractere nulo ou acima do tamanho é recusada pelo domínio")]
    public void MotivoNovo_JustificativaInvalida_Recusa()
    {
        AndamentoDasFases andamento = Andamento();

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [Motivo("NULO", justificativa: "Norma\0nova")], andamento)).Should().Equal(
            ("motivos[0].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaCaractereInvalido));
        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir(
            [], [Motivo("LONGO", justificativa: new string('j', ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength + 1))], andamento)).Should().Equal(
            ("motivos[0].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaTamanho));
        ConferenciaDosMotivosDaRetificacao.Conferir(
            [], [Motivo("NO_LIMITE", justificativa: new string('j', ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength))], andamento).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo na fase cuja avaliação começou é recusado; na fase em que ela não começou, entra")]
    public void MotivoNovo_NaFaseIniciada_Recusa()
    {
        MotivoDoDocumento naFaseIniciada = Motivo("NOVO", justificativa: Justificativa);
        MotivoDoDocumento emOutraFase = Motivo("NOVO", LugarDaDecisao.DaFase(OutraFase), Justificativa);
        AndamentoDasFases andamento = Andamento(ComAAvaliacaoIniciadaNaFase(FaseDeIsencao));

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [naFaseIniciada], andamento)).Should().Equal(
            ("motivos[0]", MotivoDeDecisaoErrorCodes.AvaliacaoIniciada));
        ConferenciaDosMotivosDaRetificacao.Conferir([], [emOutraFase], andamento).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo novo na etapa que já começou é recusado; na etapa irmã que ainda não começou, entra")]
    public void MotivoNovo_NaEtapaIniciada_Recusa()
    {
        MotivoDoDocumento naEtapaIniciada = Motivo("NOVO", LugarDaDecisao.DaEtapa(EtapaDeAnalise), Justificativa);
        MotivoDoDocumento naEtapaIrma = Motivo("NOVO", LugarDaDecisao.DaEtapa(EtapaIrma), Justificativa);
        AndamentoDasFases andamento = Andamento(ComAEtapaIniciada(EtapaDeAnalise));

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([], [naEtapaIniciada], andamento)).Should().Equal(
            ("motivos[0]", MotivoDeDecisaoErrorCodes.AvaliacaoIniciada));
        ConferenciaDosMotivosDaRetificacao.Conferir([], [naEtapaIrma], andamento).Should().BeEmpty();
    }

    [Fact(DisplayName = "As retiradas vêm antes das inclusões, cada grupo em ordem de código, e acumulam")]
    public void Recusas_AcumulamEmOrdem()
    {
        MotivoDoDocumento b = Motivo("B_PUBLICADO");
        MotivoDoDocumento a = Motivo("A_PUBLICADO");

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([b, a], [Motivo("Z_NOVO"), ComoNovo(Motivo("M_NOVO"), null)], Andamento())).Should().Equal(
            ("motivos", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao),
            ("motivos", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao),
            ("motivos[1].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria),
            ("motivos[0].justificativaDaInclusao", MotivoDeDecisaoErrorCodes.JustificativaObrigatoria));
    }

    [Fact(DisplayName = "O motivo se identifica pelo lugar e pela origem: a mesma origem no mesmo lugar com outro código e outra justificativa é o mesmo motivo")]
    public void Identidade_MesmaOrigemNoMesmoLugar_OutrosDados()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");

        ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [publicado with { Codigo = "RENOMEADO", Justificativa = Justificativa }], Andamento()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mesma origem em outro lugar é outro motivo: sai do lugar da base e entra no novo")]
    public void Identidade_MesmaOrigemEmOutroLugar_SaiEEntra()
    {
        MotivoDoDocumento publicado = Motivo("ISENCAO_NAO_COMPROVOU");
        MotivoDoDocumento movido = publicado with { Lugar = LugarDaDecisao.DaFase(OutraFase), Justificativa = Justificativa };

        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([publicado], [movido], Andamento())).Should().Equal(
            ("motivos", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao));
    }

    [Fact(DisplayName = "Origens distintas com o mesmo código são motivos distintos: um sai e outro entra")]
    public void Identidade_MesmoCodigoOrigensDistintas()
    {
        Recusas(ConferenciaDosMotivosDaRetificacao.Conferir([Motivo("MESMO")], [Motivo("MESMO", justificativa: Justificativa)], Andamento())).Should().Equal(
            ("motivos", MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao));
    }
}
