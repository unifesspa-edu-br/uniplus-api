namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do catálogo <c>rol_de_fatos_candidato</c> contra Postgres real
/// (UNI-REQ-0077, ADR-0111): seed dos nove fatos, leitor cross-módulo, ordenação,
/// resolução por chave natural, sobrevivência do <c>valores_dominio</c> nulo ao
/// round-trip, CHECKs de domínio/coerência e índice único total do código.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste; os valores externos entram por parâmetro interpolado (DbParameter).")]
public sealed class FatoCandidatoPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Seed materializa exatamente os nove fatos da ADR-0111, batendo com a fonte única")]
    public async Task Seed_MaterializaNoveFatos()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        fatos.Should().HaveCount(FatoCandidatoSeed.Itens.Count).And.HaveCount(9);
        fatos.Select(f => f.Codigo).Should().OnlyHaveUniqueItems();

        foreach (FatoCandidatoSeedItem item in FatoCandidatoSeed.Itens)
        {
            FatoCandidato persistido = fatos.Single(f => f.Codigo == item.Codigo);
            persistido.Nome.Should().Be(item.Nome);
            persistido.Dominio.Should().Be(item.Dominio);
            persistido.Natureza.Should().Be(item.Natureza);
            persistido.Cardinalidade.Should().Be(item.Cardinalidade);

            if (item.ValoresDominio is null)
            {
                persistido.ValoresDominio.Should().BeNull($"{item.Codigo} tem valores nulos na fonte");
            }
            else
            {
                persistido.ValoresDominio.Should().Equal(item.ValoresDominio);
            }
        }
    }

    [Fact(DisplayName = "Cardinalidade: só MODALIDADE e CONDICAO_ATENDIMENTO são multivalorados; os demais escalares")]
    public async Task Seed_CardinalidadeCoerente()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        string[] multivalorados = [.. fatos
            .Where(f => f.Cardinalidade == CardinalidadeFato.Multivalorado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        multivalorados.Should().Equal("CONDICAO_ATENDIMENTO", "MODALIDADE");
        fatos.Where(f => f.Cardinalidade != CardinalidadeFato.Multivalorado)
            .Should().OnlyContain(f => f.Cardinalidade == CardinalidadeFato.Escalar);
    }

    [Fact(DisplayName = "valores_dominio nulo sobrevive ao round-trip jsonb (≠ lista vazia)")]
    public async Task ValoresDominioNulo_SobreviveRoundTrip()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        FatoCandidato booleano = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "QUILOMBOLA");
        booleano.ValoresDominio.Should().BeNull("booleano nunca enumera valores — o nulo é significante");

        FatoCandidato escopoProcesso = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "MODALIDADE");
        escopoProcesso.ValoresDominio.Should().BeNull("categórico de escopo-processo tem valores nulos, não vazios");

        FatoCandidato estatico = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "COR_RACA");
        estatico.ValoresDominio.Should().Equal("BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO");
    }

    [Fact(DisplayName = "Reader (resolvível de fora do assembly) lista ordenado por código e projeta a view")]
    public async Task Reader_Lista_OrdenadoPorCodigo()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        IReadOnlyList<FatoCandidatoView> views = await reader.ListarAsync();

        views.Should().HaveCount(9);
        views.Select(v => v.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);

        FatoCandidatoView corRaca = views.Single(v => v.Codigo == "COR_RACA");
        corRaca.Dominio.Should().Be("CATEGORICO");
        corRaca.Natureza.Should().Be("BRUTO_INFORMADO");
        corRaca.Cardinalidade.Should().Be("ESCALAR");
        corRaca.ValoresDominio.Should().Contain("INDIGENA");
    }

    [Fact(DisplayName = "Reader resolve por chave natural e devolve null para código inexistente")]
    public async Task Reader_ObterPorCodigo_ResolveOuNull()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        FatoCandidatoView? sexo = await reader.ObterPorCodigoAsync("SEXO");
        sexo.Should().NotBeNull();
        sexo!.ValoresDominio.Should().Equal("FEMININO", "MASCULINO", "INTERSEXO");

        FatoCandidatoView? inexistente = await reader.ObterPorCodigoAsync("NAO_EXISTE");
        inexistente.Should().BeNull();
    }

    [Fact(DisplayName = "Índice único total recusa código duplicado (insert cru de um código já semeado)")]
    public async Task IndiceUnicoTotal_RecusaDuplicata()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {"COR_RACA"}, {"Duplicata"}, {"BOOLEANO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, NULL, {DateTimeOffset.UtcNow})
            """);

        Npgsql.PostgresException ex = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        ex.SqlState.Should().Be("23505");
        ex.ConstraintName.Should().Be("ux_rol_de_fatos_candidato_codigo");
    }

    [Fact(DisplayName = "CHECK de domínio recusa token fora do vocabulário via SQL cru")]
    public async Task Check_RecusaDominioInvalido()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"TEXTO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, NULL, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>("o CHECK ck_rol_de_fatos_candidato_dominio bloqueia 'TEXTO'");
    }

    [Fact(DisplayName = "CHECK de coerência recusa valores em fato não-categórico via SQL cru")]
    public async Task Check_RecusaValoresEmNaoCategorico()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, {"[\"SIM\"]"}::jsonb, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência só admite valores em fato CATEGORICO");
    }

    [Fact(DisplayName = "CHECK de coerência recusa array vazio em fato categórico via SQL cru")]
    public async Task Check_RecusaArrayVazioEmCategorico()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, {"[]"}::jsonb, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência exige array não vazio quando valores_dominio é informado");
    }

    [Fact(DisplayName = "CHECK de coerência recusa elemento não-string no array via SQL cru")]
    public async Task Check_RecusaElementoNaoString()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, {"[1]"}::jsonb, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "um elemento numérico quebraria a desserialização de IReadOnlyList<string> no reader");
    }

    [Fact(DisplayName = "CHECK de coerência recusa string em branco no array via SQL cru")]
    public async Task Check_RecusaStringEmBranco()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato (id, codigo, nome, dominio, natureza, cardinalidade, valores_dominio, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"}, {"[\"  \"]"}::jsonb, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a factory recusa item em branco; o CHECK espelha a invariante");
    }

    [Fact(DisplayName = "ValueComparer distingue nulo de lista vazia e preserva o nulo no snapshot")]
    public void ValueComparer_NuloDistintoDeVazio()
    {
        using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer comparer =
            ctx.Model.FindEntityType(typeof(FatoCandidato))!
                .FindProperty(nameof(FatoCandidato.ValoresDominio))!
                .GetValueComparer();

        IReadOnlyList<string> vazia = [];

        comparer.Equals(null, vazia).Should().BeFalse("nulo (escopo-processo) nunca é igual à lista vazia (ADR-0111)");
        comparer.Equals(null, null).Should().BeTrue();
        comparer.Snapshot(null).Should().BeNull("o snapshot de um nulo não pode virar lista vazia");
    }

    [Fact(DisplayName = "Seed bate com o roster literal independente da ADR-0111 (autoridade), não só com a própria fonte")]
    public async Task Seed_BateComRosterIndependente()
    {
        // Expectativa literal declarada aqui — independente de FatoCandidatoSeed.Itens.
        // Falha se a fonte do seed divergir da autoridade da ADR (código, id fixo,
        // domínio, natureza, cardinalidade ou valores), não apenas se a migration
        // divergir da fonte.
        (string Codigo, string IdSufixo, DominioFato Dominio, NaturezaFato Natureza, CardinalidadeFato Cardinalidade, string[]? Valores)[] esperado =
        [
            ("COR_RACA", "001", DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
                ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"]),
            ("QUILOMBOLA", "002", DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar, null),
            ("PCD", "003", DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar, null),
            ("EGRESSO_ESCOLA_PUBLICA", "004", DominioFato.Booleano, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar, null),
            ("RENDA_PER_CAPITA", "005", DominioFato.Numerico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar, null),
            ("FAIXA_ETARIA", "006", DominioFato.Numerico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar, null),
            ("SEXO", "007", DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Escalar,
                ["FEMININO", "MASCULINO", "INTERSEXO"]),
            ("MODALIDADE", "008", DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Multivalorado, null),
            ("CONDICAO_ATENDIMENTO", "009", DominioFato.Categorico, NaturezaFato.BrutoInformado, CardinalidadeFato.Multivalorado, null),
        ];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        fatos.Should().HaveCount(esperado.Length);

        foreach ((string codigo, string idSufixo, DominioFato dominio, NaturezaFato natureza, CardinalidadeFato cardinalidade, string[]? valores) in esperado)
        {
            FatoCandidato fato = fatos.Single(f => f.Codigo == codigo);
            fato.Id.Should().Be(Guid.Parse($"fa700000-0000-7000-8000-{idSufixo.PadLeft(12, '0')}"));
            fato.Dominio.Should().Be(dominio);
            fato.Natureza.Should().Be(natureza, "todos os nove fatos desta colheita são BRUTO_INFORMADO (ADR-0111)");
            fato.Cardinalidade.Should().Be(cardinalidade);

            if (valores is null)
            {
                fato.ValoresDominio.Should().BeNull();
            }
            else
            {
                fato.ValoresDominio.Should().Equal(valores);
            }
        }
    }

    private static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
