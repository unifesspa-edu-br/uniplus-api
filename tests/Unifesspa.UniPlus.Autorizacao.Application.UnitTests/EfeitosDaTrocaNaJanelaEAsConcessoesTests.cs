namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// Os efeitos da troca de vigência sobre as concessões e a janela, na ordem em que acontecem: a versão com acréscimo abre a
/// janela ou encerra a anterior; a sem acréscimo leva as concessões e redireciona a janela aberta ou encerra a anterior.
/// </summary>
public sealed class EfeitosDaTrocaNaJanelaEAsConcessoesTests
{
    private const string Consultar = "geral:pedidos:consultar";
    private const string Analisar = "geral:pedidos:analisar";
    private const string Emissor = "https://idp.test/realms/unifesspa";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly Identidade Autor = PerfisDeTeste.Autor;
    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante.AddHours(3);
    private static readonly TitularDaPermissao Gestor = new(Emissor, "isabella.figueiredo", "isabellapriscilafigueiredo@uniplus-test.local");

    private readonly IJanelaDeMigracaoRepository _janelas = Substitute.For<IJanelaDeMigracaoRepository>();
    private readonly Perfil _perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", [Consultar], Catalogo);
    private readonly List<JanelaDeMigracao> _abertas = [];

    [Fact(DisplayName = "A versão com acréscimo e concessão a migrar abre a janela, deixa as concessões onde estão e não encerra a anterior")]
    public async Task ComAcrescimo_ComConcessaoAMigrar_AbreAJanela()
    {
        Concessao concessao = Concessao(_perfil, "leandro.viana");
        Guid versaoAntiga = concessao.VersaoId!.Value;
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];
        _janelas.ObterConcessoesDaJanelaAsync(_perfil.Id, Agora, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>([concessao]));

        IReadOnlyList<EnviarEmail> avisos = await Efeitos([concessao]).AplicarAsync(_perfil, troca, Autor, CancellationToken.None);

