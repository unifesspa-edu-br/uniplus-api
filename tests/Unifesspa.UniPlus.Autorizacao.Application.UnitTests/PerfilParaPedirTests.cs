namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class PerfilParaPedirTests
{
    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();
    private readonly IRevisaoDasConcessoesRepository _revisoes = Substitute.For<IRevisaoDasConcessoesRepository>();
    private readonly List<string> _ordem = [];
    private readonly Perfil _perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", ["geral:pedidos:consultar"], new CatalogoDeTeste());

    public PerfilParaPedirTests()
    {
        _concessoes.ObterParaConcederAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(_ => Registrar<Perfil?>("perfil", _perfil));
        _revisoes.TemRevisaoAbertaAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("ABERTA", false));
        _revisoes.TemRevisaoPorEncerrarAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("porMaterializar", false));
    }

    [Fact(DisplayName = "O perfil sem revisão é devolvido, lido com a trava antes do relógio e das revisões")]
    public async Task Obter_SemRevisao_DevolveOPerfilNaOrdem()
    {
        Result<Perfil> resultado = await Obter();

        resultado.Value.Should().BeSameAs(_perfil);
        _ordem.Should().Equal("perfil", "relogio", "ABERTA", "porMaterializar");
    }

    [Fact(DisplayName = "O perfil inexistente é recusado sem consultar revisão nem relógio")]
    public async Task Obter_PerfilInexistente_RecusaSemConsultarARevisao()
    {
        _concessoes.ObterParaConcederAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(_ => Registrar<Perfil?>("perfil", null));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        _ordem.Should().Equal("perfil");
    }

    [Fact(DisplayName = "O perfil com revisão aberta recusa o pedido sem consultar a revisão por materializar")]
    public async Task Obter_RevisaoAberta_RecusaOPedidoEmMigracao()
    {
        _revisoes.TemRevisaoAbertaAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("ABERTA", true));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.PerfilEmRevisaoDasConcessoes);
        _ordem.Should().Equal("perfil", "relogio", "ABERTA");
    }

    [Fact(DisplayName = "O perfil com revisão vencida por materializar responde conflito de encerramento")]
    public async Task Obter_RevisaoPorMaterializar_RecusaPorEncerramento()
    {
        _revisoes.TemRevisaoPorEncerrarAsync(_perfil.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => Registrar("porMaterializar", true));

        Result<Perfil> resultado = await Obter();

        resultado.Error!.Code.Should().Be(RevisaoDasConcessoesErrorCodes.RevisaoEmEncerramento);
        _ordem.Should().Equal("perfil", "relogio", "ABERTA", "porMaterializar");
    }

    private Task<Result<Perfil>> Obter() =>
        new PerfilParaPedir(_concessoes, _revisoes, new RelogioQueRegistra(_ordem)).ObterAsync(_perfil.Id, CancellationToken.None);

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
