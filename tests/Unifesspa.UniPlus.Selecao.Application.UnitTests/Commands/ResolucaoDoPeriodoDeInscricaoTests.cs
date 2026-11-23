namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O período de inscrição do edital quando a fase que coleta inscrição está sem janela: não há período a resolver, e a
/// recusa da fase é da conferência da configuração.
/// </summary>
public sealed class ResolucaoDoPeriodoDeInscricaoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Fim = new(2026, 2, 28, 23, 59, 59, TimeSpan.FromHours(-3));

    [Fact(DisplayName = "A fase que coleta inscrição sem janela não resolve o período, e não lança")]
    public void FaseSemJanela_NaoResolveOPeriodo()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeInscricaoSemJanela();

        Result<DadosEdital>? resolvido = ResolucaoDoPeriodoDeInscricao.Resolver(processo, "001/2026", null, null, Guid.CreateVersion7(), []);

        resolvido.Should().BeNull();
    }

    [Fact(DisplayName = "O período informado onde a fase o define é recusado, mesmo com a fase sem janela")]
    public void FaseSemJanela_PeriodoInformadoContinuaRecusado()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeInscricaoSemJanela();

        Result<DadosEdital>? resolvido = ResolucaoDoPeriodoDeInscricao.Resolver(processo, "001/2026", Inicio, Fim, Guid.CreateVersion7(), []);

        resolvido!.Error!.Code.Should().Be("ProcessoSeletivo.PeriodoInscricaoNaoInformavel");
    }

    private static ProcessoSeletivo ProcessoComFaseDeInscricaoSemJanela()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Inscrição sem janela", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([FaseDeInscricaoSemJanela()], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FaseCronograma FaseDeInscricaoSemJanela() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;
}
