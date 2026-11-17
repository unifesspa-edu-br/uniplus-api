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

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.ApoioDaRecusa;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>A gravação da decisão da etapa: aprovação que conclui ou não o pedido, devolução e recusa do agregado.</summary>
public sealed class GravacaoDaDecisaoDoPedidoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "A aprovação que não conclui o pedido grava uma vez e responde com o pedido")]
    public async Task Gravar_NaoConcluido_GravaEResponde()
    {
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Gravacao(unidade)
            .GravarAsync(Decisao(), SituacaoDoPedido.AguardandoGestor, concluido: false, Agora, CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        unidade.Gravacoes.Should().Be(1);
        unidade.Descartou.Should().BeFalse();
    }

    [Fact(DisplayName = "A violação de concessão duplicada ao gravar vira a recusa de concessão já existente e descarta o rastreamento")]
    public async Task Gravar_ViolacaoDeConcessaoDuplicada_DevolveJaTemConcessao()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConcessaoDuplicada();

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Gravacao(unidade)
            .GravarAsync(Decisao(), SituacaoDoPedido.AguardandoGestor, concluido: false, Agora, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.JaTemConcessao);
        unidade.Descartou.Should().BeTrue();
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "O conflito de concorrência na aprovação devolve a recusa do pedido e descarta o rastreamento")]
    public async Task Gravar_ComConflito_DevolveOConflito()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConflito();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Gravacao(unidade)
            .GravarAsync(Decisao(), SituacaoDoPedido.AguardandoGestor, concluido: false, Agora, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "A aprovação que conclui o pedido adiciona a concessão do pedido e a grava com ele numa gravação só")]
    public async Task Gravar_Concluido_AdicionaAConcessaoEGrava()
    {
        Perfil perfil = ApoioDoPedido.PerfilComum();
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DoPerfil(perfil).AguardandoGestor();
        Result<DesfechoDaAprovacao> aprovacao = pedido.Aprovar(ApoioDoPedido.Diogo.Identidade, ApoioDoPedido.Interno, ApoioDoPedido.Contexto, Agora);
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();
        IUnitOfWork unidade = Substitute.For<IUnitOfWork>();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Gravacao(unidade, concessoes)
            .GravarAsync(new DecisaoDaEtapa(pedido, perfil, ApoioDoPedido.Interno, ApoioDoPedido.Diogo.Identidade), SituacaoDoPedido.AguardandoGestor, concluido: true, Agora, CancellationToken.None);

        aprovacao.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        resposta.IsSuccess.Should().BeTrue();
        await concessoes.Received(1).AdicionarAsync(Arg.Any<Concessao>(), Arg.Any<CancellationToken>());
        await concessoes.Received(1).AdicionarAsync(Arg.Is<Concessao>(c => c.PerfilId == perfil.Id), Arg.Any<CancellationToken>());
        await unidade.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _ = concessoes.AdicionarAsync(Arg.Any<Concessao>(), Arg.Any<CancellationToken>());
            _ = unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A aprovação que conclui o pedido com concessão igual ativa recusa e não grava")]
    public async Task Gravar_Concluido_ComConcessaoIgualAtiva_NaoGrava()
    {
        DecisaoDaEtapa decisao = Decisao();
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();
        Concessao ativa = ConcessaoBuilder.DoPerfil(decisao.Perfil, new CatalogoInstitucional()).Para(ApoioDoPedido.Betina.Identidade, ApoioDoPedido.Betina.Email).Construir();
        concessoes.ObterNaoRevogadaIgualAsync(default!, default, default!, default).ReturnsForAnyArgs(ativa);
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Gravacao(unidade, concessoes)
            .GravarAsync(decisao, SituacaoDoPedido.AguardandoGestor, concluido: true, Agora, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.JaTemConcessao);
        unidade.Gravacoes.Should().Be(0);
        unidade.Descartou.Should().BeTrue();
    }

    [Theory(DisplayName = "A aprovação recusada por perfil alterado, prazo vencido ou termo exigido grava uma vez e responde com a própria recusa")]
    [InlineData(PedidoErrorCodes.PerfilAlterado)]
    [InlineData(PedidoErrorCodes.PrazoVencido)]
    [InlineData(PedidoErrorCodes.TermoExigido)]
    public async Task RecusarAprovacao_Devolucao_GravaERespondeComARecusa(string codigo)
    {
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Gravacao(unidade)
            .RecusarAprovacaoAsync(Decisao(), Erros(codigo), CancellationToken.None);

        resposta.Error!.Code.Should().Be(codigo);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O conflito ao gravar a devolução devolve a recusa do pedido e descarta o rastreamento")]
    public async Task RecusarAprovacao_DevolucaoComConflito_DevolveOConflito()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConflito();

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Gravacao(unidade)
            .RecusarAprovacaoAsync(Decisao(), Erros(PedidoErrorCodes.PerfilAlterado), CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartou.Should().BeTrue();
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "As demais recusas do agregado descartam o rastreamento, não gravam e devolvem as violações")]
    public async Task RecusarAprovacao_OutraRecusa_DescartaSemGravar()
    {
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Gravacao(unidade)
            .RecusarAprovacaoAsync(Decisao(), Erros(PedidoErrorCodes.SituacaoInvalida), CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.SituacaoInvalida);
        unidade.Descartou.Should().BeTrue();
        unidade.Gravacoes.Should().Be(0);
        mensagens.Should().BeEmpty();
    }

    private static FieldError[] Erros(string codigo) => [new("pedido", new DomainError(codigo, "Recusa de teste."))];
}
