namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Os perfis de governança não têm versão, aprovação nem janela, e a composição deles só muda pela carga do modelo. Reúnem
/// permissões do módulo de autorização e, de outros módulos, só as reservadas à governança: uma permissão comum de outro
/// módulo chegaria a todos os gestores, Representantes ou Encarregados sem passar pela aprovação que os perfis comuns
/// exigem. O perfil comum, por sua vez, nunca reúne permissão reservada.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ComposicaoDaGovernancaNoHostTests
{
    private const string ModuloDeAutorizacao = "autorizacao";
    private const string AprovacaoDaProtecaoDoCatalogo = "configuracao:protecao-do-catalogo:aprovar";
    private const string ConsultaDaProtecaoDoCatalogo = "configuracao:protecao-do-catalogo:consultar";
    private const string ManifestacaoNaProtecaoDoCatalogo = "configuracao:protecao-do-catalogo:manifestar";

    private static readonly Guid[] PerfisDeGovernanca =
        [PerfisDeSistema.GestorDeAcessosId, PerfisDeSistema.RepresentanteDoControladorId, PerfisDeSistema.EncarregadoDeDadosId];

    private readonly MonolitoDaInstalacaoFixture _fixture;

    public ComposicaoDaGovernancaNoHostTests(MonolitoDaInstalacaoFixture fixture)
    {
        _fixture = fixture;
    }

    private CatalogoDaAplicacao Catalogo => _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

    [Fact(DisplayName = "Os perfis de governança da carga reúnem permissões da autorização e, de outros módulos, só reservadas")]
    public async Task PerfisDeGovernanca_SoReunemAutorizacaoOuReservadas()
    {
        string[] codigos = [.. (await ComposicaoGravadaAsync()).Select(static p => p.Codigo)];

        codigos.Should().NotBeEmpty();
        codigos.Should().OnlyContain(codigo => ComposicaoDaGovernanca.Admite(Linha(codigo)));
        codigos.Should().Contain([AprovacaoDaProtecaoDoCatalogo, ConsultaDaProtecaoDoCatalogo, ManifestacaoNaProtecaoDoCatalogo]);
    }

    [Fact(DisplayName = "A composição gravada dos perfis de gestor, de Representante e de Encarregado é a esperada")]
    public async Task PerfisDeGovernanca_ComposicaoGravadaEAEsperada()
    {
        PermissaoDeGovernanca[] gravada = await ComposicaoGravadaAsync();

        foreach (Guid perfil in PerfisDeGovernanca)
        {
            gravada.Where(p => p.PerfilId == perfil).Select(static p => p.Codigo).Should().BeEquivalentTo(ComposicaoDaGovernanca.CodigosDe(perfil));
        }
    }

    [Fact(DisplayName = "Reservada de outro módulo: a de decisão só no Representante, as de consulta e manifestação só no Encarregado")]
    public void PermissoesReservadasDeOutroModulo_DecisaoNoRepresentanteConsultaNoEncarregado()
    {
        string[] deOutroModulo = [.. Catalogo.Definicoes.Where(static d => d.ReservadaAGovernanca && d.Modulo != ModuloDeAutorizacao).Select(static d => d.Codigo)];

        deOutroModulo.Should().BeEquivalentTo([AprovacaoDaProtecaoDoCatalogo, ConsultaDaProtecaoDoCatalogo, ManifestacaoNaProtecaoDoCatalogo]);
        PerfisQueReunem(AprovacaoDaProtecaoDoCatalogo).Should().Equal(PerfisDeSistema.RepresentanteDoControladorId);
        PerfisQueReunem(ConsultaDaProtecaoDoCatalogo).Should().Equal(PerfisDeSistema.EncarregadoDeDadosId);
        PerfisQueReunem(ManifestacaoNaProtecaoDoCatalogo).Should().Equal(PerfisDeSistema.EncarregadoDeDadosId);
    }

    [Fact(DisplayName = "A governança recusa a permissão não reservada de outro módulo e aceita a reservada")]
    public void Governanca_AdmiteSoAReservadaDeOutroModulo()
    {
        ComposicaoDaGovernanca.Admite(Linha("configuracao:tipos-processo:consultar")).Should().BeFalse();
        ComposicaoDaGovernanca.Admite(Linha(ConsultaDaProtecaoDoCatalogo)).Should().BeTrue();
    }

    [Theory(DisplayName = "O perfil comum recusa as permissões da proteção do catálogo, reservadas à governança")]
    [InlineData(AprovacaoDaProtecaoDoCatalogo)]
    [InlineData(ConsultaDaProtecaoDoCatalogo)]
    [InlineData(ManifestacaoNaProtecaoDoCatalogo)]
    public void PerfilComum_RecusaPermissaoReservadaDeOutroModulo(string codigo)
    {
        ICatalogoDePermissoesConsultavel catalogo = _fixture.Factory.Services.GetRequiredService<ICatalogoDePermissoesConsultavel>();

        Unifesspa.UniPlus.Kernel.Results.Result<ConteudoDaVersao> conteudo = ConteudoDaVersao.Validar(
            "Proteção do catálogo", "Atua na proteção do catálogo.", [codigo], "23479.000001/2026-01", 1024, catalogo);

        conteudo.Errors.Select(static e => e.Error.Code).Should().Contain(PerfilErrorCodes.PermissaoReservada);
    }

    [Theory(DisplayName = "O catálogo de permissões é consultado com cada um dos três perfis de governança")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos)]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados)]
    public async Task CatalogoDePermissoes_ConsultadoPelosTresPerfis(TipoDeGovernanca governanca)
    {
        string sub = $"catalogo.{governanca}".ToLowerInvariant();
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, sub, governanca);

        using HttpClient cliente = Cliente(sub);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri("/api/autorizacao/permissoes", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Guid[] PerfisQueReunem(string codigo) =>
        [.. ComposicaoDaGovernanca.Esperada.Where(p => p.Codigo == codigo).Select(static p => p.PerfilId)];

    private LinhaDoRetratoDoCatalogo Linha(string codigo)
    {
        Catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao).Should().BeTrue(codigo);
        return new(definicao!.Codigo, definicao.Modulo, definicao.Requisito.Sensibilidade, definicao.DeUnidade, definicao.ReservadaAGovernanca);
    }

    private HttpClient Cliente(string sub)
    {
        HttpClient cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);

        return cliente;
    }

    private async Task<PermissaoDeGovernanca[]> ComposicaoGravadaAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Perfis.AsNoTracking()
            .Where(perfil => perfil.Governanca != null)
            .SelectMany(perfil => perfil.Permissoes.Select(permissao => new PermissaoDeGovernanca(perfil.Id, permissao.Codigo)))
            .ToArrayAsync();
    }
}
