namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>A decisão da etapa e a gravação da decisão montadas para os testes da recusa do pedido.</summary>
internal static class ApoioDaRecusa
{
    /// <summary>O pedido de Betina aguardando o gestor, com o perfil sensível que o decisor avalia.</summary>
    public static DecisaoDaEtapa Decisao()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();

        return new DecisaoDaEtapa(pedido, ApoioDoPedido.PerfilComum(), ApoioDoPedido.Sensivel, ApoioDoPedido.Diogo.Identidade);
    }

    public static GravacaoDaDecisaoDoPedido Gravacao(IUnitOfWork unidade, IConcessaoRepository? concessoes = null) =>
        new(
            unidade,
            new DoublesDeAvisos.PerfilQualquer(),
            DoublesDeAvisos.Avisos(),
            new ConcessaoDoPedidoAprovado(concessoes ?? Substitute.For<IConcessaoRepository>(), ApoioDoPedido.Catalogo, Substitute.For<IIncompatibilidadeDosPapeis>()),
            new AvisoDeDevolucaoPorTermoExigido(
                new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new DoublesDeAvisos.LinkDeTeste()));
}
