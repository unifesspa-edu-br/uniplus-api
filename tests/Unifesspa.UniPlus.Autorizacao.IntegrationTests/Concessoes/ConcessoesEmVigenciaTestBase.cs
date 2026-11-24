namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// As concessões de um perfil em seis situações de vigência, gravadas no PostgreSQL, para os testes dos recortes e da
/// listagem: sem prazo, com prazo futuro, de unidade, revogada, vencida e vencendo no instante exato de referência.
/// </summary>
public abstract class ConcessoesEmVigenciaTestBase : ConcessaoEndpointTestBase
{
    private protected const string SemPrazo = "sem-prazo";
    private protected const string PrazoFuturo = "prazo-futuro";
    private protected const string NaUnidade = "na-unidade";
    private protected const string Revogada = "REVOGADA";
    private protected const string Vencida = "VENCIDA";
    private protected const string NoLimite = "no-limite";

    // O banco guarda microssegundos: o instante de referência também, para o limite exato comparar igual nos dois lados.
    private protected readonly DateTimeOffset Agora = Truncado(DateTimeOffset.UtcNow);

    private protected readonly Guid Unidade = Guid.NewGuid();

    private protected ConcessoesEmVigenciaTestBase(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    // O perfil só de unidade da concessão de unidade, para provar que a vigente de unidade não vale como da instituição.
    private protected Guid PerfilDeUnidade { get; private set; }

    private static DateTimeOffset Truncado(DateTimeOffset instante) => instante.AddTicks(-(instante.Ticks % 10));

    private protected static string Titular(string caso) => "vigencia." + caso;

    private protected IEnumerable<(string Caso, EscopoDaConcessao Escopo, TimeSpan? Validade, bool Revogada)> Casos() =>
    [
        (SemPrazo, EscopoDaConcessao.DaInstituicao(), null, false),
        (PrazoFuturo, EscopoDaConcessao.DaInstituicao(), TimeSpan.FromHours(1), false),
        (NaUnidade, EscopoDaConcessao.DaUnidade(Unidade), null, false),
        (Revogada, EscopoDaConcessao.DaInstituicao(), null, true),
        (Vencida, EscopoDaConcessao.DaInstituicao(), TimeSpan.FromMinutes(-1), false),
        (NoLimite, EscopoDaConcessao.DaInstituicao(), TimeSpan.Zero, false),
    ];

    // A concessão de unidade pede um perfil de permissões de unidade, e as da instituição, um de permissões institucionais.
    private protected async Task<(Guid Perfil, Dictionary<string, Guid> Ids)> MontarAsync()
    {
        Guid institucional = await ConcessoesDeTeste.CriarPerfilAsync(Fixture.Factory.Services, "configuracao:tipos-processo:consultar");
        Guid deUnidade = PerfilDeUnidade = await CriarPerfilComumAsync();
        Dictionary<string, Guid> ids = [];
        foreach ((string caso, EscopoDaConcessao escopo, TimeSpan? validade, bool revogada) in Casos())
        {
            ids[caso] = await ConcederComoAsync(caso == NaUnidade ? deUnidade : institucional, caso, escopo, validade, revogada);
        }

        return (institucional, ids);
    }

    private protected async Task<Guid> ConcederComoAsync(Guid perfil, string caso, EscopoDaConcessao escopo, TimeSpan? validade, bool revogada)
    {
        Guid id = await ConcederAsync(perfil, Pessoa(Titular(caso)), escopo);
        if (validade is { } prazo)
        {
            await DefinirValidadeAsync(id, Agora + prazo);
        }

        if (revogada)
        {
            await RevogarAsync(id);
        }

        return id;
    }

    // A concessão nasce no passado para a validade, mesmo vencida, ser posterior à concessão (restrição do banco).
    private async Task DefinirValidadeAsync(Guid id, DateTimeOffset validaAte)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = {1}, valida_ate = {2} WHERE id = {0}", id, Agora.AddDays(-2), validaAte);
    }

    private async Task RevogarAsync(Guid id)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Concessao concessao = await db.Set<Concessao>().SingleAsync(c => c.Id == id);
        concessao.Revogar(new AtoDoAgente(Diogo, Agora), "Revogada para o teste da vigência.").IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }
}
