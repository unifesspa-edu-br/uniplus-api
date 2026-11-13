namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>O pedido de perfil com designação, o documento e a dispensa da segunda etapa na designação do primeiro Representante do controlador.</summary>
public sealed class PedidoDeConcessaoDesignacaoTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Pedido de perfil com designação nasce aguardando o documento, fora das etapas de decisão e do aceite")]
    public void Criar_PerfilComDesignacao_NasceAguardandoODocumento()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(new PartesDoPedido(Betina, Leandro.Email), PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora).Value!;

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        pedido.Ativo.Should().BeTrue("o pedido repetido continua barrado enquanto espera o documento");
        pedido.EtapaAtual.Should().BeNull();
        Codigos(pedido.Aceitar(Leandro, null, Agora)).Should().Equal(PedidoErrorCodes.AnexoNaoVerificado);
        Codigos(pedido.Aprovar(Diogo.Identidade, DeGestor, Contexto, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        pedido.NovoAnexo(1024).IsSuccess.Should().BeTrue("o solicitante anexa o documento justamente nesta situação");
    }

    [Fact(DisplayName = "Pedido de perfil com designação pode ser cancelado enquanto espera o documento")]
    public void Cancelar_AguardandoDocumento_Encerra()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(new PartesDoPedido(Betina, Leandro.Email), PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora).Value!;

        pedido.Cancelar(Agora).IsSuccess.Should().BeTrue();

        pedido.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
    }

    [Theory(DisplayName = "O documento verificado libera o pedido para o aceite do beneficiário, ou para o gestor no pedido para si mesmo")]
    [InlineData(false, SituacaoDoPedido.AguardandoBeneficiario)]
    [InlineData(true, SituacaoDoPedido.AguardandoGestor)]
    public void LiberarAoVerificarDocumento_SegueParaAEtapaSeguinte(bool paraSiMesmo, SituacaoDoPedido esperada)
    {
        PedidoDeConcessao pedido = PedidoComDesignacao(paraSiMesmo ? Leandro : Betina);
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);

        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeTrue();
        pedido.LiberarAoVerificarDocumento().Should().BeTrue();

        pedido.Situacao.Should().Be(esperada);
        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o pedido já seguiu");
    }

    [Fact(DisplayName = "O documento sem confirmação ou ainda sem veredicto não libera o pedido")]
    public void LiberarAoVerificarDocumento_SemVeredicto_NaoLibera()
    {
        PedidoDeConcessao pedido = PedidoComDesignacao(Betina);
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;

        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o anexo ainda não foi confirmado");
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora).IsSuccess.Should().BeTrue();
        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o anexo está em verificação, sem veredicto");
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
    }

    private static PedidoDeConcessao PedidoComDesignacao(PessoaNoToken solicitante) =>
        PedidoDeConcessao.Criar(new PartesDoPedido(solicitante, Leandro.Email), PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora).Value!;

    [Fact(DisplayName = "O documento recusado pelo verificador não libera o pedido")]
    public void LiberarAoVerificarDocumento_AnexoRecusado_NaoLibera()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(new PartesDoPedido(Betina, Leandro.Email), PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora).Value!;
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);
        anexo.RegistrarRecusado(Agora).Should().BeTrue();

        pedido.LiberarAoVerificarDocumento().Should().BeFalse();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
    }

    [Fact(DisplayName = "A designação do primeiro Representante do controlador passa só pelo gestor, com a dispensa na trilha")]
    public void Aprovar_PrimeiroRepresentante_DispensaASegundaAprovacao()
    {
        PedidoDeConcessao pedido = Pedido().DoPerfil(PerfilDeGovernanca(TipoDeGovernanca.RepresentanteDoControlador)).AguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, DeRepresentante, SemRepresentante, Agora);

        resultado.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.Decisoes.Single().Dispensa.Should().Be(MotivosDeDispensa.DesignacaoDeProtecaoDeDados);
    }

    [Fact(DisplayName = "Com Representante vigente, a designação de outro passa pela segunda etapa, sem dispensa")]
    public void Aprovar_SegundoRepresentante_AguardaASegundaEtapa()
    {
        PedidoDeConcessao pedido = Pedido().DoPerfil(PerfilDeGovernanca(TipoDeGovernanca.RepresentanteDoControlador)).AguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, DeRepresentante, ComRepresentante, Agora);

        resultado.Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);
        pedido.Decisoes.Single().Dispensa.Should().BeNull();
    }

    [Fact(DisplayName = "O segundo Representante é decidido por outro Representante, e o beneficiário não decide a própria designação")]
    public void Aprovar_SegundoRepresentante_DecididoPorOutroRepresentante()
    {
        PedidoDeConcessao pedido = Pedido().DoPerfil(PerfilDeGovernanca(TipoDeGovernanca.RepresentanteDoControlador)).AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, DeRepresentante, ComRepresentante, Agora);

        Codigos(pedido.Aprovar(Leandro.Identidade, DeRepresentante, ComRepresentante, Agora))
            .Should().Equal(PedidoErrorCodes.DecisorEParte);
        pedido.Aprovar(Marlene.Identidade, DeRepresentante, ComRepresentante, Agora).Value.Should().Be(DesfechoDaAprovacao.Concluido);
    }

    [Theory(DisplayName = "O pedido sensível de quem já é Representante passa pela segunda etapa, sem dispensa")]
    [InlineData(false)]
    [InlineData(true)]
    public void Aprovar_BeneficiarioRepresentante_AguardaASegundaEtapa(bool haRepresentanteVigente)
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, Sensivel, new ContextoDaAprovacao(haRepresentanteVigente), Agora);

        resultado.Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
        pedido.Decisoes.Single().Dispensa.Should().BeNull();
    }

    [Fact(DisplayName = "O perfil que não exige a segunda etapa termina no gestor, sem dispensa")]
    public void Aprovar_PerfilInterno_TerminaNoGestorSemDispensa()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        pedido.Aprovar(Diogo.Identidade, Interno, ComRepresentante, Agora).Value.Should().Be(DesfechoDaAprovacao.Concluido);

        pedido.Decisoes.Single().Dispensa.Should().BeNull();
    }

    private static ContextoDaAprovacao SemRepresentante => new(HaRepresentanteVigente: false);

    private static ContextoDaAprovacao ComRepresentante => new(HaRepresentanteVigente: true);
}
