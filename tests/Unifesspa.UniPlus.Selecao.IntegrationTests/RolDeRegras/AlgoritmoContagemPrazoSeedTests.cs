namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura das entradas de <c>tipo=algoritmo_contagem_prazo</c> (#1135): as
/// convenções nomeadas de contagem do prazo de interposição que o UNI-REQ-0080
/// tornou decláraveis por edital. Prova o conteúdo do seed (as quatro
/// resoluções declaradas, com exemplos nas âncoras canônicas; esquema de
/// argumentos vazio; base legal com placeholder honesto de pendência), os
/// hashes dourados que amarram o seed ao literal da migration, o reader por
/// tipo e a fronteira append-only (ADR-0112) no schema-alvo dos testes. A
/// prova de que a precondição de migration aborta diante de referência
/// fabricada fica em <see cref="AlgoritmoContagemPrazoPrecondicaoTests"/>.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste; os únicos valores externos (token e amostra) entram por DbParameter.")]
public sealed class AlgoritmoContagemPrazoSeedTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public AlgoritmoContagemPrazoSeedTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Hashes congelados na migration <c>AddAlgoritmosContagemPrazo</c>. Amarram
    /// a definição do seed ao literal da migration: editar o texto de uma
    /// entrada sem regenerar a migration quebra este teste.
    /// </summary>
    private const string HashExcluiDiaInicial =
        "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72";

    private const string HashHorasUteisDesdeAncora =
        "01bb4ae923690f2ae79373112069723569fc286ee3054bc1e190336256decd56";

    private static RegraCatalogoSeedItem Item(string codigo) =>
        RegraCatalogoSeed.Itens.Single(i => i.Codigo == codigo);

    private static readonly string[] CodigosDeContagem =
    [
        AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial,
        AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora,
    ];

    [Fact(DisplayName = "O reader devolve as duas convenções de contagem, cada uma com código, versão e hash")]
    public async Task Reader_ListarPorTipo_DevolveAsDuasConvencoes()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> algoritmos = await reader.ListarPorTipoAsync(
            TipoRegra.AlgoritmoContagemPrazo, CancellationToken.None);

        algoritmos.Should().HaveCount(2);
        algoritmos.Select(a => a.Codigo).Should().BeEquivalentTo(CodigosDeContagem);
        algoritmos.Should().OnlyContain(a => a.Versao == RegraCatalogoSeed.VersaoV1);
        algoritmos.Single(a => a.Codigo == AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)
            .Hash.Should().Be(HashExcluiDiaInicial);
        algoritmos.Single(a => a.Codigo == AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)
            .Hash.Should().Be(HashHorasUteisDesdeAncora);
    }

    [Fact(DisplayName = "Os hashes dourados amarram a definição do seed ao literal da migration")]
    public void Seed_HashesDourados_AmarramSeedEMigration()
    {
        Item(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial).ComputarHash()
            .Should().Be(HashExcluiDiaInicial, "editar a definição sem regenerar a migration dessincroniza seed e banco");
        Item(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora).ComputarHash()
            .Should().Be(HashHorasUteisDesdeAncora, "editar a definição sem regenerar a migration dessincroniza seed e banco");

        HashExcluiDiaInicial.Should().NotBe(
            HashHorasUteisDesdeAncora, "convenções diferentes têm definições — e hashes — diferentes");
    }

    [Fact(DisplayName = "Cada entrada declara as quatro resoluções, com exemplos nas âncoras canônicas")]
    public void Seed_DeclaraAsQuatroResolucoes_ComExemplosNasAncorasCanonicas()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            RegraCatalogoSeedItem item = Item(codigo);
            item.Tipo.Should().Be(TipoRegra.AlgoritmoContagemPrazo);
            item.Versao.Should().Be(RegraCatalogoSeed.VersaoV1);

            using JsonDocument documento = JsonDocument.Parse(item.InvariantesJson);
            string[] invariantes = [.. documento.RootElement.EnumerateArray().Select(e => e.GetString()!)];

            // As quatro perguntas que distinguem uma convenção da outra
            // (UNI-REQ-0080), cada uma com resposta própria e explícita.
            invariantes.Should().HaveCount(4, $"a entrada {codigo} declara as quatro resoluções");
            invariantes.Should().ContainSingle(i => i.StartsWith("âncora fora da meia-noite:", StringComparison.Ordinal));
            invariantes.Should().ContainSingle(i => i.StartsWith("âncora em dia não útil:", StringComparison.Ordinal));
            invariantes.Should().ContainSingle(i => i.StartsWith("em horas:", StringComparison.Ordinal));
            invariantes.Should().ContainSingle(i => i.StartsWith("em dias úteis:", StringComparison.Ordinal));

            // Intenção e resultado visíveis a quem escolhe: os exemplos
            // resolvidos nas âncoras canônicas vivem na própria entrada.
            string conteudo = string.Join('\n', invariantes);
            conteudo.Should().Contain("sexta 18h", $"a entrada {codigo} resolve a âncora canônica de dia útil fora da meia-noite");
            conteudo.Should().Contain("domingo 18h", $"a entrada {codigo} resolve a âncora canônica em dia não útil");
        }
    }

    [Fact(DisplayName = "O esquema de argumentos é vazio: o algoritmo é escolhido, não parametrizado")]
    public void Seed_EsquemaArgsVazio()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            using JsonDocument esquema = JsonDocument.Parse(Item(codigo).EsquemaArgsJson);

            esquema.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
            esquema.RootElement.EnumerateObject().Should().BeEmpty(
                $"a entrada {codigo} não recebe argumentos do administrador — ela é escolhida, não parametrizada");
        }
    }

    [Fact(DisplayName = "A base legal das duas entradas é o placeholder honesto de pendência, nunca citação")]
    public void Seed_BaseLegal_PlaceholderDePendencia()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            Item(codigo).BaseLegal.Should().Be(
                AlgoritmoContagemPrazoCodigo.BaseLegalPendente,
                $"a base legal da entrada {codigo} é a constante compartilhada do placeholder (UNI-REQ-0095)");
        }
    }

    [Fact(DisplayName = "O placeholder de pendência não contém citação de dispositivo")]
    public void BaseLegalPendente_NaoFabricaCitacao()
    {
        // Guarda sobre a própria constante: a igualdade do teste anterior não
        // protegeria contra alguém "completar" o placeholder com uma citação
        // aproximada — exatamente o que o UNI-REQ-0095 proíbe.
        string placeholder = AlgoritmoContagemPrazoCodigo.BaseLegalPendente;

        placeholder.Should().Contain("PENDENTE", "o placeholder declara a pendência com todas as letras");
        placeholder.Should().Contain("UNI-REQ-0095", "o placeholder aponta a dependência externa que o justifica");

        foreach (string marcaDeCitacao in new[] { "Lei nº", "Lei n.", "Decreto", "Portaria", "art.", "Art.", "§" })
        {
            placeholder.Should().NotContain(
                marcaDeCitacao, $"citação de dispositivo ('{marcaDeCitacao}') seria pior que a lacuna");
        }
    }

    // O código da entrada, como valor JSON — entre aspas. Casar o token aspado
    // (e não a substring nua) impede que um OUTRO código que apenas contenha
    // este como prefixo seja confundido com ele: o valor JSON só bate quando é
    // igual, delimitado.
    private const string DetectaReferenciaJsonb = """
        WITH amostra(configuracao_congelada) AS (VALUES (@amostra::jsonb))
        SELECT count(*) FROM amostra
        WHERE configuracao_congelada::text LIKE '%' || @token || '%'
        """;

    private const string ContaReferenciasReais = """
        SELECT count(*) FROM selecao.versoes_configuracao
        WHERE configuracao_congelada::text LIKE '%' || @token || '%'
        """;

    [Theory(DisplayName = "Nenhuma configuração congelada referencia a entrada de contagem (fronteira da ADR-0112)")]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)]
    [InlineData(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)]
    public async Task NenhumaConfiguracaoCongelada_ReferenciaEntradaDeContagem(string codigo)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string token = $"\"{codigo}\"";

        // Canário positivo: o detector ENXERGA uma configuração que referencia a
        // entrada pelo seu valor de código — sem esta prova, a ausência real
        // abaixo não significaria nada.
        long detectados = await ContarAsync(
            context,
            DetectaReferenciaJsonb,
            token,
            amostra: $$$"""{"regra":{"codigo":"{{{codigo}}}","versao":"v1"}}""");
        detectados.Should().Be(1, "o detector precisa enxergar a referência para a ausência provar algo");

        // Canário negativo: um código DISTINTO que apenas contém o semeado como
        // prefixo não é confundido com ele — a fronteira é por identidade.
        long falsosPositivos = await ContarAsync(
            context,
            DetectaReferenciaJsonb,
            token,
            amostra: $$$"""{"regra":{"codigo":"{{{codigo}}}-LEGADO","versao":"v1"}}""");
        falsosPositivos.Should().Be(0, "um código diferente que contém o semeado como prefixo não é o semeado");

        // Schema real (banco efêmero migrado): nenhuma configuração congelada
        // referencia a entrada — enquanto isso valer, ela ainda é vocabulário e
        // se corrige por substituição (ADR-0112); a partir da primeira
        // referência, só versão nova.
        long referenciasReais = await ContarAsync(context, ContaReferenciasReais, token, amostra: null);
        referenciasReais.Should().Be(
            0, "alterar ou remover uma entrada já congelada por configuração violaria o append-only (RN08)");
    }

    private static async Task<long> ContarAsync(SelecaoDbContext context, string sql, string token, string? amostra)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        AdicionarParametro(comando, "token", token);
        if (amostra is not null)
        {
            AdicionarParametro(comando, "amostra", amostra);
        }

        object? resultado = await comando.ExecuteScalarAsync(CancellationToken.None);
        return Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AdicionarParametro(DbCommand comando, string nome, string valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
