namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A semente do catálogo nasce pendente de aprovação da proteção de dados (ADR-0153): toda linha semeada de tipo de
/// documento e de fato. O banco é o migrado, antes de qualquer aprovação.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeNasceComProtecaoPendenteTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public SementeNasceComProtecaoPendenteTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Todo tipo de documento e todo fato semeados nascem pendentes, sem registro de solicitação nem de decisão")]
    public async Task SementeMigrada_NasceComAProtecaoPendente()
    {
        Guid[] tipos = [.. TipoDocumentoSeed.Itens.Select(static i => i.Id)];
        Guid[] fatos = [.. FatoCandidatoSeed.Itens.Select(static i => i.Id)];

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var doTipo = await ctx.TiposDocumento.AsNoTracking().Where(t => tipos.Contains(t.Id))
            .Select(static t => new { t.Aprovacao.Situacao, t.Aprovacao.SolicitadaEm, t.Aprovacao.DecididaEm }).ToListAsync();
        var doFato = await ctx.FatosCandidato.AsNoTracking().Where(f => fatos.Contains(f.Id))
            .Select(static f => new { f.Aprovacao.Situacao, f.Aprovacao.SolicitadaEm, f.Aprovacao.DecididaEm }).ToListAsync();

        doTipo.Should().HaveCount(tipos.Length).And.OnlyContain(static a =>
            a.Situacao == SituacaoDaAprovacao.Pendente && a.SolicitadaEm == null && a.DecididaEm == null);
        doFato.Should().HaveCount(fatos.Length).And.OnlyContain(static a =>
            a.Situacao == SituacaoDaAprovacao.Pendente && a.SolicitadaEm == null && a.DecididaEm == null);
    }
}
