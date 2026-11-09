namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// O que a entrada em vigor de uma versão faz às concessões da versão que saiu e aos pedidos ativos do perfil, e quem é
/// avisado. Só personas fictícias do portal.
/// </summary>
public sealed class EfeitosDaTrocaDeVigenciaTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";
    private const string A = "geral:pedidos:consultar";
    private const string B = "geral:pedidos:analisar";
    private const string C = "geral:pedidos:exportar";

    private static readonly CatalogoLivre Catalogo = new();
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly PessoaNoToken Betina = new(new Identidade(Emissor, "betina.neves"), "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local", true);
    private static readonly PessoaNoToken Leandro = new(new Identidade(Emissor, "leandro.viana"), "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local", true);

    [Fact(DisplayName = "Sem acréscimo, as concessões da versão que saiu passam para a nova")]
    public async Task SemAcrescimo_MigraAsConcessoes()
    {
        Perfil perfil = PerfilNaVersao1(A, B);
        Concessao concessao = Conceder(perfil);
        TrocaDeVigencia troca = Trocar(perfil, A);

        await Efeitos(concessoes: [concessao]).AplicarAsync(perfil, troca, CancellationToken.None);

        concessao.VersaoId.Should().Be(troca.Nova.Id);
    }

    [Fact(DisplayName = "Com acréscimo, as concessões ficam na versão em que estão")]
    public async Task ComAcrescimo_NaoMigra()
    {
        Perfil perfil = PerfilNaVersao1(A);
        Concessao concessao = Conceder(perfil);
        Guid versaoAntiga = concessao.VersaoId!.Value;
        TrocaDeVigencia troca = Trocar(perfil, A, B);

        await Efeitos(concessoes: [concessao]).AplicarAsync(perfil, troca, CancellationToken.None);

        concessao.VersaoId.Should().Be(versaoAntiga);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido no gestor só passa para a versão nova, sem aviso")]
    public async Task SemAcrescimo_PedidoNoGestor_SoTrocaAVersao()
    {
        Perfil perfil = PerfilNaVersao1(A, B);
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: true);
        TrocaDeVigencia troca = Trocar(perfil, A);

        IReadOnlyList<EnviarEmail> avisos = await Efeitos(pedidos: [pedido]).AplicarAsync(perfil, troca, CancellationToken.None);

        avisos.Should().BeEmpty();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.VersaoId.Should().Be(troca.Nova.Id);
        pedido.TrocasDeVersao.Should().ContainSingle().Which.Desfecho.Should().Be(DesfechoDaTrocaDeVersao.VersaoAtualizada);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido na proteção de dados volta ao gestor e avisa a fila dele")]
    public async Task SemAcrescimo_PedidoNaProtecaoDeDados_VoltaAoGestorEAvisa()
    {
        Perfil perfil = PerfilNaVersao1(A, B);
        PedidoDeConcessao pedido = PedidoNaProtecaoDeDados(perfil);
        TrocaDeVigencia troca = Trocar(perfil, A);
        TitularComEmail gestor = new(new Identidade(Emissor, "isabella.figueiredo"), "isabellapriscilafigueiredo@uniplus-test.local");

        IReadOnlyList<EnviarEmail> avisos = await Efeitos(pedidos: [pedido], titulares: [gestor]).AplicarAsync(perfil, troca, CancellationToken.None);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.ValidaAte.Should().BeNull();
        pedido.TrocasDeVersao.Should().ContainSingle().Which.Desfecho.Should().Be(DesfechoDaTrocaDeVersao.VoltouAoGestor);
        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Finalidade.Should().Be("pedido.fila_do_gestor");
        aviso.Destinatario.Should().Be(gestor.Email);
    }

    [Fact(DisplayName = "Com acréscimo, o pedido é devolvido e o aviso vai ao solicitante e ao beneficiário, sem dado pessoal nem prazo")]
    public async Task ComAcrescimo_PedidoDevolvido_AvisaAsDuasPartes()
    {
        Perfil perfil = PerfilNaVersao1(A);
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: false);
        TrocaDeVigencia troca = Trocar(perfil, A, B);

        IReadOnlyList<EnviarEmail> avisos = await Efeitos(pedidos: [pedido]).AplicarAsync(perfil, troca, CancellationToken.None);

        pedido.Situacao.Should().Be(SituacaoDoPedido.Devolvido);
        pedido.EncerradoEm.Should().NotBeNull();
        avisos.Should().HaveCount(2).And.OnlyContain(a => a.Finalidade == "pedido.devolvido");
        avisos.Select(a => a.Destinatario).Should().BeEquivalentTo([Betina.Email, Leandro.Email]);
        foreach (EnviarEmail aviso in avisos)
        {
            aviso.CorpoTexto.Should().Contain("Faça o pedido de novo depois que o perfil for atualizado")
                .And.NotContain(Betina.Nome!).And.NotContain(Leandro.Nome!).And.NotContain(Justificativa).And.NotContain("janela");
        }
    }

    [Fact(DisplayName = "O pedido de outro perfil e o pedido já encerrado ficam como estão")]
    public async Task PedidoDeOutroPerfilEEncerrado_FicamInalterados()
    {
        Perfil perfil = PerfilNaVersao1(A);
        Perfil outro = PerfilNaVersao1(C);
        PedidoDeConcessao doOutro = Pedido(outro, paraSiMesmo: true);
        PedidoDeConcessao cancelado = Pedido(perfil, paraSiMesmo: true);
        cancelado.Cancelar(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        TrocaDeVigencia troca = Trocar(perfil, A, B);

        IReadOnlyList<EnviarEmail> avisos = await Efeitos(pedidos: [doOutro, cancelado]).AplicarAsync(perfil, troca, CancellationToken.None);

        avisos.Should().BeEmpty();
        doOutro.TrocasDeVersao.Should().BeEmpty();
        cancelado.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
        cancelado.TrocasDeVersao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A primeira vigência do perfil não tem efeitos a aplicar")]
    public async Task PrimeiraVigencia_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Perfil novo", "Perfil de teste.", [A], Catalogo);
        TrocaDeVigencia troca = PerfisDeTeste.EntrarEmVigor(perfil, Catalogo).Troca!;

        Func<Task> aplicar = () => Efeitos().AplicarAsync(perfil, troca, CancellationToken.None);

        await aplicar.Should().ThrowAsync<ArgumentException>();
    }

    private static EfeitosDaTrocaDeVigencia Efeitos(
        Concessao[]? concessoes = null, PedidoDeConcessao[]? pedidos = null, TitularComEmail[]? titulares = null) =>
        new(
            new ConcessoesNaMemoria(paraMigrar: concessoes ?? [], titulares: titulares ?? []),
            new PedidosAtivosNaMemoria(pedidos ?? []),
            new AvisosDoPedido(new ConcessoesNaMemoria(titulares: titulares ?? []), new SemUnidade(), new LinkDeTeste(), TimeProvider.System),
            CenarioDaGovernancaDaVersao.JanelaDaTroca(new JanelasNaMemoria(), new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), TimeProvider.System),
            TimeProvider.System);

    private static Perfil PerfilNaVersao1(params string[] permissoes) =>
        PerfisDeTeste.Vigente($"Perfil {Guid.NewGuid():N}", "Perfil de teste.", permissoes, Catalogo);

    // A proposta não retira permissão: a troca que só reduz o conjunto passa pela remoção, que vale na hora.
    private static TrocaDeVigencia Trocar(Perfil perfil, params string[] permissoes) =>
        PerfisDeTeste.Trocar(perfil, permissoes, Catalogo)[^1];

    private static Concessao Conceder(Perfil perfil) =>
        Concessao.Conceder(
            Leandro.Identidade,
            perfil,
            EscopoDaConcessao.DaInstituicao(),
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            Diogo,
            DateTimeOffset.UtcNow,
            Catalogo).Value!;

    private static PedidoDeConcessao Pedido(Perfil perfil, bool paraSiMesmo)
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina,
            paraSiMesmo ? null : Leandro.Email,
            perfil,
            EscopoDaConcessao.DaInstituicao(),
            Justificativa,
            new DominiosQualquer(),
            Catalogo,
            DateTimeOffset.UtcNow).Value!;
        if (!paraSiMesmo)
        {
            pedido.Aceitar(Leandro, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }

        return pedido;
    }

    private static PedidoDeConcessao PedidoNaProtecaoDeDados(Perfil perfil)
    {
        PedidoDeConcessao pedido = Pedido(perfil, paraSiMesmo: true);
        PerfilAvaliado sensivel = new([A, B], "sensivel", true, null);
        pedido.Aprovar(Diogo, sensivel, new ContextoDaAprovacao(false), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);

        return pedido;
    }

    private sealed class DominiosQualquer : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => true;
    }
}
