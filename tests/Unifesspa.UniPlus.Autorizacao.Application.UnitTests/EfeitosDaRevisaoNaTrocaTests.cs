namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>O que a entrada em vigor de uma versão faz à revisão das concessões: materializa a vencida e abre a da versão que acrescenta.</summary>
public sealed class EfeitosDaRevisaoNaTrocaTests
{
    private const string Consultar = "geral:pedidos:consultar";
    private const string Analisar = "geral:pedidos:analisar";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly DateTimeOffset Abertura = PerfisDeTeste.Instante.AddHours(3);

    private readonly IRevisaoDasConcessoesRepository _revisoes = Substitute.For<IRevisaoDasConcessoesRepository>();
    private readonly Perfil _perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", [Consultar], Catalogo);
    private RevisaoDasConcessoesDoPerfil? _adicionada;

    [Fact(DisplayName = "Sem revisão não encerrada, nada se materializa e as concessões nem são lidas")]
    public async Task MaterializarSeVencida_SemRevisao_DevolveVazioSemLerAsConcessoes()
    {
        RevisaoAposAMaterializacao resultado = await Efeitos(Abertura).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeNull();
        resultado.Avisos.Should().BeEmpty();
        await _revisoes.DidNotReceiveWithAnyArgs().ObterConcessoesDaRevisaoAsync(default, default, default);
    }

    [Fact(DisplayName = "A revisão efetivamente aberta segue aberta e não é materializada")]
    public async Task MaterializarSeVencida_RevisaoAberta_SegueAberta()
    {
        (RevisaoDasConcessoesDoPerfil revisao, _) = RevisaoComUmaPendencia();

        RevisaoAposAMaterializacao resultado = await Efeitos(Abertura.AddHours(2)).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeSameAs(revisao);
        resultado.Avisos.Should().BeEmpty();
        revisao.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A revisão com o prazo vencido é materializada e não segue aberta")]
    public async Task MaterializarSeVencida_PrazoVencido_MaterializaOFim()
    {
        (RevisaoDasConcessoesDoPerfil revisao, _) = RevisaoComUmaPendencia();

        RevisaoAposAMaterializacao resultado = await Efeitos(revisao.TerminaEm.AddHours(1)).MaterializarSeVencidaAsync(_perfil, CancellationToken.None);

        resultado.Aberta.Should().BeNull();
        revisao.Encerrada.Should().BeTrue();
        revisao.Forma.Should().Be(FormaDoEncerramento.Prazo);
    }

    [Fact(DisplayName = "A abertura só conta a concessão ativa fora da versão nova e abre a revisão dela")]
    public async Task Abrir_ConcessoesVariadas_AbreSoComAsPendentes()
    {
        TrocaDeVigencia troca = ConcessoesDoPerfilAntesDaTroca(out Concessao[] concessoes);

        AberturaDaRevisaoDasConcessoes abertura = await Efeitos(Abertura).AbrirAsync(_perfil, troca, Abertura, CancellationToken.None);

        abertura.Abriu.Should().BeTrue();
        _adicionada!.PerfilId.Should().Be(_perfil.Id);
        _adicionada.VersaoAlvoId.Should().Be(troca.Nova.Id);
        _adicionada.Pendencias(concessoes, Abertura).Should().Be(1);
    }

    [Fact(DisplayName = "Sem concessão ativa fora da versão nova, a versão só vigora e nenhuma revisão se registra")]
    public async Task Abrir_SemPendentes_NaoAbreNemRegistra()
    {
        TrocaDeVigencia troca = ConcessoesDoPerfilAntesDaTroca(out Concessao[] concessoes);
        Concessao[] semPendentes = [.. concessoes.Skip(1)];
        _revisoes.ObterConcessoesDaRevisaoAsync(_perfil.Id, Abertura, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>(semPendentes));

        AberturaDaRevisaoDasConcessoes abertura = await Efeitos(Abertura).AbrirAsync(_perfil, troca, Abertura, CancellationToken.None);

        abertura.Abriu.Should().BeFalse();
        abertura.Avisos.Should().BeEmpty();
        await _revisoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    private EfeitosDaRevisaoNaTroca Efeitos(DateTimeOffset agora)
    {
        _revisoes.AdicionarAsync(Arg.Do<RevisaoDasConcessoesDoPerfil>(revisao => _adicionada = revisao), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        RegistroDeAvisosNaMemoria registro = new();
        EmissorDeAvisos emissor = new(registro, MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance);
        AvisosDaRevisao avisos = new(emissor, new GruposDaGovernanca(Substitute.For<ITitularesDaPermissao>()), new LinkDeTeste(), new CatalogoDaAplicacao([]));

        return new EfeitosDaRevisaoNaTroca(_revisoes, new LeitorDaMigracao(_revisoes, Catalogo, new RelogioFixo(agora)), avisos, AvisosDosEfeitos(registro));
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
        _revisoes.ObterConcessoesDaRevisaoAsync(_perfil.Id, Abertura, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>(concessoes));

        return troca;
    }

    // A revisão da versão nova, aberta, com a concessão de Leandro pendente de migrar.
    private (RevisaoDasConcessoesDoPerfil Revisao, Concessao Concessao) RevisaoComUmaPendencia()
    {
        Concessao concessao = ConcessaoDe("leandro.viana");
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(_perfil, [Consultar, Analisar], Catalogo)[^1];
        RevisaoDasConcessoesDoPerfil revisao = RevisaoDasConcessoesDoPerfil.Abrir(_perfil.Id, troca.Nova, Abertura);
        _revisoes.ObterNaoEncerradaParaAlterarAsync(_perfil.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<RevisaoDasConcessoesDoPerfil?>(revisao));
        _revisoes.ObterConcessoesDaRevisaoAsync(_perfil.Id, revisao.AbertaEm, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Concessao>>([concessao]));
        _revisoes.ObterComposicoesAsync(default!, default, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>> { [concessao.Id] = [Consultar] }));

        return (revisao, concessao);
    }

    private Concessao ConcessaoDe(string contaId, DateTimeOffset? validaAte = null) =>
        ConcessaoBuilder.DoPerfil(_perfil, Catalogo)
            .Para(new Identidade("https://idp.test/realms/unifesspa", contaId))
            .NaInstituicao()
            .ValidaAte(validaAte)
            .Construir();
}
