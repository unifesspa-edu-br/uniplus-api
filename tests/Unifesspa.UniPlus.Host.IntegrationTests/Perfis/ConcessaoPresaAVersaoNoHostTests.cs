namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O que a concessão dá quando o perfil muda de versão: a interseção do que a versão dela reúne com tudo o que vigorou
/// depois, lida no banco pelo mesmo ponto que a decisão e os titulares usam.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConcessaoPresaAVersaoNoHostTests : PedidosDeConcessaoTestBase, IAsyncLifetime
{
    private const string A = "teste:inscricoes:operar";
    private const string B = "teste:atendimento:analisar";
    private const string C = "teste:laudos:consultar";
    private const string Catalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string Motivos = "selecao:motivos-de-decisao:manter";

    public ConcessaoPresaAVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public Task InitializeAsync() => ResetarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Acréscimo numa versão posterior não chega à concessão da versão antiga")]
    public async Task AcrescimoPosterior_NaoChegaAConcessaoAntiga()
    {
        Guid perfil = await PerfilComConcessaoAsync([A]);

        await TrocarComposicaoAsync(perfil, A, B);

        (await PermissoesDaConcessaoAsync(perfil)).Should().BeEquivalentTo([A]);
    }

    [Fact(DisplayName = "Permissão removida na versão vigente deixa de valer para a concessão da versão antiga")]
    public async Task PermissaoRemovida_DeixaDeValer()
    {
        Guid perfil = await PerfilComConcessaoAsync([A, B]);

        await TrocarComposicaoAsync(perfil, A);

        (await PermissoesDaConcessaoAsync(perfil)).Should().BeEquivalentTo([A]);
    }

    [Fact(DisplayName = "Permissão removida e recolocada em versão posterior não volta para a concessão da versão antiga")]
    public async Task PermissaoRemovidaERecolocada_NaoVolta()
    {
        Guid perfil = await PerfilComConcessaoAsync([A, B]);

        await TrocarComposicaoAsync(perfil, A, B, C);
        await DescartarJanelasAsync();
        await TrocarComposicaoAsync(perfil, A, C);
        await TrocarComposicaoAsync(perfil, A, B, C);

        (await PermissoesDaConcessaoAsync(perfil)).Should().BeEquivalentTo([A]);
    }

    [Fact(DisplayName = "Concessão feita na versão vigente dá o que ela reúne, inclusive o que a versão anterior não tinha")]
    public async Task ConcessaoNovaNaVersaoVigente_DaAComposicaoDela()
    {
        Guid perfil = await SemearPerfilAsync("Versão corrente", A);
        await TrocarComposicaoAsync(perfil, A, B);

        await ConcederAsync(perfil);

        (await PermissoesDaConcessaoAsync(perfil)).Should().BeEquivalentTo([A, B]);
    }

    [Fact(DisplayName = "Concessão cuja interseção é vazia continua listada, sem permissão")]
    public async Task InterseccaoVazia_ContinuaListada()
    {
        Guid perfil = await PerfilComConcessaoAsync([A, B]);

        await TrocarComposicaoAsync(perfil, C);

        ConcessaoAtiva concessao = (await ListarAsync()).Single(c => c.PerfilId == perfil);
        concessao.Permissoes.Should().BeEmpty();
        concessao.VersaoId.Should().NotBeNull();
    }

    [Fact(DisplayName = "Perfil sem vigência aberta e concessão sem versão não dão permissão alguma e seguem listados")]
    public async Task SemVigenciaOuSemVersao_NaoDaPermissao()
    {
        Guid semVigencia = await PerfilComConcessaoAsync([A]);
        Guid semVersao = await PerfilComConcessaoAsync([A]);
        await ExecutarSqlAsync("UPDATE autorizacao.vigencias_do_perfil SET ate = now() WHERE perfil_id = {0}", semVigencia);
        await ExecutarSqlAsync("UPDATE autorizacao.concessoes SET versao_id = NULL WHERE perfil_id = {0}", semVersao);

        IReadOnlyList<ConcessaoAtiva> concessoes = await ListarAsync();

        concessoes.Single(c => c.PerfilId == semVigencia).Permissoes.Should().BeEmpty();
        concessoes.Single(c => c.PerfilId == semVersao).Permissoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "A concessão de governança dá as permissões do perfil e não tem versão")]
    public async Task Governanca_DaAsPermissoesDoPerfilSemVersao()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.GestorDeAcessos);

        ConcessaoAtiva concessao = (await ListarAsync()).Single(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId);

        concessao.VersaoId.Should().BeNull();
        concessao.Permissoes.Should().Contain(AprovarFuncional);
    }

    [Fact(DisplayName = "A concessão efetiva leva a versão do perfil, e o titular da permissão removida deixa de ser avisado")]
    public async Task ConcessaoEfetivaELevaAVersaoETitularesSeguemARegra()
    {
        Guid perfil = await PerfilComConcessaoAsync([Catalogo, Motivos], institucional: true);
        await TrocarComposicaoAsync(perfil, Catalogo);

        ConcessaoAtiva concessao = (await ListarAsync()).Single(c => c.PerfilId == perfil);
        EffectiveGrant grant = EffectiveGrant.DaConcessao(Catalogo, concessao).Value!;

        grant.VersaoId.Should().Be(concessao.VersaoId);
        grant.Fonte.Should().Be(FonteGrant.Concessao);
        (await TitularesAsync(Catalogo)).Should().Contain(EmailDe(perfil));
        (await TitularesAsync(Motivos)).Should().NotContain(EmailDe(perfil));
    }

    private static string EmailDe(Guid perfil) => $"{perfil:N}@uniplus-test.local";

    private async Task<Guid> PerfilComConcessaoAsync(string[] permissoes, bool institucional = false)
    {
        Guid perfil = await SemearPerfilAsync("Concessão presa", permissoes);
        await ConcederAsync(perfil, institucional);

        return perfil;
    }

    private Task<Guid> ConcederAsync(Guid perfil, bool institucional = false) =>
        ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services,
            Leandro.Sub.ToString(),
            perfil,
            institucional ? EscopoDaConcessao.DaInstituicao() : EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()),
            email: EmailDe(perfil));

    private async Task<IReadOnlyList<ConcessaoAtiva>> ListarAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(TestAuthHandler.DefaultIssuer, Leandro.Sub.ToString()).Value!, CancellationToken.None);
    }

    private async Task<string[]> PermissoesDaConcessaoAsync(Guid perfil) =>
        [.. (await ListarAsync()).Single(c => c.PerfilId == perfil).Permissoes];

    private async Task<IReadOnlyList<string>> TitularesAsync(string permissao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ITitularesDaPermissao>().ListarEmailsAsync(permissao);
    }
}
