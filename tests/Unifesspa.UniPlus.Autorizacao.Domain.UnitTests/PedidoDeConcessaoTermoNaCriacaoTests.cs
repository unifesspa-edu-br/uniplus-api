namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>A fixação do termo de responsabilidade na criação do pedido protegido.</summary>
public sealed class PedidoDeConcessaoTermoNaCriacaoTests : PedidoDeConcessaoTestBase
{
    private static readonly VersaoDoTermo Vigente = TermosDeTeste.Publicada();

    [Fact(DisplayName = "Pedido protegido para si sem documento nasce aguardando o gestor, com o aceite registrado no envio")]
    public void Fixar_ParaSiSemDocumento_EnvioEOAceite()
    {
        PedidoDeConcessao pedido = Pedido().ParaSiMesmo().Protegido().ComVigente(Vigente).Criado();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Termo.Should().Be(new TermoFixado(Vigente.Id, 1, Vigente.Resumo));
        pedido.AceiteDoTermo!.Pessoa.Should().Be(Betina.Identidade);
        pedido.AceiteDoTermo.AceitoEm.Should().Be(Agora);
        pedido.AceiteDoTermo.Resumo.Should().Be(Vigente.Resumo);
    }

    [Fact(DisplayName = "Pedido protegido para outra pessoa fixa a versão sem registrar aceite de quem pede")]
    public void Fixar_ParaOutraPessoa_SemAceite()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().ComVigente(Vigente).Criado();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedido.TermoVersaoId.Should().Be(Vigente.Id);
        pedido.AceiteDoTermo.Should().BeNull();
        pedido.AguardaAceiteDoTermo.Should().BeTrue();
    }

    [Fact(DisplayName = "Pedido protegido para si com documento nasce aguardando o documento, sem aceite")]
    public void Fixar_ParaSiComDocumento_AguardaODocumento()
    {
        PedidoDeConcessao pedido = Pedido().ParaSiMesmo().DeclarandoDocumento().Protegido().ComVigente(Vigente).Criado();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        pedido.AnexaraDocumento.Should().BeTrue();
        pedido.AceiteDoTermo.Should().BeNull();
    }

    [Fact(DisplayName = "Pedido protegido sem termo publicado é recusado")]
    public void Fixar_SemVigente_Recusa() =>
        Codigos(Novo(paraSiMesmo: false).FixarTermo(new TermoNaCriacao(null, null, true), Agora))
            .Should().Equal(PedidoErrorCodes.SemTermoVigente);

    [Fact(DisplayName = "A versão apresentada diferente da vigente é recusada com conflito")]
    public void Fixar_Divergente_Recusa() =>
        Codigos(Novo(paraSiMesmo: false).FixarTermo(new TermoNaCriacao(Vigente, Guid.NewGuid(), true), Agora))
            .Should().Equal(PedidoErrorCodes.TermoDivergente);

    [Fact(DisplayName = "No envio que é o aceite, a versão apresentada é obrigatória")]
    public void Fixar_EnvioSemApresentada_Recusa()
    {
        PedidoDeConcessao pedido = Novo(paraSiMesmo: true);

        pedido.FixarTermo(new TermoNaCriacao(Vigente, null, true), Agora).Errors.Should().ContainSingle()
            .Which.Field.Should().Be("termoVersaoApresentadaId");
        pedido.TermoVersaoId.Should().BeNull();
    }

    [Fact(DisplayName = "No pedido protegido para outra pessoa, a versão apresentada também é obrigatória (UNI-REQ-0377)")]
    public void Fixar_ParaOutraPessoaSemApresentada_Recusa()
    {
        PedidoDeConcessao pedido = Novo(paraSiMesmo: false);

        Codigos(pedido.FixarTermo(new TermoNaCriacao(Vigente, null, true), Agora)).Should().Equal(PedidoErrorCodes.ApresentacaoDoTermoInvalida);
        pedido.TermoVersaoId.Should().BeNull();
    }

    [Fact(DisplayName = "No pedido protegido para outra pessoa, a versão apresentada se fixa sem registrar aceite de quem pede")]
    public void Fixar_ParaOutraPessoaComApresentada_FixaSemAceite()
    {
        PedidoDeConcessao pedido = Novo(paraSiMesmo: false);

        pedido.FixarTermo(new TermoNaCriacao(Vigente, Vigente.Id, true), Agora).IsSuccess.Should().BeTrue();
        (pedido.TermoVersaoId, pedido.AceiteDoTermo).Should().Be((Vigente.Id, null));
    }

    [Fact(DisplayName = "Pedido não protegido não fixa termo e recusa a versão apresentada")]
    public void Fixar_NaoProtegido()
    {
        PedidoDeConcessao pedido = Pedido().ComVigente(Vigente).Criado();

        pedido.Termo.Should().BeNull();
        Codigos(Novo(paraSiMesmo: false).FixarTermo(new TermoNaCriacao(null, Vigente.Id, false), Agora))
            .Should().Equal(PedidoErrorCodes.ApresentacaoDoTermoInvalida);
    }

    [Fact(DisplayName = "O perfil de governança exige o termo e declara sempre o documento de designação")]
    public void Criar_Governanca_ExigeTermoEDocumento()
    {
        PedidoDeConcessao pedido = Pedido().DoPerfil(PerfilDeGovernanca()).ComVigente(Vigente).Criado();

        PedidoDeConcessao.ExigeTermo(PerfilDeGovernanca(), Catalogo).Should().BeTrue();
        PedidoDeConcessao.ExigeTermo(PerfilComum(), Catalogo).Should().BeFalse();
        pedido.AnexaraDocumento.Should().BeTrue();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        pedido.TermoVersaoId.Should().Be(Vigente.Id);
    }

    private static PedidoDeConcessao Novo(bool paraSiMesmo) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(Betina, paraSiMesmo ? null : Leandro.Email), PerfilComum(), EscopoDe(null), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora).Value!;
}
