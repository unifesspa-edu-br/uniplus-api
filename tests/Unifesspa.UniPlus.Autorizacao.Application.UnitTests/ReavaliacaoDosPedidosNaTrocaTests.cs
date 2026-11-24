namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// A reavaliação dos pedidos ativos do perfil quando uma versão nova entra em vigor: o pedido devolvido avisa as duas partes,
/// o que volta ao gestor avisa a fila dele e o que só troca de versão segue sem aviso. Só personas fictícias do portal.
/// </summary>
public sealed class ReavaliacaoDosPedidosNaTrocaTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string A = "geral:pedidos:consultar";
    private const string B = "geral:pedidos:analisar";
    private const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";

    private static readonly CatalogoLivre Catalogo = new();
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly PessoaNoToken Betina = new(new Identidade(Emissor, "betina.neves"), "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local", true);
    private static readonly PessoaNoToken Leandro = new(new Identidade(Emissor, "leandro.viana"), "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local", true);
    private static readonly VersaoDoTermo Vigente = TermosDeTeste.Publicada();

    [Fact(DisplayName = "Sem acréscimo, o pedido no gestor troca de versão e segue sem aviso")]
    public async Task SemAcrescimo_PedidoNoGestor_SoTrocaAVersao()
    {
        Perfil perfil = PerfilNaVersao1(A, B);
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: true);
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [A], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Reavaliacao(pedido).ReavaliarAsync(perfil, troca, CancellationToken.None);

        avisos.Should().BeEmpty();
        pedido.VersaoId.Should().Be(troca.Nova.Id);
        pedido.TrocasDeVersao.Single().Desfecho.Should().Be(DesfechoDaTrocaDeVersao.VersaoAtualizada);
    }

    [Fact(DisplayName = "Com acréscimo, o pedido é devolvido e as duas partes são avisadas")]
    public async Task ComAcrescimo_PedidoDevolvido_AvisaAsDuasPartes()
    {
        Perfil perfil = PerfilNaVersao1(A);
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: false);
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [A, B], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Reavaliacao(pedido).ReavaliarAsync(perfil, troca, CancellationToken.None);

        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.Devolvido);
        avisos.Select(a => a.Destinatario).Should().BeEquivalentTo([Betina.Email, Leandro.Email]);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido na proteção de dados volta ao gestor e avisa a fila dele")]
    public async Task SemAcrescimo_PedidoNaProtecaoDeDados_VoltaAoGestorEAvisa()
    {
        Perfil perfil = PerfilNaVersao1(A, B);
        PedidoDeConcessao pedido = PedidoNaProtecaoDeDados(perfil);
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [A], Catalogo)[^1];
        TitularComEmail gestor = new(new Identidade(Emissor, "isabella.figueiredo"), "isabellapriscilafigueiredo@uniplus-test.local");

        IReadOnlyList<EnviarEmail> avisos = await Reavaliacao(pedido, [gestor]).ReavaliarAsync(perfil, troca, CancellationToken.None);

        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        avisos.Should().ContainSingle().Which.Finalidade.Should().Be("pedido.fila_do_gestor");
    }

    [Fact(DisplayName = "O pedido de outro perfil não é reavaliado, e sem pedidos ativos não há aviso")]
    public async Task PedidoDeOutroPerfil_NaoEReavaliado()
    {
        Perfil perfil = PerfilNaVersao1(A);
        PedidoDeConcessao doOutro = Pedido(PerfilNaVersao1(B), paraSiMesmo: true);
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [A, B], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Reavaliacao(doOutro).ReavaliarAsync(perfil, troca, CancellationToken.None);

        avisos.Should().BeEmpty();
        doOutro.TrocasDeVersao.Should().BeEmpty();
    }

    private static ReavaliacaoDosPedidosNaTroca Reavaliacao(PedidoDeConcessao pedido, TitularComEmail[]? titulares = null) =>
        new(
            new PedidosAtivosNaMemoria([pedido]),
            new AvisosDoPedido(new ConcessoesNaMemoria(titulares: titulares ?? []), new SemUnidade(), new LinkDeTeste(), TimeProvider.System),
            CenarioDaGovernancaDaVersao.AvisoDeDevolucao(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria()),
            TimeProvider.System);

    private static Perfil PerfilNaVersao1(params string[] permissoes) =>
        PerfisDeTeste.Vigente($"Perfil {Guid.NewGuid():N}", "Perfil de teste.", permissoes, Catalogo);

    private static PedidoDeConcessao Pedido(Perfil perfil, bool paraSiMesmo)
    {
        PedidoDeConcessao pedido = CriacaoDoPedido.Criar(
            new PartesDoPedido(Betina, paraSiMesmo ? null : Leandro.Email), perfil, EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), new DominiosDeTeste(), Catalogo, DateTimeOffset.UtcNow).Value!;
        pedido.FixarTermo(new TermoNaCriacao(Vigente, Vigente.Id, true), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        if (!paraSiMesmo)
        {
            pedido.Aceitar(Leandro, pedido.Termo?.VersaoId, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }

        return pedido;
    }

    private static PedidoDeConcessao PedidoNaProtecaoDeDados(Perfil perfil)
    {
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: true);
        PerfilAvaliado sensivel = new([A, B], "SENSIVEL", true, null);
        pedido.Aprovar(Diogo, sensivel, new ContextoDaAprovacao(false), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        return pedido;
    }
}
