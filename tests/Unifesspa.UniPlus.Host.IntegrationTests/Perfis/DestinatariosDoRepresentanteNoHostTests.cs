namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Quem recebe os avisos de decisão protegida, lido no banco: só o Representante do controlador com concessão vigente.
/// A concessão vencida ou revogada não gera destinatário, e o Encarregado de dados não recebe aviso de decisão, tanto
/// na resolução pelo perfil, que o pedido usa, quanto na resolução pela permissão reservada, que a governança de perfis
/// usa. Marlene tem o papel de Representante vigente, Betina tem a concessão vencida, Diogo a revogada e Leandro é o Encarregado.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class DestinatariosDoRepresentanteNoHostTests : PedidosDeConcessaoTestBase, IAsyncLifetime
{
    private static readonly string AprovarPelaProtecao =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo;

    public DestinatariosDoRepresentanteNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public async Task InitializeAsync()
    {
        await ResetarAsync();
        await RepresentanteAsync(Marlene);
        Guid vencida = await RepresentanteAsync(Betina);
        Guid revogada = await RepresentanteAsync(Diogo);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Leandro.Email);
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '10 days', valida_ate = now() - interval '1 day' WHERE id = {0}", vencida);
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET revogada_em = now(), revogada_por_emissor = beneficiario_emissor, revogada_por_sub = 'concessao.de.teste', motivo_revogacao = 'Revogada para o teste.' WHERE id = {0}",
            revogada);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Só o Representante vigente é avisado pela permissão reservada; vencido, revogado e Encarregado não")]
    public async Task PelaPermissao_SoORepresentanteVigente()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<TitularDaPermissao> titulares = await escopo.ServiceProvider.GetRequiredService<ITitularesDaPermissao>()
            .ListarTitularesComEmailAsync(AprovarPelaProtecao);

        titulares.Select(static t => t.Email).Should().Equal(Marlene.Email);
    }

    [Fact(DisplayName = "Só o Representante vigente é avisado pelo perfil; vencido, revogado e Encarregado não")]
    public async Task PeloPerfil_SoORepresentanteVigente()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<TitularComEmail> titulares = await escopo.ServiceProvider.GetRequiredService<IConcessaoRepository>()
            .ListarTitularesAtivosComEmailAsync(PerfisDeSistema.RepresentanteDoControladorId, DateTimeOffset.UtcNow);

        titulares.Select(static t => t.Email).Should().Equal(Marlene.Email);
    }

    private Task<Guid> RepresentanteAsync(Persona persona) =>
        ConcessoesDeTeste.ConcederGovernancaAsync(
            Fixture.Factory.Services, persona.Sub.ToString(), TipoDeGovernanca.RepresentanteDoControlador, persona.Email);
}
