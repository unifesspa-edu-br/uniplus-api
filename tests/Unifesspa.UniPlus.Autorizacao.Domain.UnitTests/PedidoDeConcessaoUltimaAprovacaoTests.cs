namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>
/// A aprovação do gestor que a proteção de dados confere é a última registrada. Com o gestor aprovando de novo no mesmo
/// instante, o que decide é a ordem de registro, e a decisão ainda não gravada é a mais recente.
/// </summary>
public sealed class PedidoDeConcessaoUltimaAprovacaoTests : PedidoDeConcessaoTestBase
{
    private static readonly PerfilAvaliado Ampliado = new([.. Sensivel.Permissoes, "selecao:inscricoes:exportar"], "sensivel", true, null);

    [Fact(DisplayName = "A segunda aprovação do gestor, no mesmo instante da primeira, é a que a proteção de dados confere")]
    public void MesmoInstante_DecisaoNaoGravadaEAMaisRecente()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);
        Codigos(pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora)).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Aprovar(Diogo.Identidade, Ampliado, Contexto, Agora).IsSuccess.Should().BeTrue();

        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Entre decisões gravadas no mesmo instante vale a de maior ordem de registro, qualquer que seja a posição na lista")]
    [InlineData(9, 3, false)]
    [InlineData(3, 9, true)]
    public void MesmoInstante_ValeAMaiorOrdemDeRegistro(long ordemDaPrimeira, long ordemDaSegunda, bool proteDeDadosAprova)
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);
        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora);
        pedido.Aprovar(Diogo.Identidade, Ampliado, Contexto, Agora);
        DecisaoDoPedido[] aprovacoesDoGestor = [.. pedido.Decisoes.Where(d => d.Etapa == EtapaDoPedido.Gestor)];
        Numerar(aprovacoesDoGestor[0], ordemDaPrimeira);
        Numerar(aprovacoesDoGestor[1], ordemDaSegunda);

        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora).IsSuccess.Should().Be(proteDeDadosAprova);
    }

    [Fact(DisplayName = "Com o relógio que regride, a aprovação do gestor de maior instante continua sendo a última")]
    public void RelogioQueRegride_ValeOInstante()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora.AddMinutes(5));
        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora.AddMinutes(6));
        pedido.Aprovar(Diogo.Identidade, Ampliado, Contexto, Agora);

        Codigos(pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora.AddMinutes(7))).Should().Equal(PedidoErrorCodes.PerfilAlterado);
    }

    // A ordem de registro é do banco: o teste simula a decisão relida com o número que o banco lhe deu.
    private static void Numerar(DecisaoDoPedido decisao, long ordem) =>
        typeof(DecisaoDoPedido).GetProperty(nameof(DecisaoDoPedido.OrdemDeRegistro), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(decisao, ordem);
}
