namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>O anexo declarado, a aprovação com documento pendente e o perfil que passa a exigir o termo.</summary>
public sealed class PedidoDeConcessaoTermoNaDecisaoTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Pedido protegido sem documento declarado não recebe anexo")]
    public void NovoAnexo_ProtegidoSemDeclaracao_Recusa() =>
        Codigos(Pedido().Protegido().Criado().NovoAnexo(1024)).Should().Equal(PedidoErrorCodes.AnexoNaoDeclarado);

    [Fact(DisplayName = "Pedido comum sem declaração continua aceitando anexo até a primeira decisão")]
    public void NovoAnexo_ComumSemDeclaracao_Aceita() =>
        Pedido().AguardandoGestor().NovoAnexo(1024).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Verificação recusada mantém o pedido aguardando o documento, que aceita outro anexo")]
    public void Verificacao_Recusada_ContinuaAguardando()
    {
        PedidoDeConcessao pedido = Pedido().DeclarandoDocumento().Protegido().Criado();
        AnexoDoPedido recusado = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(recusado.Id, "\"etag\"", Agora);
        recusado.RegistrarRecusado(Agora);

        pedido.LiberarAoVerificarDocumento().Should().BeFalse();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        VerificarDocumento(pedido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        Codigos(pedido.NovoAnexo(1024)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "Aprovação com documento pendente é recusada")]
    public void Aprovar_DocumentoPendente_Recusa() =>
        Codigos(Pedido().ParaSiMesmo().DeclarandoDocumento().Protegido().Criado().Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora))
            .Should().Equal(PedidoErrorCodes.SituacaoInvalida);

    [Fact(DisplayName = "Pedido com termo fixado e sem aceite não se aprova, mesmo que o estado gravado o permitisse")]
    public void Aprovar_TermoSemAceite_Recusa()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().Criado();
        typeof(PedidoDeConcessao).GetProperty(nameof(PedidoDeConcessao.Situacao))!
            .SetValue(pedido, SituacaoDoPedido.AguardandoGestor, BindingFlags.NonPublic, null, null, null);

        Codigos(pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        pedido.Decisoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Perfil que passou a exigir o termo depois da criação devolve o pedido na aprovação do gestor")]
    public void Aprovar_PerfilPassouAProtegido_Devolve()
    {
        PedidoDeConcessao pedido = Pedido().AguardandoGestor();

        Codigos(pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.TermoExigido);

        pedido.Situacao.Should().Be(SituacaoDoPedido.Devolvido);
        pedido.EncerradoEm.Should().Be(Agora);
        pedido.Decisoes.Single().Resultado.Should().Be(ResultadoDaDecisao.TermoExigido);
    }

    [Fact(DisplayName = "Pedido com termo aceito segue para a proteção de dados")]
    public void Aprovar_TermoAceito_Segue() =>
        Pedido().Protegido().AguardandoGestor().Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora).Value
            .Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
}
