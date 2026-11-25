namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class EmissaoDoEnvioDoAnexoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O solicitante recebe a URL de envio, válida por 15 minutos, e o anexo se grava com o pedido marcado")]
    public async Task Emitir_DoSolicitante_EmiteEGrava()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Betina.Identidade, 2048);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ExpiraEm.Should().Be(Agora.AddMinutes(15));
        resultado.Value.TipoDeMidia.Should().Be(EnvioDoPdf.TipoDeMidia);
        cenario.Pedido.Anexos.Should().ContainSingle();
        cenario.Armazenamento.ChavesDeEnvio.Should().Equal(cenario.Pedido.Anexos.Single().ChaveDeEnvio);
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Pedidos.Received(1).Atualizar(cenario.Pedido);
    }

    [Fact(DisplayName = "O pedido de outra pessoa não se distingue do inexistente e não emite URL")]
    public async Task Emitir_DeOutraPessoa_NaoEncontrado()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Leandro.Identidade, 2048);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O tamanho fora do limite descarta o rastreamento e não emite URL")]
    public async Task Emitir_TamanhoInvalido_DescartaSemEmitir()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Betina.Identidade, PdfDeclarado.TamanhoMaximoBytes + 1);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.AnexoInvalido);
        cenario.Unidade.Descartou.Should().BeTrue();
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
        cenario.Pedido.Anexos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O conflito de concorrência devolve a recusa do pedido e descarta o rastreamento")]
    public async Task Emitir_ComConflito_DevolveOConflito()
    {
        Cenario cenario = new(UnidadeDeTrabalho.ComConflito());

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Betina.Identidade, 2048);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        cenario.Unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "A URL sai antes de gravar, e o pedido é marcado antes da gravação, para serializar com a decisão")]
    public async Task Emitir_UrlEMarcaAntesDeGravar()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        (IPedidoDeConcessaoRepository pedidos, IArmazenamentoDosDocumentosDaAutorizacao armazenamento, IUnitOfWork unidade) = Dublês(pedido);

        await Emissao(pedidos, armazenamento, unidade).EmitirAsync(new CriarAnexoDoPedidoCommand(pedido.Id, 2048), ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        Received.InOrder(() =>
        {
            _ = armazenamento.GerarUrlDeEnvioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
            pedidos.Atualizar(pedido);
            _ = unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A falha do armazenamento propaga e nada é gravado nem marcado")]
    public async Task Emitir_FalhaDoArmazenamento_NaoGrava()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        (IPedidoDeConcessaoRepository pedidos, IArmazenamentoDosDocumentosDaAutorizacao armazenamento, IUnitOfWork unidade) = Dublês(pedido);
        armazenamento.GerarUrlDeEnvioAsync(default!, default!, default, default).ReturnsForAnyArgs(_ => Task.FromException<string>(new InvalidOperationException("armazenamento fora")));

        Func<Task> emitir = () => Emissao(pedidos, armazenamento, unidade)
            .EmitirAsync(new CriarAnexoDoPedidoCommand(pedido.Id, 2048), ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        await emitir.Should().ThrowAsync<InvalidOperationException>();
        pedidos.DidNotReceiveWithAnyArgs().Atualizar(default!);
        await unidade.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "O pedido de outra pessoa recusa como não encontrado antes de conferir o tamanho")]
    public async Task Emitir_DeOutraPessoaComTamanhoInvalido_NaoEncontrado()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Leandro.Identidade, 0);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O pedido que já não aceita anexo recusa pela situação antes de conferir o tamanho")]
    public async Task Emitir_PedidoCancelado_SituacaoAntesDoTamanho()
    {
        Cenario cenario = new();
        cenario.Pedido.Cancelar(Agora).IsSuccess.Should().BeTrue();

        Result<AnexoParaEnvioDto> resultado = await cenario.EmitirAsync(ApoioDoPedido.Betina.Identidade, 0);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O handler entrega a URL de envio do anexo ao solicitante do token")]
    public async Task Handle_DoSolicitante_DevolveAUrlDeEnvio()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await CriarAnexoDoPedidoCommandHandler.Handle(
            new CriarAnexoDoPedidoCommand(cenario.Pedido.Id, 2048),
            cenario.Emissao(),
            new DoublesDePerfis.Usuario(ApoioDoPedido.Betina.Identidade),
            CancellationToken.None);

        resultado.Value!.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio.Codigo());
        cenario.Pedido.Anexos.Should().ContainSingle();
    }

    [Fact(DisplayName = "O handler recusa o autor não identificado antes de qualquer leitura")]
    public async Task Handle_SemIdentidade_AutorNaoIdentificado()
    {
        Cenario cenario = new();

        Result<AnexoParaEnvioDto> resultado = await CriarAnexoDoPedidoCommandHandler.Handle(
            new CriarAnexoDoPedidoCommand(cenario.Pedido.Id, 2048), cenario.Emissao(), new DoublesDePerfis.Usuario(null), CancellationToken.None);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.AutorNaoIdentificado);
        await cenario.Pedidos.DidNotReceiveWithAnyArgs().ObterParaAlterarAsync(default, default);
    }

    private static (IPedidoDeConcessaoRepository Pedidos, IArmazenamentoDosDocumentosDaAutorizacao Armazenamento, IUnitOfWork Unidade) Dublês(PedidoDeConcessao pedido)
    {
        IPedidoDeConcessaoRepository pedidos = Substitute.For<IPedidoDeConcessaoRepository>();
        pedidos.ObterParaAlterarAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento = Substitute.For<IArmazenamentoDosDocumentosDaAutorizacao>();
        armazenamento.GerarUrlDeEnvioAsync(default!, default!, default, default).ReturnsForAnyArgs("https://armazenamento.test/envio");

        return (pedidos, armazenamento, Substitute.For<IUnitOfWork>());
    }

    private static EmissaoDoEnvioDoAnexo Emissao(IPedidoDeConcessaoRepository pedidos, IArmazenamentoDosDocumentosDaAutorizacao armazenamento, IUnitOfWork unidade) =>
        new(pedidos, new EnvioDoPdf(armazenamento), unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(Agora));

    private sealed class Cenario(UnidadeDeTrabalho? unidade = null)
    {
        public PedidoDeConcessao Pedido { get; } = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();

        public IPedidoDeConcessaoRepository Pedidos { get; } = Substitute.For<IPedidoDeConcessaoRepository>();

        public DoublesDePerfis.Armazenamento Armazenamento { get; } = new();

        public UnidadeDeTrabalho Unidade { get; } = unidade ?? new UnidadeDeTrabalho();

        public EmissaoDoEnvioDoAnexo Emissao()
        {
            Pedidos.ObterParaAlterarAsync(Pedido.Id, Arg.Any<CancellationToken>()).Returns(Pedido);

            return new EmissaoDoEnvioDoAnexo(Pedidos, new EnvioDoPdf(Armazenamento), Unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(Agora));
        }

        public Task<Result<AnexoParaEnvioDto>> EmitirAsync(Identidade solicitante, long tamanhoDoPdf) =>
            Emissao().EmitirAsync(new CriarAnexoDoPedidoCommand(Pedido.Id, tamanhoDoPdf), solicitante, CancellationToken.None);
    }
}
