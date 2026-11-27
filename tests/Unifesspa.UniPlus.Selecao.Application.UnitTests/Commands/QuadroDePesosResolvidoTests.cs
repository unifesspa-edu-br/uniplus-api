namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

public sealed class QuadroDePesosResolvidoTests
{
    private readonly IPesoAreaEnemReader _pesos = Substitute.For<IPesoAreaEnemReader>();

    [Fact(DisplayName = "A resolução cadastrada e completa traz o quadro com um grupo para cada linha do cadastro")]
    public async Task DaResolucaoAsync_ResolucaoCompleta_TrazOQuadroSemRecusas()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns(ResolucaoCom(GruposDoAnexoI));

        QuadroDePesosResolvido resolvido = await QuadroDePesosResolvido.DaResolucaoAsync(ResolucaoDePesos, _pesos, CancellationToken.None);

        resolvido.Resolucao.Should().Be(ResolucaoDePesos);
        resolvido.Recusas.Should().BeEmpty();
        resolvido.Quadro.Select(static grupo => grupo.GrupoAreaEnem.Codigo).Should().Equal(GruposDoAnexoI.Select(static grupo => grupo.Codigo));
    }

    [Fact(DisplayName = "A resolução com caractere nulo é recusada pela forma, sem consultar o cadastro")]
    public async Task DaResolucaoAsync_FormaInvalida_RecusaSemConsultarOCadastro()
    {
        const string resolucaoComCaractereNulo = "Res. 805\0/2024";

        QuadroDePesosResolvido resolvido = await QuadroDePesosResolvido.DaResolucaoAsync(resolucaoComCaractereNulo, _pesos, CancellationToken.None);

        resolvido.Quadro.Should().BeEmpty();
        resolvido.Recusas.Should().ContainSingle().Which.Field.Should().Be(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem);
        await _pesos.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "A resolução que o cadastro não conhece é recusada como não encontrada")]
    public async Task DaResolucaoAsync_ResolucaoInexistente_RecusaComoNaoEncontrada()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>()).Returns((ResolucaoPesoAreaEnemView?)null);

        QuadroDePesosResolvido resolvido = await QuadroDePesosResolvido.DaResolucaoAsync(ResolucaoDePesos, _pesos, CancellationToken.None);

        resolvido.Quadro.Should().BeEmpty();
        resolvido.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada");
    }

    [Fact(DisplayName = "A resolução sem pesos para todos os grupos é recusada como incompleta")]
    public async Task DaResolucaoAsync_ResolucaoIncompleta_RecusaComoIncompleta()
    {
        _pesos.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>())
            .Returns(ResolucaoCom(GruposDoAnexoI.Take(3), GruposDoAnexoI[3]));

        QuadroDePesosResolvido resolvido = await QuadroDePesosResolvido.DaResolucaoAsync(ResolucaoDePesos, _pesos, CancellationToken.None);

        resolvido.Quadro.Should().BeEmpty();
        resolvido.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta");
    }

    [Fact]
    public void DaResolucaoCadastrada_LinhaInvalida_RecusaSemCopiarQuadro()
    {
        PesoAreaEnemView linha = new(Guid.NewGuid(), "Resolução", new GrupoAreaEnemView("", "Grupo"), [], "Base legal");
        ResolucaoPesoAreaEnemView cadastrada = new("Resolução cadastrada", [linha], []);

        QuadroDePesosResolvido resolvido = QuadroDePesosResolvido.DaResolucaoCadastrada("Resolução informada", cadastrada);

        resolvido.Resolucao.Should().Be("Resolução informada");
        resolvido.Quadro.Should().BeEmpty();
        resolvido.Recusas.Should().NotBeEmpty().And.OnlyContain(erro => erro.Field == ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem);
    }
}
