namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class LeitoresDeTiposPorCodigoTests(ConfiguracaoDbFixture fixture)
{
    [Fact]
    public async Task ObterAtivoPorCodigoAsync_TipoEtapaComEspacos_NaoEncontra()
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(userId: null);
        TipoEtapaReader reader = new(context);

        (await reader.ObterAtivoPorCodigoAsync("NOTA_ENEM")).Should().NotBeNull();
        (await reader.ObterAtivoPorCodigoAsync(" NOTA_ENEM ")).Should().BeNull();
    }

    [Fact]
    public async Task ObterAtivoPorCodigoAsync_TipoProcessoComEspacos_NaoEncontra()
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await context.Database.BeginTransactionAsync();
        TipoProcesso tipo = TipoProcesso.Criar("TIPO_EXATO", "Tipo de teste", null).Value!;
        context.TiposProcesso.Add(tipo);
        await context.SaveChangesAsync();
        TipoProcessoReader reader = new(context);

        (await reader.ObterAtivoPorCodigoAsync("TIPO_EXATO")).Should().NotBeNull();
        (await reader.ObterAtivoPorCodigoAsync(" TIPO_EXATO ")).Should().BeNull();
    }
    [Fact]
    public async Task ObterNomesPorCodigosAsync_ComVariosCodigos_IncluiDesativados()
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await context.Database.BeginTransactionAsync();
        TipoProcesso ativo = TipoProcesso.Criar("LEITURA_ATIVO", "Nome ativo", null).Value!;
        TipoProcesso inativo = TipoProcesso.Criar("LEITURA_INATIVO", "Nome inativo", null).Value!;
        inativo.Desativar().IsSuccess.Should().BeTrue();
        context.TiposProcesso.AddRange(ativo, inativo);
        await context.SaveChangesAsync();

        IReadOnlyDictionary<string, string> nomes = await new TipoProcessoReader(context).ObterNomesPorCodigosAsync(
            [ativo.Codigo, inativo.Codigo, TodosOsTiposDeProcesso.Codigo, "INEXISTENTE"]);

        nomes.Should().BeEquivalentTo(new Dictionary<string, string> { [ativo.Codigo] = ativo.Nome, [inativo.Codigo] = inativo.Nome });
    }

    [Fact]
    public async Task ObterNomesPorCodigosAsync_SemCodigos_RetornaVazio()
    {
        await using UniPlusDbContext context = fixture.CreateDbContext(userId: null);

        IReadOnlyDictionary<string, string> nomes = await new TipoProcessoReader(context).ObterNomesPorCodigosAsync([]);

        nomes.Should().BeEmpty();
    }
}
