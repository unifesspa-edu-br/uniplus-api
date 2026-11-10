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

    [Fact(DisplayName = "Perfil desativado é reativado por outra pessoa e a alteração é gravada")]
    public async Task Handle_PerfilDesativado_ReativaEGrava()
    {
        Perfil perfil = PerfilComum();
        PerfisDeTeste.Desativar(perfil, new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, []);

        Result resultado = await Reativar(cenario, perfil.Id);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Reativar o perfil por quem o desativou é recusado e nada é gravado")]
    public async Task Handle_PorQuemDesativou_Recusa()
    {
        Perfil perfil = PerfilComum();
        PerfisDeTeste.Desativar(perfil, new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, []);

        Result resultado = await Reativar(cenario, perfil.Id);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.ReativacaoPorQuemDesativou);
        perfil.Ativo.Should().BeFalse();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Perfil inexistente devolve NaoEncontrado e nada é gravado")]
    public async Task Handle_PerfilInexistente_NaoEncontrado()
    {
        CenarioDaGovernancaDaVersao cenario = new(PerfilComum(), PerfisDeTeste.ProtecaoDeDados, []);

        Result resultado = await Reativar(cenario, Guid.NewGuid());

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Perfil de governança recusa a reativação, descarta o rastreamento e não grava")]
    public async Task Handle_PerfilDeGovernanca_RecusaEDescarta()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", "Governa os acessos.", [Consultar], new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, []);

        Result resultado = await Reativar(cenario, perfil.Id);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        cenario.Unidade.Descartes.Should().Be(1);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Conflito de concorrência na gravação descarta o rastreamento e devolve o conflito")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Perfil perfil = PerfilComum();
        PerfisDeTeste.Desativar(perfil, new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.ProtecaoDeDados, []);
        cenario.Unidade.Falha = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException();

        Result resultado = await Reativar(cenario, perfil.Id);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.ConflitoDeConcorrencia);
        cenario.Unidade.Descartes.Should().Be(1);
    }

    [Fact(DisplayName = "Desativar o perfil não cancela a versão pendente e grava o motivo")]
    public async Task Desativar_PerfilComPendente_MantemAPendente()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, []);

        Result resultado = await DesativarPerfilCommandHandler.Handle(
            new DesativarPerfilCommand(perfil.Id, PerfisDeTeste.MotivoDaContencao), cenario.Ativacao, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeFalse();
        perfil.VersaoPendente.Should().NotBeNull();
        perfil.DesativacaoAberta!.Motivo.Should().Be(PerfisDeTeste.MotivoDaContencao);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Desativar o perfil sem motivo é recusado")]
    public async Task Desativar_SemMotivo_Recusa()
    {
        Perfil perfil = PerfilComum();
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, []);

        Result resultado = await DesativarPerfilCommandHandler.Handle(
            new DesativarPerfilCommand(perfil.Id, null), cenario.Ativacao, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        perfil.Ativo.Should().BeTrue();
    }

    private static Task<Result> Reativar(CenarioDaGovernancaDaVersao cenario, Guid perfilId) =>
        ReativarPerfilCommandHandler.Handle(
            new ReativarPerfilCommand(perfilId, PerfisDeTeste.MotivoDaContencao), cenario.Ativacao, CancellationToken.None);

    private static Perfil PerfilComum() =>
        PerfisDeTeste.Vigente("Analista", "Descrição válida.", [Consultar], new CatalogoDeTeste());
}
