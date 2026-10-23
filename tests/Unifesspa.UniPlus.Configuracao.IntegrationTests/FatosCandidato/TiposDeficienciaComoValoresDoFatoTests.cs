namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O tipo de deficiência da reserva de vagas oferece os tipos do cadastro de tipos de deficiência, em
/// qualquer processo, sem depender da oferta de atendimento (UNI-REQ-0012): o leitor do catálogo os
/// entrega como valores do fato, contra Postgres real.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TiposDeficienciaComoValoresDoFatoTests
{
    private const string Admin = "admin-tipos-deficiencia";

    private readonly ConfiguracaoDbFixture _fixture;

    public TiposDeficienciaComoValoresDoFatoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "TIPO_DEFICIENCIA tem como valores os tipos do cadastro, pelo código e com o nome, o removido inativo e o código recriado pelo tipo vivo")]
    public async Task Leitor_TipoDeficiencia_ValoresSaoOsTiposDoCadastro()
    {
        string prefixo = $"DEF_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        TipoDeficiencia vivo = Novo($"{prefixo}_A", $"{prefixo} vivo");
        TipoDeficiencia removido = Novo($"{prefixo}_B", $"{prefixo} removido");
        TipoDeficiencia substituido = Novo($"{prefixo}_C", $"{prefixo} substituído");
        TipoDeficiencia recriado = Novo($"{prefixo}_C", $"{prefixo} recriado");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin))
        {
            ctx.TiposDeficiencia.AddRange(vivo, removido, substituido);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin))
        {
            ctx.TiposDeficiencia.RemoveRange(
                await ctx.TiposDeficiencia.Where(t => t.Id == removido.Id || t.Id == substituido.Id).ToListAsync());
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin))
        {
            ctx.TiposDeficiencia.Add(recriado);
            await ctx.SaveChangesAsync();
        }

        try
        {
            await ConferirOsValoresDoFatoAsync(prefixo);
        }
        finally
        {
            // O banco é compartilhado pela coleção: os tipos do teste não ficam no cadastro dos outros.
            await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
            await ctx.TiposDeficiencia.IgnoreQueryFilters()
                .Where(t => t.Id == vivo.Id || t.Id == removido.Id || t.Id == substituido.Id || t.Id == recriado.Id)
                .ExecuteDeleteAsync();
        }
    }

    private async Task ConferirOsValoresDoFatoAsync(string prefixo)
    {
        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        FatoCandidatoReader reader = new(readCtx);
        FatoCandidatoView? porCodigo = await reader.ObterPorCodigoAsync("TIPO_DEFICIENCIA");
        FatoCandidatoView daLista = (await reader.ListarAsync()).Single(static f => f.Codigo == "TIPO_DEFICIENCIA");

        foreach (FatoCandidatoView fato in new[] { porCodigo!, daLista })
        {
            fato.FonteValores.Should().Be("TIPOS_DEFICIENCIA");
            fato.ValoresDominioDeclarados!
                .Where(v => v.Codigo.StartsWith(prefixo, StringComparison.Ordinal))
                .Select(static v => (v.Codigo, v.Descricao, v.Ativo))
                .Should().Equal(
                    ($"{prefixo}_A", $"{prefixo} vivo", true),
                    ($"{prefixo}_B", $"{prefixo} removido", false),
                    ($"{prefixo}_C", $"{prefixo} recriado", true));
            fato.ValoresDominio.Should().Equal(fato.ValoresDominioDeclarados!.Select(static v => v.Codigo));
        }
    }

    private static TipoDeficiencia Novo(string codigo, string nome) =>
        TipoDeficiencia.Criar(codigo, nome, "Descrição de teste").Value!;
}
