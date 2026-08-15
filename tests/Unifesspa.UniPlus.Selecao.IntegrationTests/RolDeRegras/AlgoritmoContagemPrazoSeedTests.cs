namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

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
/// convenções nomeadas de contagem do prazo de interposição que o UNI-REQ-0112
/// tornou decláraveis por edital. Prova o conteúdo do seed (as quatro
/// resoluções declaradas, com exemplos nas âncoras canônicas; esquema de
/// argumentos vazio; base legal com placeholder honesto de pendência), os
/// hashes dourados que amarram o seed ao literal da migration, o reader por
/// tipo e a fronteira append-only (ADR-0112) no schema-alvo dos testes. A
/// prova de que a precondição de migration aborta diante de referência
/// fabricada fica em <see cref="AlgoritmoContagemPrazoPrecondicaoTests"/>.
/// </summary>
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
        "fce95fc44b52a5a93a697b0309659a5af0085f9d39ceac1c3917c7b00b1c0be5";

    private const string HashHorasUteisDesdeAncora =
        "49b637293aaaa71449dcb971fc548c59fc840545d2d1740ac352f858a291105b";

    private const string HashAvancaDataUtil =
        "cd4c631492d02126c88a7ca5558992b3ed8a27c80692ecabfb73609293f2a9c8";

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
            // (UNI-REQ-0112), cada uma com resposta própria e explícita.
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

    [Fact(DisplayName = "A base legal das três entradas é a constante compartilhada do fundamento declarado pelo edital")]
    public void Seed_BaseLegal_FundamentoDeclaradoPeloEdital()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            Item(codigo).BaseLegal.Should().Be(
                AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital,
                $"a base legal da entrada {codigo} é a constante compartilhada do fundamento aprovado (UNI-REQ-0095)");
        }
    }

    [Fact(DisplayName = "O fundamento não excede o que a decisão institucional sustenta")]
    public void BaseLegal_NaoExcedeADecisao()
    {
        // Antes da decisão, a guarda protegia contra completar o placeholder com citação
        // aproximada. Agora que há fundamento, o risco inverteu: o texto pode reivindicar
        // mais autoridade do que a decisão tem. O registro dela declara os próprios limites
        // — sem parecer formal, sem manifestação direta do procurador, sem precedente
        // individualizado —, e é isso que se fixa aqui.
        string fundamento = AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital;

        fundamento.Should().Contain("edital", "é o edital que fixa o prazo e declara a convenção");
        fundamento.Should().Contain("UNI-REQ-0095", "o texto aponta a decisão que o sustenta");
        fundamento.Should().Contain("juridicamente orientada",
            "a qualificação é parte do fundamento, não ressalva de estilo");

        // O texto nega ser jurisprudência, então proibir a palavra reprovaria a negativa
        // junto com a reivindicação. O que não pode aparecer é a afirmação.
        fundamento.Should().Contain("não é parecer formal nem jurisprudência consolidada",
            "o limite do registro é parte do fundamento, não nota de rodapé");

        foreach (string reivindicacao in new[] { "conforme jurisprudência", "segundo jurisprudência", "precedente", "STF", "STJ" })
        {
            fundamento.Should().NotContain(
                reivindicacao,
                $"'{reivindicacao}' atribuiria à decisão uma autoridade que o próprio registro nega ter");
        }

        // O art. 59 é regra geral ressalvada disposição específica, e no âmbito do certame
        // quem prevalece é o edital. Citá-lo aqui inverteria a decisão.
        foreach (string dispositivo in new[] { "art.", "Art.", "§", "Lei nº", "Decreto" })
        {
            fundamento.Should().NotContain(
                dispositivo,
                $"citar '{dispositivo}' apresentaria o dispositivo geral como fonte do prazo do certame, invertendo a decisão");
        }
    }

    [Fact(DisplayName = "O fundamento nega dispor sobre efeito suspensivo, em vez de silenciar")]
    public void BaseLegal_NegaDisporSobreEfeitoSuspensivo()
    {
        // Silêncio deixaria o leitor supor que a decisão do prazo alcança a suspensividade.
        // A negativa é explícita porque a dependência daquela garantia continua aberta.
        string fundamento = AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital;

        fundamento.Should().Contain("dispõe sobre efeito suspensivo",
            "a negativa é explícita — silêncio deixaria supor que a decisão do prazo a alcança");
        fundamento.Should().Contain("UNI-REQ-0117", "a negativa aponta onde a dependência é rastreada");
    }

    [Theory(DisplayName = "Nenhuma configuração congelada referencia a entrada de contagem (fronteira da ADR-0112)")]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)]
    [InlineData(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)]
    [InlineData(AlgoritmoContagemPrazoCodigo.AvancaDataUtil)]
    public async Task NenhumaConfiguracaoCongelada_ReferenciaEntradaDeContagem(string codigo)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        await FronteiraAppendOnlyDoRol.NenhumaReferenciaCongeladaAsync(
            context, codigo, RegraCatalogoSeed.VersaoV1);
    }
}
