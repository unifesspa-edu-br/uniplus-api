namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Dá permissão às personas dos testes de integração do jeito que a produção dá: gravando a
/// concessão de um perfil. O token dos testes não carrega permissão alguma, porque o token
/// identifica a pessoa e não concede acesso.
/// </summary>
/// <remarks>
/// As permissões de governança só chegam por concessão dos perfis de governança da carga
/// inicial (<see cref="ConcederGovernancaAsync"/>). O perfil comum de teste é criado pelo
/// próprio domínio, contra o catálogo do host, e por isso nunca reúne permissão reservada.
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
        Result<Perfil> perfil = Perfil.Criar(
            $"Perfil de teste {Guid.NewGuid():N}",
            "Perfil criado pelo apoio dos testes de integração.",
            permissoes,
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>());
        if (perfil.IsFailure)
        {
            throw new InvalidOperationException(string.Join("; ", perfil.Errors.Select(e => e.Error.Message)));
        }

        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        db.Perfis.Add(perfil.Value!);
        await db.SaveChangesAsync();

        return perfil.Value!.Id;
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
    public static async Task<Guid> ConcederAsync(
        IServiceProvider services,
        string sub,
        Guid perfilId,
        EscopoDaConcessao? escopo = null,
        DateTimeOffset? validaAte = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        EscopoDaConcessao alcance = escopo ?? EscopoDaConcessao.DaInstituicao();
        Identidade beneficiario = new(TestAuthHandler.DefaultIssuer, sub);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IConcessaoRepository concessoes = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();

        Concessao? existente = await concessoes.ObterNaoRevogadaIgualAsync(beneficiario, perfilId, alcance);
        if (existente is not null)
        {
            return existente.Id;
        }

        Perfil perfil = await concessoes.ObterParaConcederAsync(perfilId)
            ?? throw new InvalidOperationException($"O perfil {perfilId} não existe.");
        Result<Concessao> concessao = Concessao.Conceder(
            beneficiario,
            perfil,
            alcance,
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            ConcedidaPor,
            scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>(),
            validaAte,
            EmailDeTeste);
        if (concessao.IsFailure)
        {
            throw new InvalidOperationException(string.Join("; ", concessao.Errors.Select(e => e.Error.Message)));
        }

        await concessoes.AdicionarAsync(concessao.Value!);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();

        return concessao.Value!.Id;
    }

    /// <summary>Apaga todas as concessões da pessoa, para o teste começar de uma pessoa sem acesso algum.</summary>
    public static async Task RemoverDaPessoaAsync(IServiceProvider services, string sub)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
            .Where(c => c.BeneficiarioEmissor == TestAuthHandler.DefaultIssuer && c.BeneficiarioSub == sub)
            .ExecuteDeleteAsync();
    }

    /// <summary>Concede à pessoa um perfil novo que reúne as permissões, no escopo da instituição.</summary>
    public static async Task<Guid> ConcederPermissoesAsync(IServiceProvider services, string sub, params string[] permissoes) =>
        await ConcederAsync(services, sub, await CriarPerfilAsync(services, permissoes));

    /// <summary>
    /// Concede à pessoa o perfil de governança da carga inicial: o do gestor de acessos ou o da
    /// encarregada de dados.
    /// </summary>
    public static Task<Guid> ConcederGovernancaAsync(IServiceProvider services, string sub, TipoDeGovernanca governanca) =>
        ConcederAsync(
            services,
            sub,
            governanca == TipoDeGovernanca.GestorDeAcessos
                ? PerfisDeSistema.GestorDeAcessosId
                : PerfisDeSistema.EncarregadaDeDadosId);
}
