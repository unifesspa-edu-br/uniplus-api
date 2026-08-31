namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o catálogo semeado por <c>HasData</c> contra
/// <see cref="TipoDocumentoSeed"/> — a fonte única — item a item. Um seed que não
/// bate com a lista significa migration desatualizada: o banco de qualquer
/// ambiente novo divergiria do que o código diz semear. As invariantes da própria
/// lista (tamanho do catálogo, formato dos códigos, nomes sem regra embutida) são
/// guardadas sem banco em <c>CatalogoDeTiposDocumentoTests</c>.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDocumentoSeedTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDocumentoSeedTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os setenta tipos do seed existem no banco migrado, com id, código, nome e categoria da fonte única")]
    public async Task Seed_MaterializadoNoBanco_BateComAFonteUnica()
    {
        Guid[] idsDoSeed = [.. TipoDocumentoSeed.Itens.Select(item => item.Id)];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        Dictionary<Guid, TipoDocumento> persistidos = await ctx.TiposDocumento
            .AsNoTracking()
            .Where(t => idsDoSeed.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        persistidos.Should().HaveCount(TipoDocumentoSeed.Itens.Count);

        foreach (TipoDocumentoSeedItem item in TipoDocumentoSeed.Itens)
        {
            persistidos.Should().ContainKey(item.Id, "o identificador do seed é fixo e igual em todo ambiente");
            TipoDocumento persistido = persistidos[item.Id];
            persistido.Codigo.Valor.Should().Be(item.Codigo);
            persistido.Nome.Should().Be(item.Nome);
            persistido.Categoria.Should().Be(item.Categoria);
            persistido.IsDeleted.Should().BeFalse();
        }
    }

    [Fact(DisplayName = "Todo tipo semeado é alcançável por código pelo leitor cross-módulo")]
    public async Task Seed_AlcancavelPorCodigo()
    {
        // É por código que a regra legal referencia o tipo. Um seed que existisse no
        // banco mas não fosse encontrado por esse caminho — por divergência de
        // normalização entre o que se grava e o que se busca — deixaria toda regra
        // que o cita inavaliável, sem nada no cadastro sinalizando o problema.
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoDocumentoReader reader = new(ctx);

        List<string> naoEncontrados = [];
        foreach (TipoDocumentoSeedItem item in TipoDocumentoSeed.Itens)
        {
            TipoDocumentoView? encontrado = await reader.ObterVivoPorCodigoAsync(item.Codigo);
            if (encontrado is null)
            {
                naoEncontrados.Add(item.Codigo);
            }
        }

        naoEncontrados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo semeado continua administrável: pode ser renomeado pelo cadastro")]
    public async Task Seed_PermaneceAdministravel()
    {
        // Seed-governado não é congelado: o catálogo entrega o ponto de partida, e a
        // instituição segue dona do que exibe.
        Guid id = TipoDocumentoSeed.Itens[0].Id;
        string nomeOriginal = TipoDocumentoSeed.Itens[0].Nome;
        string nomeNovo = $"{nomeOriginal} — revisado";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-seed"))
        {
            TipoDocumento tipo = await ctx.TiposDocumento.SingleAsync(t => t.Id == id);
            tipo.Atualizar(tipo.Codigo.Valor, nomeNovo, tipo.Descricao, tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb, tipo.TipoEquivalente)
                .IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null))
        {
            TipoDocumento lido = await readCtx.TiposDocumento.AsNoTracking().SingleAsync(t => t.Id == id);
            lido.Nome.Should().Be(nomeNovo);
        }

        // Devolve ao estado semeado para não contaminar os demais testes da collection.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-seed"))
        {
            TipoDocumento tipo = await ctx.TiposDocumento.SingleAsync(t => t.Id == id);
            tipo.Atualizar(tipo.Codigo.Valor, nomeOriginal, tipo.Descricao, tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb, tipo.TipoEquivalente);
            await ctx.SaveChangesAsync();
        }
    }
}
