namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>A recusa da etapa do pedido, na ordem das recusas: autor, permissão, pedido, etapa e recusa do agregado.</summary>
public sealed class RecusaDoPedidoTests
{
    private const string Funcional = "autorizacao:pedidos:aprovar-funcional";

    private const string Protecao = "autorizacao:pedidos:aprovar-protecao-de-dados";

    private static readonly RecusarPedidoDeConcessaoCommand Comando = new(Guid.CreateVersion7(), "Não atende o perfil.");

    [Fact(DisplayName = "Sem identidade no token, a recusa é do autor não identificado, antes de qualquer outra conferência")]
    public async Task Recusar_SemDecisor_AutorNaoIdentificado()
    {
        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(new UnidadeDeTrabalho())
            .RecusarAsync(Comando, new VerificadorDeTeste("diogo.souza", [Funcional]), null, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.AutorNaoIdentificado);
    }

    [Fact(DisplayName = "Quem não decide etapa alguma é recusado por falta de permissão")]
    public async Task Recusar_SemPermissao_Recusa()
    {
        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(new UnidadeDeTrabalho())
            .RecusarAsync(Comando, new VerificadorDeTeste("paulo.nogueira", []), ApoioDoPedido.Diogo.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.SemPermissao);
    }

    [Fact(DisplayName = "O pedido que não existe não é encontrado, mesmo para quem decide")]
    public async Task Recusar_PedidoInexistente_NaoEncontrado()
    {
        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(new UnidadeDeTrabalho())
            .RecusarAsync(Comando, new VerificadorDeTeste("diogo.souza", [Funcional]), ApoioDoPedido.Diogo.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Quem decide a etapa recusa o pedido, que se grava uma vez como recusado")]
    public async Task Recusar_DoGestor_RecusaEGrava()
    {
        Perfil perfil = ApoioDoPedido.PerfilComum();
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DoPerfil(perfil).AguardandoGestor();
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(unidade, pedido, perfil)
            .RecusarAsync(Comando with { Id = pedido.Id }, new VerificadorDeTeste("diogo.souza", [Funcional]), ApoioDoPedido.Diogo.Identidade, CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.Recusado);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Quem decide só a outra etapa recebe a recusa de etapa, antes da validação do motivo pelo agregado")]
    public async Task Recusar_EtapaDeOutroDecisor_EtapaInvalidaAntesDoMotivo()
    {
        Perfil perfil = ApoioDoPedido.PerfilComum();
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DoPerfil(perfil).AguardandoGestor();
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(unidade, pedido, perfil)
            .RecusarAsync(new RecusarPedidoDeConcessaoCommand(pedido.Id, null), new VerificadorDeTeste("diogo.souza", [Protecao]), ApoioDoPedido.Diogo.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.EtapaInvalida);
        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Sem motivo, o agregado recusa a recusa e nada se grava")]
    public async Task Recusar_SemMotivo_RecusadaPeloAgregado()
    {
        Perfil perfil = ApoioDoPedido.PerfilComum();
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DoPerfil(perfil).AguardandoGestor();
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusa(unidade, pedido, perfil)
            .RecusarAsync(new RecusarPedidoDeConcessaoCommand(pedido.Id, null), new VerificadorDeTeste("diogo.souza", [Funcional]), ApoioDoPedido.Diogo.Identidade, CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        unidade.Gravacoes.Should().Be(0);
        unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "O handler leva a identidade do token à recusa: sem identidade, o autor não é identificado")]
    public async Task Handle_SemIdentidade_AutorNaoIdentificado()
    {
        (Result<PedidoDeConcessaoDto> resposta, _) = await RecusarPedidoDeConcessaoCommandHandler.Handle(
            Comando, Recusa(new UnidadeDeTrabalho()), new VerificadorDeTeste("diogo.souza", [Funcional]), new DoublesDePerfis.Usuario(null), CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.AutorNaoIdentificado);
    }

    private static RecusaDoPedido Recusa(UnidadeDeTrabalho unidade, PedidoDeConcessao? pedido = null, Perfil? perfil = null)
    {
        IPedidoDeConcessaoRepository pedidos = Substitute.For<IPedidoDeConcessaoRepository>();
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();
        if (pedido is not null && perfil is not null)
        {
            pedidos.ObterPerfilIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(perfil.Id);
            pedidos.ObterParaAlterarAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
            concessoes.ObterParaConcederAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(perfil);
        }

        return new RecusaDoPedido(pedidos, concessoes, new CatalogoDaAplicacao([]), ApoioDaRecusa.Gravacao(unidade), TimeProvider.System);
    }
}