        _abertas.Should().ContainSingle().Which.VersaoAlvoId.Should().Be(troca.Nova.Id);
        concessao.VersaoId.Should().Be(versaoAntiga);
        avisos.Should().NotContain(a => a.Finalidade == FinalidadeDaVersaoEncerrada);
    }

    [Fact(DisplayName = "A versão com acréscimo sem concessão a migrar não abre janela e encerra a anterior com o aviso de sem concessão")]
    public async Task ComAcrescimo_SemConcessaoAMigrar_EncerraAAnterior()
    {
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Efeitos([]).AplicarAsync(_perfil, troca, Autor, CancellationToken.None);

        _abertas.Should().BeEmpty();
        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Finalidade.Should().Be(FinalidadeDaVersaoEncerrada);
        aviso.Destinatario.Should().Be(Gestor.Email);
        aviso.CorpoTexto.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "A versão sem acréscimo e sem janela leva as concessões e encerra a anterior com o aviso de versão substituída")]
    public async Task SemAcrescimo_SemJanela_MigraEEncerraAAnterior()
    {
        Perfil perfil = PerfilDuplo();
        Concessao concessao = Concessao(perfil, "leandro.viana");
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [Consultar], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Efeitos([concessao]).AplicarAsync(perfil, troca, Autor, CancellationToken.None);

        concessao.VersaoId.Should().Be(troca.Nova.Id);
        _abertas.Should().BeEmpty();
        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Finalidade.Should().Be(FinalidadeDaVersaoEncerrada);
    }

    [Fact(DisplayName = "Os dois encerramentos da versão anterior dizem coisas diferentes: sem concessão a migrar e versão substituída")]
    public async Task OsDoisEncerramentos_TemTextosDiferentes()
    {
        TrocaDeVigencia comAcrescimo = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];
        EnviarEmail semConcessao = (await Efeitos([]).AplicarAsync(_perfil, comAcrescimo, Autor, CancellationToken.None)).Single();
        Perfil outro = PerfilDuplo();
        TrocaDeVigencia semAcrescimo = PerfisDeTeste.Trocar(outro, [Consultar], Catalogo)[^1];

        EnviarEmail substituida = (await Efeitos([]).AplicarAsync(outro, semAcrescimo, Autor, CancellationToken.None)).Single();

        semConcessao.CorpoTexto.Should().Contain("sem nenhuma concessão a migrar");
        substituida.CorpoTexto.Should().Contain("não exige migração");
    }

    [Fact(DisplayName = "A versão sem acréscimo com janela aberta leva as concessões, redireciona o alvo da janela e não avisa")]
    public async Task SemAcrescimo_ComJanelaAberta_RedirecionaSemAviso()
    {
        Perfil perfil = PerfilDuplo();
        Concessao concessao = Concessao(perfil, "leandro.viana");
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, [Consultar], Catalogo)[^1];
        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, troca.Nova, Agora);
        _janelas.ObterNaoEncerradaParaAlterarAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<JanelaDeMigracao?>(janela));
        _janelas.ObterConcessoesDaJanelaAsync(perfil.Id, janela.AbertaEm, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>([concessao]));
        _janelas.ObterComposicoesAsync(default!, default, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>> { [concessao.Id] = [Consultar] }));

        IReadOnlyList<EnviarEmail> avisos = await Efeitos([concessao]).AplicarAsync(perfil, troca, Autor, CancellationToken.None);

        concessao.VersaoId.Should().Be(troca.Nova.Id);
        janela.VersaoAlvoId.Should().Be(troca.Nova.Id);
        avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os avisos saem na ordem da troca e depois dos pedidos")]
    public async Task Avisos_DaTrocaAntesDosPedidos()
    {
        PedidoDeConcessao pedido = PedidoDeTeste(_perfil);
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];

        IReadOnlyList<EnviarEmail> avisos = await Efeitos([], [pedido]).AplicarAsync(_perfil, troca, Autor, CancellationToken.None);

        pedido.VersaoId.Should().NotBe(troca.Nova.Id, "o pedido é da versão anterior, e a reavaliação o devolve");

        avisos.Select(a => a.Finalidade).Should().Equal(
            FinalidadeDaVersaoEncerrada, "perfil.pedido_devolvido_pela_janela", "perfil.pedido_devolvido_pela_janela");
    }

    private const string FinalidadeDaVersaoEncerrada = "perfil.versao_encerrada";

    private EfeitosDaTrocaDeVigencia Efeitos(Concessao[] paraMigrar, PedidoDeConcessao[]? pedidos = null)
    {
        RegistroDeAvisosNaMemoria registro = new();

        return new EfeitosDaTrocaDeVigencia(
            new ConcessoesNaMemoria(paraMigrar: paraMigrar, titulares: []),
            JanelaDaTroca(registro),
            AvisosDosEfeitos(registro, [Gestor]),
            new ReavaliacaoDosPedidosNaTroca(
                new PedidosAtivosNaMemoria(pedidos ?? []),
                new AvisosDoPedido(new ConcessoesNaMemoria(titulares: []), new SemUnidade(), new LinkDeTeste(), TimeProvider.System),
                AvisoDeDevolucao(registro),
                TimeProvider.System),
            new RelogioFixo(Agora));
    }

    private EfeitosDaJanelaNaTroca JanelaDaTroca(RegistroDeAvisosNaMemoria registro)
    {
        EmissorDeAvisos emissor = new(registro, MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance);
        AvisosDaJanela avisos = new(emissor, new GruposDaGovernanca(new TitularesDaGovernanca()), new LinkDeTeste(), new CatalogoDaAplicacao([]));
        _janelas.AdicionarAsync(Arg.Do<JanelaDeMigracao>(_abertas.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return new EfeitosDaJanelaNaTroca(_janelas, new LeitorDaMigracao(_janelas, Catalogo, new RelogioFixo(Agora)), avisos, AvisosDosEfeitos(registro, [Gestor]));
    }

    private static Perfil PerfilDuplo() =>
        PerfisDeTeste.Vigente($"Perfil {Guid.NewGuid():N}", "Acompanha os processos.", [Consultar, Analisar], Catalogo);

    private static Concessao Concessao(Perfil perfil, string contaId) =>
        ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(new Identidade(Emissor, contaId)).NaInstituicao().Construir();

    private static PedidoDeConcessao PedidoDeTeste(Perfil perfil)
    {
        PessoaNoToken solicitante = new(new Identidade(Emissor, "betina.neves"), "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local", true);
        PedidoDeConcessao pedido = CriacaoDoPedido.Criar(
            new PartesDoPedido(solicitante, "leandro-viana84@uniplus-test.local"),
            perfil,
            EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido("A coordenação precisa analisar os pedidos de atendimento."),
            new DominiosDeTeste(),
            Catalogo,
            DateTimeOffset.UtcNow).Value!;
        VersaoDoTermo vigente = TermosDeTeste.Publicada();
        pedido.FixarTermo(new TermoNaCriacao(vigente, vigente.Id, true), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        return pedido;
    }
}
