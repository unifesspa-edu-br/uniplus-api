namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O aviso próprio do pedido devolvido porque o perfil passou a exigir o termo de responsabilidade.</summary>
public sealed class AvisoDeDevolucaoPorTermoExigidoTests
{
    private readonly CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria _registro = new();

    [Fact(DisplayName = "O pedido devolvido por exigir o termo avisa o solicitante para refazê-lo, com o link do pedido e sem dado pessoal")]
    public async Task Devolvido_AvisaOSolicitante()
    {
        PedidoDeConcessao pedido = Devolvido();

        EnviarEmail aviso = (await AvisarAsync(pedido)).Should().ContainSingle().Subject;

        aviso.Destinatario.Should().Be(PedidoDeConcessaoTestBase.Betina.Email);
        aviso.Finalidade.Should().Be("perfil.pedido_devolvido_por_termo_exigido");
        aviso.CorpoTexto.Should().Contain("refaça-o").And.Contain($"/acessos/pedidos/{pedido.Id}")
            .And.NotContain(PedidoDeConcessaoTestBase.Betina.Nome!).And.NotContain(PedidoDeConcessaoTestBase.Justificativa);
    }

    [Fact(DisplayName = "O aviso da devolução por exigir o termo não se repete para o mesmo pedido")]
    public async Task Devolvido_NaoRepete()
    {
        PedidoDeConcessao pedido = Devolvido();

        (await AvisarAsync(pedido)).Should().ContainSingle();
        (await AvisarAsync(pedido)).Should().BeEmpty();
    }

    private static PedidoDeConcessao Devolvido()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Aprovar(new Identidade(PedidoDeConcessaoTestBase.Emissor, "diogo.souza"), new(["a"], "sensivel", true, null), new(false), DateTimeOffset.UtcNow)
            .IsFailure.Should().BeTrue();

        return pedido;
    }

    private Task<IReadOnlyList<EnviarEmail>> AvisarAsync(PedidoDeConcessao pedido) =>
        new AvisoDeDevolucaoPorTermoExigido(
                new EmissorDeAvisos(_registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance), new DoublesDeAvisos.LinkDeTeste())
            .AvisarAsync(pedido, "Análise de atendimento", CancellationToken.None);
}
