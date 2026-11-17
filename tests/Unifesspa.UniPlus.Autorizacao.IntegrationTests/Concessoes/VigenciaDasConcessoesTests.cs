namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O recorte das concessões vigentes sobre concessões reais no PostgreSQL: a tradução do banco, o instante exato do
/// vencimento e a concordância com a regra do domínio (<see cref="Concessao.EstaAtiva"/>).
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class VigenciaDasConcessoesTests : ConcessoesEmVigenciaTestBase
{
    public VigenciaDasConcessoesTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Vigentes recorta as não revogadas cuja vigência não venceu, e a que vence no instante exato já venceu")]
    public async Task Vigentes_RecortaPelaRevogacaoEPeloInstante()
    {
        (_, Dictionary<string, Guid> ids) = await MontarAsync();

        Guid[] vigentes = await IdsDoRecorteAsync(ids, q => q.Vigentes(Agora));

        vigentes.Should().BeEquivalentTo([ids[SemPrazo], ids[PrazoFuturo], ids[NaUnidade]]);
    }

    [Fact(DisplayName = "VigentesDaInstituicao deixa de fora a concessão de unidade além das revogadas e vencidas")]
    public async Task VigentesDaInstituicao_SoEscopoDaInstituicao()
    {
        (_, Dictionary<string, Guid> ids) = await MontarAsync();

        Guid[] vigentes = await IdsDoRecorteAsync(ids, q => q.VigentesDaInstituicao(Agora));

        vigentes.Should().BeEquivalentTo([ids[SemPrazo], ids[PrazoFuturo]]);
    }

    [Fact(DisplayName = "Vigentes concorda com a regra do domínio para cada concessão")]
    public async Task Vigentes_ConcordaComEstaAtivaDoDominio()
    {
        (_, Dictionary<string, Guid> ids) = await MontarAsync();
        Guid[] vigentes = await IdsDoRecorteAsync(ids, q => q.Vigentes(Agora));

        Concessao[] todas = await ConcessoesAsync(ids);

        todas.Should().HaveCount(6);
        todas.Should().OnlyContain(c => vigentes.Contains(c.Id) == c.EstaAtiva(Agora));
    }

    [Fact(DisplayName = "A pessoa só tem concessão ativa do perfil quando ela vale na instituição no instante")]
    public async Task TemConcessaoAtivaDoPerfil_SoVigenteDaInstituicao()
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();

        Dictionary<string, bool> tem = [];
        foreach (string nome in ids.Keys)
        {
            tem[nome] = await TemAtivaAsync(Pessoa(Titular(nome)), perfil);
        }

        tem.Where(p => p.Value).Select(p => p.Key).Should().BeEquivalentTo([SemPrazo, PrazoFuturo]);
        (await TemAtivaAsync(Pessoa(Titular(SemPrazo)), Guid.NewGuid())).Should().BeFalse("o perfil é outro");
        (await TemAtivaAsync(Pessoa(Titular(NaUnidade)), PerfilDeUnidade)).Should().BeFalse("a vigente de unidade não vale na instituição");
    }

    private async Task<Guid[]> IdsDoRecorteAsync(Dictionary<string, Guid> ids, Func<IQueryable<Concessao>, IQueryable<Concessao>> recorte)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IQueryable<Concessao> doPerfil = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .Set<Concessao>().AsNoTracking().Where(c => ids.Values.Contains(c.Id));

        return await recorte(doPerfil).Select(c => c.Id).ToArrayAsync();
    }

    private async Task<Concessao[]> ConcessoesAsync(Dictionary<string, Guid> ids)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .Set<Concessao>().AsNoTracking().Where(c => ids.Values.Contains(c.Id)).ToArrayAsync();
    }

    private async Task<bool> TemAtivaAsync(Identidade pessoa, Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().TemConcessaoAtivaDoPerfilAsync(pessoa, perfil, Agora);
    }
}
