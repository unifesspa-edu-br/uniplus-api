namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// As decisões do pedido que têm o mesmo instante saem na ordem em que foram registradas, e a decisão ainda não
/// gravada, sem número de ordem, vem por último.
/// </summary>
public sealed class DecisoesDoPedidoNoMesmoInstanteTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Primeiro = new("https://idp.test/realms/unifesspa", "isabella.ferreira");
    private static readonly Identidade Segundo = new("https://idp.test/realms/unifesspa", "diogo.souza");

    [Fact(DisplayName = "A decisão ainda não gravada vem depois da gravada no mesmo instante")]
    public void MesmoInstante_DecisaoNaoGravadaVemPorUltimo()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Primeiro, new(["a"], "sensivel", true, null), new(false), Instante);
        pedido.Aprovar(Segundo, new(["a", "b"], "sensivel", true, null), new(false), Instante);
        Numerar(pedido.Decisoes.First(d => d.DecisorSub == Primeiro.Sub), 7);

        string[] decisores = [.. PedidoMapping.ToDto(pedido, null).Decisoes.Select(d => d.Decisor.Sub)];

        decisores.Should().Equal(Primeiro.Sub, Segundo.Sub);
    }

    // A ordem de registro é do banco: o teste simula a decisão relida com o número que o banco lhe deu.
    private static void Numerar(DecisaoDoPedido decisao, long ordem) =>
        typeof(DecisaoDoPedido).GetProperty(nameof(DecisaoDoPedido.OrdemDeRegistro), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(decisao, ordem);
}
