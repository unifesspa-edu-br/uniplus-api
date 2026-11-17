namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class ConfirmacaoDoAnexoDoPedidoTests
{
    private const string ETag = "\"etag-do-put\"";

    [Fact(DisplayName = "O solicitante confirma o envio: o anexo vai à verificação, o pedido se marca e grava uma vez")]
    public async Task Confirmar_DoSolicitante_PoeEmVerificacao()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, cenario.Anexo.Id, ETag);

        resultado.IsSuccess.Should().BeTrue();
        cenario.Anexo.Situacao.Should().Be(SituacaoDoAnexo.EmVerificacao);
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Pedidos.Received(1).Atualizar(cenario.Pedido);
    }

    [Fact(DisplayName = "O pedido de outra pessoa não se distingue do inexistente")]
    public async Task Confirmar_DeOutraPessoa_NaoEncontrado()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Leandro.Identidade, cenario.Anexo.Id, ETag);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        cenario.Anexo.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O anexo que o pedido não tem não é encontrado, e nada se grava")]
    public async Task Confirmar_AnexoInexistente_DescartaSemGravar()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, Guid.NewGuid(), ETag);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        cenario.Unidade.Descartou.Should().BeTrue();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O ETag em branco é recusado pelo anexo, sem gravar")]
    public async Task Confirmar_ETagEmBranco_Recusa()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, cenario.Anexo.Id, "  ");

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.AnexoInvalido);
        cenario.Anexo.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
        cenario.Unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "O conflito de concorrência devolve a recusa do pedido e descarta o rastreamento")]
    public async Task Confirmar_ComConflito_DevolveOConflito()
    {
        Cenario cenario = new(UnidadeDeTrabalho.ComConflito());

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, cenario.Anexo.Id, ETag);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        cenario.Unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "O pedido é marcado antes da gravação, para serializar a confirmação com a decisão")]
    public async Task Confirmar_MarcaOPedidoAntesDeGravar()
    {
        Cenario cenario = new();
        IUnitOfWork unidade = Substitute.For<IUnitOfWork>();

        await new ConfirmacaoDoAnexoDoPedido(cenario.Pedidos, unidade, TimeProvider.System)
            .ConfirmarAsync(new ConfirmarAnexoDoPedidoCommand(cenario.Pedido.Id, cenario.Anexo.Id, ETag), ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        Received.InOrder(() =>
        {
            cenario.Pedidos.Atualizar(cenario.Pedido);
            _ = unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "O pedido de outra pessoa recusa como não encontrado antes de conferir o anexo e o ETag")]
    public async Task Confirmar_DeOutraPessoa_PedidoAntesDoAnexo()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Leandro.Identidade, Guid.NewGuid(), " ");

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        cenario.Unidade.Descartou.Should().BeFalse();
    }

    [Fact(DisplayName = "O pedido que já não se altera recusa pela situação, antes do anexo inexistente e do ETag em branco")]
    public async Task Confirmar_PedidoCancelado_SituacaoAntesDoAnexoEDoETag()
    {
        Cenario cenario = new();
        cenario.Pedido.Cancelar(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, Guid.NewGuid(), " ");

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O anexo inexistente recusa antes de conferir o ETag")]
    public async Task Confirmar_AnexoInexistenteEETagEmBranco_AnexoAntesDoETag()
    {
        Cenario cenario = new();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, Guid.NewGuid(), " ");

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O anexo que já está em verificação recusa pela situação antes de conferir o ETag")]
    public async Task Confirmar_AnexoJaConfirmado_SituacaoAntesDoETag()
    {
        Cenario cenario = new();
        (await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, cenario.Anexo.Id, ETag)).IsSuccess.Should().BeTrue();

        Result<AnexoDoPedido> resultado = await cenario.ConfirmarAsync(ApoioDoPedido.Betina.Identidade, cenario.Anexo.Id, " ");

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O handler devolve o anexo em verificação e o pedido ao verificador, com o ETag e o tamanho declarado")]
    public async Task Handle_Confirmado_PedeAVerificacao()
    {
        Cenario cenario = new();

        (Result<AnexoDoPedidoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarAnexoDoPedidoCommandHandler.Handle(
            new ConfirmarAnexoDoPedidoCommand(cenario.Pedido.Id, cenario.Anexo.Id, ETag),
            cenario.Confirmacao(),
            new DoublesDePerfis.Usuario(ApoioDoPedido.Betina.Identidade),
            CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        PedidoDeVerificacaoDeArquivo pedido = mensagens.Should().ContainSingle().Subject.Should().BeOfType<PedidoDeVerificacaoDeArquivo>().Subject;
        pedido.ChaveDeEnvio.Should().Be(cenario.Anexo.ChaveDeEnvio);
        pedido.ETag.Should().Be(ETag);
        pedido.TamanhoMaximoBytes.Should().Be(2048);
        pedido.Destino.Should().Be(DestinoDoResultado.Autorizacao);
    }

    [Fact(DisplayName = "O handler recusa o autor não identificado antes de qualquer leitura")]
    public async Task Handle_SemIdentidade_AutorNaoIdentificado()
    {
        Cenario cenario = new();

        (Result<AnexoDoPedidoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarAnexoDoPedidoCommandHandler.Handle(
            new ConfirmarAnexoDoPedidoCommand(cenario.Pedido.Id, cenario.Anexo.Id, ETag),
            cenario.Confirmacao(),
            new DoublesDePerfis.Usuario(null),
            CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.AutorNaoIdentificado);
        mensagens.Should().BeEmpty();
        await cenario.Pedidos.DidNotReceiveWithAnyArgs().ObterParaAlterarAsync(default, default);
    }

    private sealed class Cenario
    {
        public Cenario(UnidadeDeTrabalho? unidade = null)
        {
            Pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
            Anexo = Pedido.NovoAnexo(2048).Value!;
            Unidade = unidade ?? new UnidadeDeTrabalho();
            Pedidos.ObterParaAlterarAsync(Pedido.Id, Arg.Any<CancellationToken>()).Returns(Pedido);
        }

        public PedidoDeConcessao Pedido { get; }

        public AnexoDoPedido Anexo { get; }

        public IPedidoDeConcessaoRepository Pedidos { get; } = Substitute.For<IPedidoDeConcessaoRepository>();

        public UnidadeDeTrabalho Unidade { get; }

        public ConfirmacaoDoAnexoDoPedido Confirmacao() => new(Pedidos, Unidade, TimeProvider.System);

        public Task<Result<AnexoDoPedido>> ConfirmarAsync(Identidade solicitante, Guid anexoId, string etag) =>
            Confirmacao().ConfirmarAsync(new ConfirmarAnexoDoPedidoCommand(Pedido.Id, anexoId, etag), solicitante, CancellationToken.None);
    }
}
