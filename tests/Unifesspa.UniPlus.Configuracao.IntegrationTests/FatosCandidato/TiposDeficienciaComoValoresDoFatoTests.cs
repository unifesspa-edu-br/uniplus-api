namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O tipo de deficiência da reserva de vagas oferece os tipos do cadastro de tipos de deficiência, em
/// qualquer processo, sem depender do atendimento especializado (UNI-REQ-0012): o leitor do catálogo os
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
        TipoDeficiencia[] tipos = NovosTipos(prefixo);
        try
        {
            await CadastrarERemoverAsync(tipos);
            await ConferirOsValoresDoFatoAsync(prefixo);
        }
        finally
        {
            await ApagarDoCadastroAsync([.. tipos.Select(static t => t.Id)]);
        }
    }

    /// <summary>Os tipos do teste, nesta ordem: vivo, removido, substituído e recriado (que reusa o código do substituído).</summary>
    private static TipoDeficiencia[] NovosTipos(string prefixo) =>
    [
        Novo($"{prefixo}_A", $"{prefixo} vivo"),
        Novo($"{prefixo}_B", $"{prefixo} removido"),
        Novo($"{prefixo}_C", $"{prefixo} substituído"),
        Novo($"{prefixo}_C", $"{prefixo} recriado"),
    ];

    private async Task CadastrarERemoverAsync(TipoDeficiencia[] tipos)
    {
        await CadastrarAsync(tipos[..3]);
        await RemoverAsync(tipos[1].Id, tipos[2].Id);
        await CadastrarAsync(tipos[3]);
    }

    private async Task CadastrarAsync(params TipoDeficiencia[] tipos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.TiposDeficiencia.AddRange(tipos);
        await ctx.SaveChangesAsync();
    }

    private async Task RemoverAsync(params Guid[] ids)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.TiposDeficiencia.RemoveRange(await ctx.TiposDeficiencia.Where(t => ids.Contains(t.Id)).ToListAsync());
        await ctx.SaveChangesAsync();
    }

    // O banco é compartilhado pela coleção: os tipos do teste não ficam no cadastro dos outros.
    private async Task ApagarDoCadastroAsync(Guid[] ids)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.TiposDeficiencia.IgnoreQueryFilters().Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
    }

    private async Task ConferirOsValoresDoFatoAsync(string prefixo)
    {
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        FatoCandidatoReader reader = new(readCtx);
        FatoCandidatoView? porCodigo = await reader.ObterPorCodigoAsync("TIPO_DEFICIENCIA");
        FatoCandidatoView daLista = (await reader.ListarAsync()).Single(static f => f.Codigo == "TIPO_DEFICIENCIA");

        ConferirOsValores(porCodigo!, prefixo);
        ConferirOsValores(daLista, prefixo);
    }

    private static void ConferirOsValores(FatoCandidatoView fato, string prefixo)
    {
        fato.FonteValoresValor.Should().Be(FonteValoresFato.TiposDeDeficiencia);
        fato.ValoresDominioDeclarados!
            .Where(v => v.Codigo.StartsWith(prefixo, StringComparison.Ordinal))
            .Select(static v => (v.Codigo, v.Descricao, v.Ativo))
            .Should().Equal(
                ($"{prefixo}_A", $"{prefixo} vivo", true),
                ($"{prefixo}_B", $"{prefixo} removido", false),
                ($"{prefixo}_C", $"{prefixo} recriado", true));
        fato.ValoresDominio.Should().Equal(fato.ValoresDominioDeclarados!.Select(static v => v.Codigo));
    }

    private static TipoDeficiencia Novo(string codigo, string nome) =>
        TipoDeficiencia.Criar(codigo, nome, "Descrição de teste").Value!;
}
