namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// O gate de publicação devolve a primeira pendência do cronograma, na ordem em que as confere. Cada
/// pendência é provada sozinha (código e mensagem exatos) e contra a seguinte no mesmo estado: quem
/// corrige o cronograma de cima para baixo vê sempre o primeiro problema.
/// </summary>
public sealed partial class PendenciasDoCronogramaPrecedenciaTests
{
    /// <summary>
    /// Pares de pendências vizinhas na ordem da conferência. Onde duas vizinhas não coexistem no mesmo
    /// estado, a primeira é pareada com a próxima que coexiste: a fase de avaliação sem etapa exige que
    /// nenhuma etapa fique sem fase declarada, e a origem por inscrição própria sem fase de coleta
    /// exige que nenhuma fase colete inscrição, e por isso nenhuma está sem janela. A conclusão
    /// declarada numa fase sem preliminar, e não a conclusão não declarada, é a que coexiste com vagas
    /// sem fase que produz resultado, porque a segunda exige que nenhuma fase publique resultado.
    /// </summary>
    private static readonly (Pendencia Primeira, Pendencia Segunda)[] ParesVizinhos =
    [
        (Pendencia.AvaliacaoSemEtapa, Pendencia.InscricaoPropriaSemFaseDeColeta),
        (Pendencia.EtapaSemFaseDeclarada, Pendencia.InscricaoPropriaSemFaseDeColeta),
        (Pendencia.InscricaoPropriaSemFaseDeColeta, Pendencia.FaseSemUnidadeResponsavel),
        (Pendencia.FaseDeColetaSemJanela, Pendencia.FaseSemUnidadeResponsavel),
        (Pendencia.FaseSemUnidadeResponsavel, Pendencia.IsencaoSemPrazo),
        (Pendencia.IsencaoSemPrazo, Pendencia.VagasSemFaseQueProduzResultado),
        (Pendencia.VagasSemFaseQueProduzResultado, Pendencia.ConclusaoDeclaradaSemPreliminar),
        (Pendencia.ConclusaoDeclaradaSemPreliminar, Pendencia.AncoraDaFaseSemPreliminar),
        (Pendencia.AncoraDaFaseSemPreliminar, Pendencia.AncoraDaEtapaForaDosPreliminares),
        (Pendencia.AncoraDaEtapaForaDosPreliminares, Pendencia.BancasSemRecorteDeCompetencia),
        (Pendencia.BancasSemRecorteDeCompetencia, Pendencia.EtapaComecaAntesDaFase),
    ];

    public static TheoryData<Pendencia> TodasAsPendencias() => [.. Enum.GetValues<Pendencia>()];

    public static TheoryData<Pendencia, Pendencia> Pares()
    {
        TheoryData<Pendencia, Pendencia> pares = [];
        foreach ((Pendencia primeira, Pendencia segunda) in ParesVizinhos)
        {
            pares.Add(primeira, segunda);
        }

        return pares;
    }

    [Fact(DisplayName = "O cronograma conforme não tem pendência, de modo que cada recusa abaixo vem da pendência aplicada")]
    public void CronogramaConforme_SemPendencia()
    {
        DomainError? pendencia = PendenciaDoCronograma(CronogramaEmMontagem.Com());

        pendencia.Should().BeNull();
    }

    [Theory(DisplayName = "Cada pendência, sozinha, é recusada com o código e a mensagem dela")]
    [MemberData(nameof(TodasAsPendencias))]
    public void UmaPendencia_RecusaComCodigoEMensagem(Pendencia pendencia)
    {
        DomainError? recusa = PendenciaDoCronograma(CronogramaEmMontagem.Com(pendencia));

        recusa!.Code.Should().Be(Recusas[pendencia].Codigo);
        recusa.Message.Should().Be(Recusas[pendencia].Mensagem);
    }

    [Theory(DisplayName = "O estado que tem duas pendências vizinhas é recusado pela primeira, na ordem da conferência")]
    [MemberData(nameof(Pares))]
    public void DuasPendenciasVizinhas_RecusaAPrimeira(Pendencia primeira, Pendencia segunda)
    {
        DomainError? recusa = PendenciaDoCronograma(CronogramaEmMontagem.Com(primeira, segunda));

        recusa!.Code.Should().Be(Recusas[primeira].Codigo);
        recusa.Message.Should().Be(Recusas[primeira].Mensagem);
    }

    [Fact(DisplayName = "A conferência sem o estado da configuração é erro de programação")]
    public void SemEstado_Lanca()
    {
        Action semEstado = () => PendenciasDoCronograma.PendenciaDoCronograma(null!, Belem);

        semEstado.Should().Throw<ArgumentNullException>();
    }

    private static DomainError? PendenciaDoCronograma(CronogramaEmMontagem cronograma) =>
        PendenciasDoCronograma.PendenciaDoCronograma(cronograma.Montar(), Belem);
}
