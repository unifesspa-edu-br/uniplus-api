namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

/// <summary>Confere a persistência dos itens de avaliação de uma etapa.</summary>
public sealed class ItensDeAvaliacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task GravarEtapa_DevolveItensComOMesmoIdNaOrdemDeclarada()
    {
        EtapaProcesso etapa = await GravarAsync();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        EtapaProcesso relida = await Carregar(contexto, etapa.Id);
        relida.ItensDeAvaliacao.Select(i => (i.Id, i.Rotulo, i.Ordem)).Should().Equal(
            etapa.ItensDeAvaliacao.Select(i => (i.Id, i.Rotulo, i.Ordem)));
    }

    [Fact]
    public async Task RegravarItemComId_TrocaORotuloNaMesmaLinha()
    {
        EtapaProcesso gravada = await GravarAsync();
        Guid idDaClareza = gravada.ItensDeAvaliacao.Single(i => i.Rotulo == "Clareza").Id;
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        EtapaProcesso etapa = await Carregar(contexto, gravada.Id);
        EtapaDeAtendimentoBuilder.RedeclararItens(etapa, [new(idDaClareza, "Objetividade", 4m), new(null, "Postura", 6m)]);
        await contexto.SaveChangesAsync();
        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        EtapaProcesso relida = await Carregar(leitura, etapa.Id);
        relida.ItensDeAvaliacao.Should().Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade")
            .And.Contain(i => i.Rotulo == "Postura");
    }

    [Fact]
    public async Task RegravarSemUmItem_ApagaALinhaDoItemRetirado()
    {
        EtapaProcesso gravada = await GravarAsync();
        Guid idDoDominio = gravada.ItensDeAvaliacao.Single(i => i.Rotulo == "Domínio").Id;
        Guid idDaClareza = gravada.ItensDeAvaliacao.Single(i => i.Rotulo == "Clareza").Id;
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        EtapaProcesso etapa = await Carregar(contexto, gravada.Id);
        EtapaDeAtendimentoBuilder.RedeclararItens(etapa, [new(idDaClareza, "Clareza", 4m), new(null, "Postura", 6m)]);
        await contexto.SaveChangesAsync();
        await using UniPlusDbContext leitura = fixture.CreateDbContext();
        EtapaProcesso relida = await Carregar(leitura, etapa.Id);
        relida.ItensDeAvaliacao.Should().HaveCount(2).And.NotContain(i => i.Id == idDoDominio);
    }

    private async Task<EtapaProcesso> GravarAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = EtapaDeAtendimentoBuilder.CriarProcesso(
            [new("Parcial", 0.5m), new("Completo", 1m)], [new(null, "Clareza", 4m), new(null, "Domínio", 6m)]);
        contexto.Set<ProcessoSeletivo>().Add(processo);
        await contexto.SaveChangesAsync();
        return processo.Etapas.Single();
    }

    private static Task<EtapaProcesso> Carregar(UniPlusDbContext contexto, Guid etapaId) =>
        contexto.Set<EtapaProcesso>().Include(e => e.ItensDeAvaliacao).FirstAsync(e => e.Id == etapaId);
}
