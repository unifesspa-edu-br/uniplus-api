namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Entre 21h e 23h59 em Belém o dia em UTC já é o seguinte: a versão do tipo de ato que a edição das etapas e a do
/// cronograma de fases consultam vale para o dia civil do fuso institucional, não para o dia em UTC.
/// </summary>
public sealed class DiaCivilDaVigenciaDosTiposDeAtoNaEdicaoTests
{
    private static readonly DateOnly DiaEmBelem = new(2026, 6, 15);
    private const string FusoNaoReconhecido = "ProcessoSeletivo.FusoInstitucionalNaoReconhecido";

    [Theory(DisplayName = "No fim do dia em Belém, o tipo de ato que só vale a partir do dia seguinte não é aceito na definição das etapas")]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public async Task Etapas_TipoQueSoValeNoDiaSeguinte_NaoEhAceitoEConsultaODiaCivilLocal(int hora, int minuto)
    {
        ITipoAtoPublicadoReader catalogo = CatalogoComVersaoValidaA(new DateOnly(2026, 6, 16));

        Result resultado = await DefinirEtapasAsync(catalogo, new LeituraDoInstanteDoComando(RelogioEmBelem(hora, minuto), new ResolvedorFusoDeTeste()));

        resultado.Error!.Code.Should().Be("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
        await catalogo.Received(1).ObterVigenteAsync(AtoDaClassificacao, DiaEmBelem, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "No fim do dia em Belém, o tipo de ato vigente no dia local é aceito na definição das etapas")]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public async Task Etapas_TipoVigenteNoDiaLocal_EhAceito(int hora, int minuto)
    {
        ITipoAtoPublicadoReader catalogo = CatalogoComVersaoValidaA(DiaEmBelem);

        Result resultado = await DefinirEtapasAsync(catalogo, new LeituraDoInstanteDoComando(RelogioEmBelem(hora, minuto), new ResolvedorFusoDeTeste()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fuso não reconhecido recusa a definição das etapas sem consultar o tipo de ato nem gravar")]
    public async Task Etapas_FusoNaoReconhecido_RecusaSemConsultarNemGravar()
    {
        ITipoAtoPublicadoReader catalogo = CatalogoComVersaoValidaA(DiaEmBelem);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await DefinirEtapasAsync(
            catalogo, new LeituraDoInstanteDoComando(RelogioEmBelem(21, 0), new ResolvedorFusoIndisponivelDeTeste()), unitOfWork);

        resultado.Error!.Code.Should().Be(FusoNaoReconhecido);
        catalogo.ReceivedCalls().Should().BeEmpty();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "No fim do dia em Belém, o tipo de ato que só vale a partir do dia seguinte não é aceito na definição do cronograma")]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public async Task Cronograma_TipoQueSoValeNoDiaSeguinte_NaoEhAceitoEConsultaODiaCivilLocal(int hora, int minuto)
    {
        CenarioDoCronogramaDeFases cenario = CenarioNoFimDoDiaEmBelem(hora, minuto, diaDaVersao: new DateOnly(2026, 6, 16));

        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProdutoDaFase.AtoNaoEncontradoNoCatalogo");
        await cenario.TipoAtoPublicadoReader.Received(1).ObterVigenteAsync(AtoDaClassificacao, DiaEmBelem, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "No fim do dia em Belém, o tipo de ato vigente no dia local é aceito na definição do cronograma")]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public async Task Cronograma_TipoVigenteNoDiaLocal_EhAceito(int hora, int minuto)
    {
        CenarioDoCronogramaDeFases cenario = CenarioNoFimDoDiaEmBelem(hora, minuto, diaDaVersao: DiaEmBelem);

        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fuso não reconhecido recusa a definição do cronograma sem consultar o tipo de ato nem gravar")]
    public async Task Cronograma_FusoNaoReconhecido_RecusaSemConsultarNemGravar()
    {
        CenarioDoCronogramaDeFases cenario = CenarioNoFimDoDiaEmBelem(21, 0, diaDaVersao: DiaEmBelem);
        cenario.Fuso = new ResolvedorFusoIndisponivelDeTeste();

        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()));

        resultado.Error!.Code.Should().Be(FusoNaoReconhecido);
        cenario.TipoAtoPublicadoReader.ReceivedCalls().Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static RelogioFixo RelogioEmBelem(int hora, int minuto) => new(InstanteEmBelem.Em(2026, 6, 15, hora, minuto));

    private static ITipoAtoPublicadoReader CatalogoComVersaoValidaA(DateOnly dia)
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(AtoDaClassificacao, dia, Arg.Any<CancellationToken>()).Returns(TipoAtoDaClassificacao());
        return catalogo;
    }

    private static CenarioDoCronogramaDeFases CenarioNoFimDoDiaEmBelem(int hora, int minuto, DateOnly diaDaVersao)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        cenario.Relogio = RelogioEmBelem(hora, minuto);
        cenario.TipoAtoPublicadoReader.ObterVigenteAsync(AtoDaClassificacao, diaDaVersao, Arg.Any<CancellationToken>())
            .Returns(TipoAtoDaClassificacao());
        return cenario;
    }

    private static Task<Result> DefinirEtapasAsync(ITipoAtoPublicadoReader catalogo, LeituraDoInstanteDoComando instantes, IUnitOfWork? unitOfWork = null)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput etapa = EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1)
            with
        { Produtos = [new ProdutoDaEtapaInput(AtoDaClassificacao, "DEFINITIVO")] };

        return Executar(
            new DefinirEtapasCommand(processo.Id, [etapa]), RepositorioCom(processo), unitOfWork ?? Substitute.For<IUnitOfWork>(),
            cadastros: new(Atos: catalogo, Instantes: instantes));
    }
}
