namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A confirmação da instalação com o código certo: concede os dois gestores na ordem única de travas, ou recusa quando
/// outra instalação já venceu a corrida, sem prender a chave de idempotência quando nada se grava.
/// </summary>
public sealed class ConfirmacaoDaInstalacaoTests
{
    [Theory(DisplayName = "Código certo com gestor já instalado cancela a instalação, grava a tentativa como já havia gestor e recusa")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmar_OutraInstalacaoVenceuACorrida_RecusaSemGravarComoAceita(bool conflitoNaGravacao)
    {
        Instalacao instalacao = AguardandoCodigo();
        RepositorioDaCorrida instalacoes = new(instalacao);
        UnidadeDeTrabalho unidade = conflitoNaGravacao ? UnidadeDeTrabalho.ComConflito() : new();

        Result resultado = await ConfirmarAsync(instalacoes, new ConcessoesDaInstalacao(gestoresAtivos: 1, travas: []), unidade, new PapeisDeTeste());

        resultado.Errors.Single().Error.Code.Should().Be(conflitoNaGravacao ? InstalacaoErrorCodes.JaHaGestorAoIniciar : InstalacaoErrorCodes.JaHaGestor);
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacoes.Tentativa!.Resultado.Should().Be(ResultadoDaTentativaDeInstalacao.JaHaviaGestor);
        unidade.Descartou.Should().Be(conflitoNaGravacao);
    }

    [Fact(DisplayName = "Concessão duplicada na gravação descarta a confirmação e recusa sem a marca de recusa gravada")]
    public async Task Confirmar_ConcessaoDuplicadaNaGravacao_RecusaComCodigoQueNaoPrendeAChave()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConcessaoDuplicada();

        Result resultado = await ConfirmarAsync(
            new RepositorioDaCorrida(AguardandoCodigo()), new ConcessoesDaInstalacao(gestoresAtivos: 0, travas: []), unidade, new PapeisDeTeste());

        resultado.Errors.Single().Error.Code.Should().Be(InstalacaoErrorCodes.JaHaGestorAoIniciar);
        unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "A confirmação trava o perfil de gestor, depois a pessoa de cada gestor e só então a trava dos gestores")]
    public async Task Confirmar_TravaNaOrdemUnica()
    {
        List<string> travas = [];
        ConcessoesDaInstalacao concessoes = new(gestoresAtivos: 0, travas);

        Result resultado = await ConfirmarAsync(new RepositorioDaCorrida(AguardandoCodigo()), concessoes, new UnidadeDeTrabalho(), new PapeisDeTeste(travas));

        resultado.IsSuccess.Should().BeTrue();
        travas.Should().Equal("perfil", "pessoa:" + Gestores()[0].Sub, "pessoa:" + Gestores()[1].Sub, "gestores");
        concessoes.Adicionadas.Should().HaveCount(2).And.OnlyContain(c => c.PerfilId == concessoes.Adicionadas[0].PerfilId);
    }

    [Fact(DisplayName = "Sem instalação aguardando código, a tentativa é gravada como sem pendência e recusada com o código inválido")]
    public async Task Confirmar_SemPendencia_GravaATentativaERecusa()
    {
        RepositorioDaCorrida instalacoes = new(null);

        Result resultado = await ConfirmarAsync(instalacoes, new ConcessoesDaInstalacao(gestoresAtivos: 0, travas: []), new UnidadeDeTrabalho(), new PapeisDeTeste());

        resultado.Errors.Single().Error.Code.Should().Be(InstalacaoErrorCodes.CodigoInvalido);
        instalacoes.Tentativa!.Resultado.Should().Be(ResultadoDaTentativaDeInstalacao.SemPendencia);
    }

    private static Task<Result> ConfirmarAsync(
        RepositorioDaCorrida instalacoes, ConcessoesDaInstalacao concessoes, UnidadeDeTrabalho unidade, PapeisDeTeste papeis) =>
        ConfirmarInstalacaoCommandHandler.Handle(
            new ConfirmarInstalacaoCommand("12345678"),
            new TentativaDeConfirmacao(instalacoes, new ResumoFixo(), unidade, NullLogger<TentativaDeConfirmacao>.Instance),
            new ConcessaoDosGestores(instalacoes, concessoes, new CatalogoInstitucional(), papeis, unidade, Substitute.For<ITermoDeResponsabilidadeRepository>()),
            new Usuario(Executor),
            TimeProvider.System,
            CancellationToken.None);
}
