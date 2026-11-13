namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Dá permissão às personas dos testes de integração do jeito que a produção dá: gravando a
/// concessão de um perfil. O token dos testes não carrega permissão alguma, porque o token
/// identifica a pessoa e não concede acesso.
/// </summary>
/// <remarks>
/// As permissões de governança só chegam por concessão dos perfis de governança da carga
/// inicial (<see cref="ConcederGovernancaAsync"/>). O perfil comum de teste é criado e posto em
/// vigor pelo próprio domínio, contra o catálogo do host, e por isso nunca reúne permissão reservada.
/// </remarks>
public static class ConcessoesDeTeste
{
    /// <summary>E-mail gravado nas concessões de teste, que a revogação pede de volta para confirmar o beneficiário.</summary>
    public const string EmailDeTeste = "beneficiario@uniplus-test.local";

    private static readonly Identidade ConcedidaPor = new(TestAuthHandler.DefaultIssuer, "concessao.de.teste");

    /// <summary>Cria um perfil comum, ativo, com as permissões informadas, e devolve o seu identificador.</summary>
    public static async Task<Guid> CriarPerfilAsync(IServiceProvider services, params string[] permissoes)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        Perfil perfil = PerfisDeTeste.Vigente(
            $"Perfil de teste {Guid.NewGuid():N}",
            "Perfil criado pelo apoio dos testes de integração.",
            permissoes,
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>());

        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        db.Perfis.Add(perfil);
        await db.SaveChangesAsync();

        return perfil.Id;
    }

    /// <summary>
    /// Concede o perfil à pessoa no escopo informado, sem passar pelo pedido. Sem escopo, vale a
    /// instituição. Concessão igual já ativa é reaproveitada.
    /// </summary>
    /// <param name="services">Provedor de serviços do host de teste.</param>
    /// <param name="sub">Valor do <c>sub</c> que a persona envia no token.</param>
    /// <param name="perfilId">Perfil a conceder.</param>
    /// <param name="escopo">Escopo da concessão; a instituição quando omitido.</param>
    /// <param name="validaAte">Fim da vigência, quando a concessão é por prazo.</param>
    /// <param name="email">E-mail que a concessão guarda do titular; <see cref="EmailDeTeste"/> quando omitido.</param>
    public static async Task<Guid> ConcederAsync(
        IServiceProvider services,
        string sub,
        Guid perfilId,
        EscopoDaConcessao? escopo = null,
        DateTimeOffset? validaAte = null,
        string? email = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        EscopoDaConcessao alcance = escopo ?? EscopoDaConcessao.DaInstituicao();
        Identidade beneficiario = new(TestAuthHandler.DefaultIssuer, sub);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IConcessaoRepository concessoes = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        return await concessoes.ObterNaoRevogadaIgualAsync(beneficiario, perfilId, alcance) is { } existente
            ? existente.Id
            : await GravarNovaAsync(scope.ServiceProvider, beneficiario, perfilId, alcance, (validaAte, email ?? EmailDeTeste));
    }

    private static async Task<Guid> GravarNovaAsync(
        IServiceProvider servicos, Identidade beneficiario, Guid perfilId, EscopoDaConcessao alcance, (DateTimeOffset? ValidaAte, string Email) dados)
    {
        Concessao concessao = await NovaAsync(servicos, beneficiario, perfilId, alcance, dados);
        await servicos.GetRequiredService<IConcessaoRepository>().AdicionarAsync(concessao);
        await servicos.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
        return concessao.Id;
    }

    private static async Task<Concessao> NovaAsync(
        IServiceProvider servicos, Identidade beneficiario, Guid perfilId, EscopoDaConcessao alcance, (DateTimeOffset? ValidaAte, string Email) dados)
    {
        Perfil perfil = await servicos.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfilId)
            ?? throw new InvalidOperationException($"O perfil {perfilId} não existe.");

        return ConcessaoBuilder.DoPerfil(perfil, servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Para(beneficiario, dados.Email)
            .NoEscopo(alcance)
            .ConcedidaPor(ConcedidaPor)
            .Em(servicos.GetRequiredService<TimeProvider>().GetUtcNow())
            .ValidaAte(dados.ValidaAte)
            .Construir();
    }

    /// <summary>
    /// Apaga todas as concessões da pessoa, para o teste começar de uma pessoa sem acesso algum, e antes as providências
    /// solicitadas sobre elas, que a chave estrangeira não deixa para trás.
    /// </summary>
    public static async Task RemoverDaPessoaAsync(IServiceProvider services, string sub)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        IQueryable<Concessao> daPessoa = db.Concessoes.Where(c => c.BeneficiarioEmissor == TestAuthHandler.DefaultIssuer && c.BeneficiarioSub == sub);
        await db.Providencias.Where(p => daPessoa.Any(c => c.Id == p.ConcessaoId)).ExecuteDeleteAsync();
        await daPessoa.ExecuteDeleteAsync();
    }

    /// <summary>Concede à pessoa um perfil novo que reúne as permissões, no escopo da instituição.</summary>
    public static async Task<Guid> ConcederPermissoesAsync(IServiceProvider services, string sub, params string[] permissoes) =>
        await ConcederAsync(services, sub, await CriarPerfilAsync(services, permissoes));

    /// <summary>
    /// Concede à pessoa o perfil de governança da carga inicial: o do gestor de acessos, o do
    /// Representante do controlador ou o do Encarregado de dados.
    /// </summary>
    public static Task<Guid> ConcederGovernancaAsync(
        IServiceProvider services, string sub, TipoDeGovernanca governanca, string? email = null) =>
        ConcederAsync(services, sub, PerfilDe(governanca), email: email);

    private static Guid PerfilDe(TipoDeGovernanca governanca) => governanca switch
    {
        TipoDeGovernanca.GestorDeAcessos => PerfisDeSistema.GestorDeAcessosId,
        TipoDeGovernanca.RepresentanteDoControlador => PerfisDeSistema.RepresentanteDoControladorId,
        _ => PerfisDeSistema.EncarregadoDeDadosId,
    };
}
