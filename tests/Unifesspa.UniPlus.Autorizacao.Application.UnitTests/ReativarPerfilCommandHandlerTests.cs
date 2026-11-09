namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class ReativarPerfilCommandHandlerTests
{
    private const string Consultar = "selecao:processos:consultar";

    [Fact(DisplayName = "Perfil desativado é reativado e a alteração é gravada")]
    public async Task Handle_PerfilDesativado_ReativaEGrava()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(perfil, unidade);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Perfil inexistente devolve NaoEncontrado e nada é gravado")]
    public async Task Handle_PerfilInexistente_NaoEncontrado()
    {
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(null, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Perfil de governança recusa a reativação, descarta o rastreamento e não grava")]
    public async Task Handle_PerfilDeGovernanca_RecusaEDescarta()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", "Governa os acessos.", [Consultar], new CatalogoDeTeste());
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(perfil, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        unidade.Descartes.Should().Be(1);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Conflito de concorrência na gravação descarta o rastreamento e devolve o conflito")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();
        UnidadeDeTrabalho unidade = new() { Falha = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException() };

        Result resultado = await Reativar(perfil, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartes.Should().Be(1);
    }

    [Fact(DisplayName = "Desativar o perfil não cancela a versão pendente")]
    public async Task Desativar_PerfilComPendente_MantemAPendente()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        UnidadeDeTrabalho unidade = new();

        Result resultado = await DesativarPerfilCommandHandler.Handle(
            new DesativarPerfilCommand(perfil.Id),
            new OperacaoSobreOPerfil(new PerfisNaMemoria(perfil), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1))),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeFalse();
        perfil.VersaoPendente.Should().NotBeNull();
        unidade.Gravacoes.Should().Be(1);
    }

    private static Task<Result> Reativar(Perfil? perfil, UnidadeDeTrabalho unidade) =>
        ReativarPerfilCommandHandler.Handle(
            new ReativarPerfilCommand(perfil?.Id ?? Guid.NewGuid()),
            new OperacaoSobreOPerfil(new PerfisNaMemoria(perfil is null ? [] : [perfil]), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1))),
            CancellationToken.None);

    private static Perfil PerfilComum() =>
        PerfisDeTeste.Vigente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
}
