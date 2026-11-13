namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>A ordem documento verificado, aceite do termo e gestor, e quem aceita.</summary>
public sealed class PedidoDeConcessaoTermoNoAceiteTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Pedido próprio protegido com documento: o aceite espera a verificação e só então vai ao gestor")]
    public void Aceitar_ParaSiComDocumento_DepoisDaVerificacao()
    {
        PedidoDeConcessao pedido = Pedido().ParaSiMesmo().DeclarandoDocumento().Protegido().Criado();

        Codigos(pedido.Aceitar(Betina, pedido.TermoVersaoId, Agora)).Should().Equal(PedidoErrorCodes.AnexoNaoVerificado);
        VerificarDocumento(pedido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario, "o pedido protegido passa pelo aceite antes do gestor");
        pedido.Aceitar(Betina, pedido.TermoVersaoId, Agora).IsSuccess.Should().BeTrue();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.AceiteDoTermo!.Pessoa.Should().Be(Betina.Identidade);
    }

    [Fact(DisplayName = "Pedido próprio comum com documento: depois da verificação vai direto ao gestor")]
    public void Liberar_ParaSiComumComDocumento_VaiAoGestor()
    {
        PedidoDeConcessao pedido = Pedido().ParaSiMesmo().DeclarandoDocumento().Criado();

        VerificarDocumento(pedido);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Theory(DisplayName = "Pedido protegido para outra pessoa: o aceite do termo é o aceite do pedido, depois do documento quando declarado")]
    [InlineData(false)]
    [InlineData(true)]
    public void Aceitar_ParaOutraPessoa(bool comDocumento)
    {
        PedidoDeConcessaoBuilder cenario = comDocumento ? Pedido().DeclarandoDocumento() : Pedido();
        PedidoDeConcessao pedido = cenario.Protegido().Criado();
        if (comDocumento)
        {
            VerificarDocumento(pedido);
        }

        pedido.Aceitar(Leandro, pedido.TermoVersaoId, Agora).IsSuccess.Should().BeTrue();

        pedido.AceiteDoTermo!.Pessoa.Should().Be(Leandro.Identidade);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Fact(DisplayName = "Só o beneficiário aceita: nem o solicitante nem outra pessoa aceitam por ele")]
    public void Aceitar_SoOBeneficiario()
    {
        PedidoDeConcessao paraOutra = Pedido().Protegido().Criado();
        PedidoDeConcessao proprio = Pedido().ParaSiMesmo().DeclarandoDocumento().Protegido().Criado();
        VerificarDocumento(proprio);

        Codigos(paraOutra.Aceitar(Betina, paraOutra.TermoVersaoId, Agora)).Should().Equal(PedidoErrorCodes.NaoEOBeneficiario);
        Codigos(proprio.Aceitar(Leandro with { Email = Betina.Email }, proprio.TermoVersaoId, Agora))
            .Should().Equal(PedidoErrorCodes.NaoEOBeneficiario);
    }

    [Fact(DisplayName = "No aceite, a versão ausente ou diferente da fixada é recusada; no pedido sem termo, a versão é indevida")]
    public void Aceitar_VersaoApresentada()
    {
        PedidoDeConcessao comTermo = Pedido().Protegido().Criado();
        PedidoDeConcessao semTermo = Pedido().Criado();

        Codigos(comTermo.Aceitar(Leandro, null, Agora)).Should().Equal(PedidoErrorCodes.ApresentacaoDoTermoInvalida);
        Codigos(comTermo.Aceitar(Leandro, Guid.NewGuid(), Agora)).Should().Equal(PedidoErrorCodes.TermoDivergente);
        Codigos(semTermo.Aceitar(Leandro, Guid.NewGuid(), Agora)).Should().Equal(PedidoErrorCodes.ApresentacaoDoTermoInvalida);
        comTermo.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
    }

    [Fact(DisplayName = "A versão fixada continua a do pedido depois que outra versão é publicada")]
    public void Aceitar_DepoisDeNovaPublicacao_ValeAFixada()
    {
        PedidoDeConcessaoBuilder cenario = Pedido().Protegido();
        PedidoDeConcessao pedido = cenario.Criado();
        VersaoDoTermo nova = TermosDeTeste.Publicada("segunda versão");

        Codigos(pedido.Aceitar(Leandro, nova.Id, Agora)).Should().Equal(PedidoErrorCodes.TermoDivergente);
        pedido.Aceitar(Leandro, cenario.Vigente.Id, Agora).IsSuccess.Should().BeTrue();
        pedido.AceiteDoTermo!.VersaoDoTermoId.Should().Be(cenario.Vigente.Id);
    }
}
