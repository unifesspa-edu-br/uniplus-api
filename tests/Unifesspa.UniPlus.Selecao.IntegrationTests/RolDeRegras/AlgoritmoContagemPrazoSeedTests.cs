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
    /// Hashes congelados nas migrations que semeiam as convenções. Amarram a
    /// definição do seed ao literal da migration: editar o texto de uma entrada
    /// sem regenerar a migration quebra este teste.
    /// </summary>
    private const string HashExcluiDiaInicial =
        "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72";

    private const string HashHorasUteisDesdeAncora =
        "01bb4ae923690f2ae79373112069723569fc286ee3054bc1e190336256decd56";

    private const string HashAvancaDataUtil =
        "73bf9e2448e2656e421243dff5f375c7fc9598eca71a5f291169efe01b777922";

    private static RegraCatalogoSeedItem Item(string codigo) =>
        RegraCatalogoSeed.Itens.Single(i => i.Codigo == codigo);

    private static string[] Invariantes(string codigo)
    {
        using JsonDocument documento = JsonDocument.Parse(Item(codigo).InvariantesJson);
        return [.. documento.RootElement.EnumerateArray().Select(e => e.GetString()!)];
    }

    private static readonly string[] CodigosDeContagem =
    [
        AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial,
        AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora,
        AlgoritmoContagemPrazoCodigo.AvancaDataUtil,
    ];

    private static readonly Dictionary<string, string> HashesDourados = new(StringComparer.Ordinal)
    {
        [AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial] = HashExcluiDiaInicial,
        [AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora] = HashHorasUteisDesdeAncora,
        [AlgoritmoContagemPrazoCodigo.AvancaDataUtil] = HashAvancaDataUtil,
    };

    [Fact(DisplayName = "O reader devolve as três convenções de contagem, cada uma com código, versão e hash")]
    public async Task Reader_ListarPorTipo_DevolveAsConvencoes()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> algoritmos = await reader.ListarPorTipoAsync(
            TipoRegra.AlgoritmoContagemPrazo, CancellationToken.None);

        algoritmos.Should().HaveCount(3);
        algoritmos.Select(a => a.Codigo).Should().BeEquivalentTo(CodigosDeContagem);
        algoritmos.Should().OnlyContain(a => a.Versao == RegraCatalogoSeed.VersaoV1);

        foreach (RegraCatalogo algoritmo in algoritmos)
        {
            algoritmo.Hash.Should().Be(HashesDourados[algoritmo.Codigo]);
        }
    }

    [Fact(DisplayName = "Os hashes dourados amarram a definição do seed ao literal da migration")]
    public void Seed_HashesDourados_AmarramSeedEMigration()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            Item(codigo).ComputarHash().Should().Be(
                HashesDourados[codigo],
                $"editar a definição de {codigo} sem regenerar a migration dessincroniza seed e banco");
        }

        HashesDourados.Values.Should().OnlyHaveUniqueItems(
            "convenções diferentes têm definições — e hashes — diferentes");
    }

    [Fact(DisplayName = "A coincidência declarada entre convenções nomeia uma entrada que existe no catálogo")]
    public void Seed_CoincidenciaDeclarada_NomeiaEntradaExistente()
    {
        // A convenção que avança data útil declara, na invariante de horas, que
        // nessa unidade coincide com outra entrada — informação de que quem
        // escolhe depende. Um código errado ali é texto plausível que ninguém
        // percebe, então o nome citado precisa existir no catálogo.
        string invarianteHoras = Invariantes(AlgoritmoContagemPrazoCodigo.AvancaDataUtil)
            .Single(i => i.StartsWith("em horas:", StringComparison.Ordinal));

        invarianteHoras.Should().Contain(
            AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora,
            "a coincidência em horas é declarada nomeando a outra convenção");

        RegraCatalogoSeed.Itens.Should().Contain(
            i => i.Codigo == AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora
                && i.Tipo == TipoRegra.AlgoritmoContagemPrazo,
            "a convenção citada tem de existir no catálogo, sob o mesmo tipo");
    }

    [Fact(DisplayName = "Cada entrada declara as quatro resoluções, com exemplos nas âncoras canônicas")]
    public void Seed_DeclaraAsQuatroResolucoes_ComExemplosNasAncorasCanonicas()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            RegraCatalogoSeedItem item = Item(codigo);
            item.Tipo.Should().Be(TipoRegra.AlgoritmoContagemPrazo);
            item.Versao.Should().Be(RegraCatalogoSeed.VersaoV1);

            string[] invariantes = Invariantes(codigo);

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

    [Theory(DisplayName = "Nenhuma configuração congelada referencia a entrada de contagem (fronteira da ADR-0112)")]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)]
    [InlineData(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)]
    public async Task NenhumaConfiguracaoCongelada_ReferenciaEntradaDeContagem(string codigo)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        await FronteiraAppendOnlyDoRol.NenhumaReferenciaCongeladaAsync(
            context, codigo, RegraCatalogoSeed.VersaoV1);
    }
}
