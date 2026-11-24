namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Quem recebe os avisos de decisão protegida, lido no banco: só o Representante do controlador com concessão vigente.
/// A concessão vencida ou revogada não gera destinatário, e o Gestor de proteção de dados não recebe aviso de decisão, tanto
/// na resolução pelo perfil, que o pedido usa, quanto na resolução pela permissão reservada, que a governança de perfis
/// usa. As personas são exclusivas desta classe: Sérgio tem o papel de Representante vigente, Roberto tem a concessão
/// vencida, Thiago a revogada e Lara tem o papel de Gestor de proteção de dados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class DestinatariosDoRepresentanteNoHostTests : PedidosDeConcessaoTestBase, IAsyncLifetime
{
    private static readonly Persona Sergio = new("sergio.dias", "Sérgio Theo Dias", "sergiotheodias@uniplus-test.local");
    private static readonly Persona Roberto = new("roberto.alves", "Roberto Ian Alves", "robertoianalves@uniplus-test.local");
    private static readonly Persona Thiago = new("thiago.silva", "Thiago Joaquim Enzo da Silva", "thiago-dasilva90@uniplus-test.local");
    private static readonly Persona Lara = new("lara.aragao", "Lara Isadora Aragão", "laraisadoraaragao@uniplus-test.local");

    private static readonly string AprovarPelaProtecao =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo;

    // Só as concessões que o caso criou são apagadas ao fim dele; a que já existia fica como estava.
    private readonly List<Guid> _criadas = [];

    public DestinatariosDoRepresentanteNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public async Task InitializeAsync()
    {
        await ResetarAsync();
        await ConcederAsync(Sergio, TipoDeGovernanca.RepresentanteDoControlador);
        Guid vencida = await ConcederAsync(Roberto, TipoDeGovernanca.RepresentanteDoControlador);
        Guid revogada = await ConcederAsync(Thiago, TipoDeGovernanca.RepresentanteDoControlador);
        await ConcederAsync(Lara, TipoDeGovernanca.GestorDeProtecaoDeDados);
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '10 days', valida_ate = now() - interval '1 day' WHERE id = {0}", vencida);
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET revogada_em = now(), revogada_por_emissor = beneficiario_emissor, revogada_por_conta_id = 'concessao.de.teste', motivo_revogacao = 'Revogada para o teste.' WHERE id = {0}",
            revogada);
    }

    public async Task DisposeAsync()
    {
        Guid[] criadas = [.. _criadas];
        await ExecutarSqlAsync("DELETE FROM autorizacao.liberacoes_da_reclassificacao WHERE concessao_id = ANY({0})", criadas);
        await ExecutarSqlAsync("DELETE FROM autorizacao.bases_da_permissao_concedida WHERE concessao_id = ANY({0})", criadas);
        await ExecutarSqlAsync("DELETE FROM autorizacao.concessoes WHERE id = ANY({0})", criadas);
    }

    [Fact(DisplayName = "Só o Representante vigente é avisado pela permissão reservada; vencido, revogado e Gestor de proteção de dados não")]
    public async Task PelaPermissao_SoORepresentanteVigente()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<TitularDaPermissao> titulares = await escopo.ServiceProvider.GetRequiredService<ITitularesDaPermissao>()
            .ListarTitularesComEmailAsync(AprovarPelaProtecao);

        SoOVigente(titulares.Select(static t => t.Email));
    }

    [Fact(DisplayName = "Só o Representante vigente é avisado pelo perfil; vencido, revogado e Gestor de proteção de dados não")]
    public async Task PeloPerfil_SoORepresentanteVigente()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<TitularComEmail> titulares = await escopo.ServiceProvider.GetRequiredService<IConcessaoRepository>()
            .ListarTitularesAtivosComEmailAsync(PerfisDeSistema.RepresentanteDoControladorId, DateTimeOffset.UtcNow);

        SoOVigente(titulares.Select(static t => t.Email));
    }

    // Outras classes da coleção podem ter Representantes; aqui só importa o que acontece com as personas desta.
    private static void SoOVigente(IEnumerable<string> emails) =>
        emails.Should().Contain(Sergio.Email).And.NotContain([Roberto.Email, Thiago.Email, Lara.Email]);

    private static Guid PerfilDe(TipoDeGovernanca governanca) =>
        governanca == TipoDeGovernanca.RepresentanteDoControlador ? PerfisDeSistema.RepresentanteDoControladorId : PerfisDeSistema.GestorDeProtecaoDeDadosId;

    // Confere antes se a concessão já existia, porque o apoio reaproveita a igual e devolve o mesmo id.
    private async Task<Guid> ConcederAsync(Persona persona, TipoDeGovernanca governanca)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        Concessao? existente = await escopo.ServiceProvider.GetRequiredService<IConcessaoRepository>().ObterNaoRevogadaIgualAsync(
            new Identidade(TestAuthHandler.DefaultIssuer, persona.ContaId.ToString()), PerfilDe(governanca), EscopoDaConcessao.DaInstituicao());
        Guid id = await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, persona.ContaId.ToString(), governanca, persona.Email);
        if (existente is null)
        {
            _criadas.Add(id);
        }

        return id;
    }
}
