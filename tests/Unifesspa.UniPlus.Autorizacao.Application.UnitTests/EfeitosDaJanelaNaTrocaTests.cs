namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>O que a entrada em vigor de uma versão faz à janela de migração: materializa a vencida e abre a da versão que acrescenta.</summary>
public sealed class EfeitosDaJanelaNaTrocaTests
{
    private const string Consultar = "geral:pedidos:consultar";
    private const string Analisar = "geral:pedidos:analisar";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly DateTimeOffset Abertura = PerfisDeTeste.Instante.AddHours(3);

    private readonly IJanelaDeMigracaoRepository _janelas = Substitute.For<IJanelaDeMigracaoRepository>();
    private readonly Perfil _perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", [Consultar], Catalogo);
    private JanelaDeMigracao? _adicionada;

    [Fact(DisplayName = "Sem janela não encerrada, nada se materializa e as concessões nem são lidas")]
    public async Task MaterializarSeVencida_SemJanela_DevolveVazioSemLerAsConcessoes()
    {
        JanelaAposAMaterializacao resultado = await Efeitos(Abertura).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeNull();
        resultado.Avisos.Should().BeEmpty();
        await _janelas.DidNotReceiveWithAnyArgs().ObterConcessoesDaJanelaAsync(default, default, default);
    }

    [Fact(DisplayName = "A janela efetivamente aberta segue aberta e não é materializada")]
    public async Task MaterializarSeVencida_JanelaAberta_SegueAberta()
    {
        (JanelaDeMigracao janela, _) = JanelaComUmaPendencia();

        JanelaAposAMaterializacao resultado = await Efeitos(Abertura.AddHours(2)).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeSameAs(janela);
        resultado.Avisos.Should().BeEmpty();
        janela.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A janela com o prazo vencido é materializada e não segue aberta")]
    public async Task MaterializarSeVencida_PrazoVencido_MaterializaOFim()
    {
        (JanelaDeMigracao janela, _) = JanelaComUmaPendencia();

        JanelaAposAMaterializacao resultado = await Efeitos(janela.Prazo.AddHours(1)).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeNull();
        janela.Encerrada.Should().BeTrue();
        janela.Forma.Should().Be(FormaDoEncerramento.Prazo);
    }

    [Fact(DisplayName = "A abertura só conta a concessão ativa fora da versão nova e abre a janela dela")]
    public async Task Abrir_ConcessoesVariadas_AbreSoComAsPendentes()
    {
        TrocaDeVigencia troca = ConcessoesDoPerfilAntesDaTroca(out Concessao[] concessoes);

        AberturaDaJanela abertura = await Efeitos(Abertura).AbrirAsync(_perfil, troca, Abertura, CancellationToken.None);

        abertura.Abriu.Should().BeTrue();
        _adicionada!.PerfilId.Should().Be(_perfil.Id);
        _adicionada.VersaoAlvoId.Should().Be(troca.Nova.Id);
        _adicionada.Pendencias(concessoes, Abertura).Should().Be(1);
    }

    [Fact(DisplayName = "Sem concessão ativa fora da versão nova, a versão só vigora e nenhuma janela se registra")]
    public async Task Abrir_SemPendentes_NaoAbreNemRegistra()
    {
        TrocaDeVigencia troca = ConcessoesDoPerfilAntesDaTroca(out Concessao[] concessoes);
        Concessao[] semPendentes = [.. concessoes.Skip(1)];
        _janelas.ObterConcessoesDaJanelaAsync(_perfil.Id, Abertura, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>(semPendentes));

        AberturaDaJanela abertura = await Efeitos(Abertura).AbrirAsync(_perfil, troca, Abertura, CancellationToken.None);

        abertura.Abriu.Should().BeFalse();
        abertura.Avisos.Should().BeEmpty();
        await _janelas.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    private EfeitosDaJanelaNaTroca Efeitos(DateTimeOffset agora)
    {
        _janelas.AdicionarAsync(Arg.Do<JanelaDeMigracao>(janela => _adicionada = janela), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        RegistroDeAvisosNaMemoria registro = new();
        EmissorDeAvisos emissor = new(registro, MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance);
        AvisosDaJanela avisos = new(emissor, new GruposDaGovernanca(Substitute.For<ITitularesDaPermissao>()), new LinkDeTeste(), new CatalogoDaAplicacao([]));

        return new EfeitosDaJanelaNaTroca(_janelas, new LeitorDaMigracao(_janelas, Catalogo, new RelogioFixo(agora)), avisos, AvisosDosEfeitos(registro));
    }

    // A pendente, a que já está na versão nova, a revogada e a vencida: só a primeira é pendente de migrar.
    private TrocaDeVigencia ConcessoesDoPerfilAntesDaTroca(out Concessao[] concessoes)
    {
        Concessao pendente = ConcessaoDe("leandro.viana");
        Concessao naVersaoNova = ConcessaoDe("isabella.figueiredo");
        Concessao revogada = ConcessaoDe("marlene.campos");
        Concessao vencida = ConcessaoDe("betina.neves", PerfisDeTeste.Instante.AddHours(2));
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];
        naVersaoNova.MigrarParaVersao(troca.Anterior!, troca.Nova);
        revogada.Revogar(new AtoDoAgente(Diogo, PerfisDeTeste.Instante.AddMinutes(30)), "Teste de revogação.").IsSuccess.Should().BeTrue();
        concessoes = [pendente, naVersaoNova, revogada, vencida];
        _janelas.ObterConcessoesDaJanelaAsync(_perfil.Id, Abertura, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>(concessoes));

        return troca;
    }

    // A janela da versão nova, aberta, com a concessão de Leandro pendente de migrar.
    private (JanelaDeMigracao Janela, Concessao Concessao) JanelaComUmaPendencia()
    {
        Concessao concessao = ConcessaoDe("leandro.viana");
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];
        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(_perfil.Id, troca.Nova, Abertura);
        _janelas.ObterNaoEncerradaParaAlterarAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<JanelaDeMigracao?>(janela));
        _janelas.ObterConcessoesDaJanelaAsync(_perfil.Id, janela.AbertaEm, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>([concessao]));
        _janelas.ObterComposicoesAsync(default!, default, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>> { [concessao.Id] = [Consultar] }));

        return (janela, concessao);
    }

    private Concessao ConcessaoDe(string contaId, DateTimeOffset? validaAte = null) =>
        ConcessaoBuilder.DoPerfil(_perfil, Catalogo)
            .Para(new Identidade("https://idp.test/realms/unifesspa", contaId))
            .NaInstituicao()
            .ValidaAte(validaAte)
            .Construir();
}
