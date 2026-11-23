namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

public sealed class OrdemDosItensENiveisPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task ReordenarItens_MantemIdentificadores()
    {
        Guid etapaId = await GravarAsync();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        EtapaProcesso etapa = await contexto.Set<EtapaProcesso>().Include(e => e.ItensDeAvaliacao).SingleAsync(e => e.Id == etapaId);
        ItemDeAvaliacao[] itens = [.. etapa.ItensDeAvaliacao];
        EtapaDeAtendimentoBuilder.RedeclararItens(etapa, [new(itens[1].Id, itens[1].Rotulo, 5m), new(itens[0].Id, itens[0].Rotulo, 5m)]);
        await contexto.SaveChangesAsync();
        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        EtapaProcesso relida = await leitura.Set<EtapaProcesso>().Include(e => e.ItensDeAvaliacao).SingleAsync(e => e.Id == etapaId);
        relida.ItensDeAvaliacao.Select(static item => (item.Id, item.Ordem)).Should().Equal((itens[1].Id, 1), (itens[0].Id, 2));
    }

    [Fact]
    public async Task ReordenarNiveis_MantemIdentificadores()
    {
        Guid etapaId = await GravarAsync();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        int[] anteriores = await IdentificadoresDosNiveisAsync(contexto, etapaId);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE selecao.niveis_de_atendimento_da_etapa SET ordem = 3 - ordem WHERE etapa_processo_id = {etapaId}
            """);
        int[] posteriores = await IdentificadoresDosNiveisAsync(contexto, etapaId);
        posteriores.Should().Equal(anteriores.Reverse());
    }

    [Fact]
    public async Task TrocarRotulosDeNiveis_MantemIdentificadores()
    {
        Guid etapaId = await GravarAsync();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        int[] anteriores = await IdentificadoresDosNiveisAsync(contexto, etapaId);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE selecao.niveis_de_atendimento_da_etapa SET rotulo = CASE rotulo WHEN 'Parcial' THEN 'Completo' ELSE 'Parcial' END
            WHERE etapa_processo_id = {etapaId}
            """);
        int[] posteriores = await IdentificadoresDosNiveisAsync(contexto, etapaId);
        posteriores.Should().Equal(anteriores);
    }

    private static Task<int[]> IdentificadoresDosNiveisAsync(UniPlusDbContext contexto, Guid etapaId) =>
        contexto.Database.SqlQuery<int>($"""
            SELECT id AS "Value" FROM selecao.niveis_de_atendimento_da_etapa WHERE etapa_processo_id = {etapaId} ORDER BY ordem
            """).ToArrayAsync();

    private async Task<Guid> GravarAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = EtapaDeAtendimentoBuilder.CriarProcesso([new("Parcial", 0.5m), new("Completo", 1m)], [new(null, "Item", 10m)]);
        EtapaProcesso etapa = processo.Etapas.Single();
        EtapaDeAtendimentoBuilder.RedeclararItens(etapa, [new(null, "A", 5m), new(null, "B", 5m)]);
        contexto.Set<ProcessoSeletivo>().Add(processo);
        await contexto.SaveChangesAsync();
        return etapa.Id;
    }

}
