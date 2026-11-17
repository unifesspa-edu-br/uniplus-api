namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class PerfilParaPedirTests
{
    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();
    private readonly IJanelaDeMigracaoRepository _janelas = Substitute.For<IJanelaDeMigracaoRepository>();
    private readonly List<string> _ordem = [];
    private readonly Perfil _perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", ["geral:pedidos:consultar"], new CatalogoDeTeste());

    public PerfilParaPedirTests()
    {
        _concessoes.ObterParaConcederAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(_ => Registrar<Perfil?>("perfil", _perfil));
        _janelas.TemJanelaAbertaAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("aberta", false));
        _janelas.TemJanelaPorMaterializarAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("porMaterializar", false));
    }

    [Fact(DisplayName = "O perfil sem janela é devolvido, lido com a trava antes do relógio e das janelas")]
    public async Task Obter_SemJanela_DevolveOPerfilNaOrdem()
    {
        Result<Perfil> resultado = await Obter();

        resultado.Value.Should().BeSameAs(_perfil);
        _ordem.Should().Equal("perfil", "relogio", "aberta", "porMaterializar");
    }

    [Fact(DisplayName = "O perfil inexistente é recusado sem consultar janela nem relógio")]
    public async Task Obter_PerfilInexistente_RecusaSemConsultarAJanela()
    {
        _concessoes.ObterParaConcederAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(_ => Registrar<Perfil?>("perfil", null));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        _ordem.Should().Equal("perfil");
    }

    [Fact(DisplayName = "O perfil com janela aberta recusa o pedido sem consultar a janela por materializar")]
    public async Task Obter_JanelaAberta_RecusaOPedidoEmMigracao()
    {
        _janelas.TemJanelaAbertaAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("aberta", true));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.PerfilEmJanelaDeMigracao);
        _ordem.Should().Equal("perfil", "relogio", "aberta");
    }

    [Fact(DisplayName = "O perfil com janela vencida por materializar responde conflito de encerramento")]
    public async Task Obter_JanelaPorMaterializar_RecusaPorEncerramento()
    {
        _janelas.TemJanelaPorMaterializarAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("porMaterializar", true));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(MigracaoErrorCodes.JanelaEmEncerramento);
        _ordem.Should().Equal("perfil", "relogio", "aberta", "porMaterializar");
    }

    private Task<Result<Perfil>> Obter() =>
        new PerfilParaPedir(_concessoes, _janelas, new RelogioQueRegistra(_ordem)).ObterAsync(_perfil.Id, CancellationToken.None);

    private Task<T> Registrar<T>(string passo, T valor)
    {
        _ordem.Add(passo);

        return Task.FromResult(valor);
    }

    private sealed class RelogioQueRegistra(List<string> ordem) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            ordem.Add("relogio");

            return PerfisDeTeste.Instante;
        }
    }
}
