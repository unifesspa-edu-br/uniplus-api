namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Globalization;
using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Guarda o arquivo de seed do calendário de dias úteis
/// (<c>seeds/seed-calendario-dias-uteis.json</c>) contra edições que passariam
/// despercebidas: uma abrangência fora do vocabulário fechado, uma referência
/// territorial incoerente, ou uma data duplicada.
/// </summary>
/// <remarks>
/// Mesmo espírito de <see cref="SeedTiposAtoTests"/>: lê o arquivo real, não uma
/// cópia em memória — trocar um valor no JSON precisa quebrar algum destes testes.
/// </remarks>
public sealed class SeedCalendarioDiasUteisTests
{
    private const string CaminhoRelativo = "seeds/seed-calendario-dias-uteis.json";
    private const string IbgeDeMaraba = "1504208";
    private const int DescricaoMaxLength = 200;

    private static readonly string[] AbrangenciasValidas = ["NACIONAL", "ESTADUAL", "MUNICIPAL", "INSTITUCIONAL"];
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "O arquivo de seed existe e traz os dezenove dias não úteis do dataset de referência")]
    public void Seed_TrazOsDezenoveDias()
    {
        Carregar().DiasNaoUteis.Should().HaveCount(19);
    }

    [Fact(DisplayName = "Toda abrangência do seed está no vocabulário fechado, em UPPER_SNAKE")]
    public void Seed_AbrangenciaNoVocabularioFechado()
    {
        foreach (DiaSeed dia in Carregar().DiasNaoUteis)
        {
            AbrangenciasValidas.Should().Contain(
                dia.Abrangencia, $"{dia.Data}: '{dia.Abrangencia}' não é abrangência que o agregado aceita");
        }
    }

    [Fact(DisplayName = "UF só aparece em dia de abrangência estadual, e sempre presente nela")]
    public void Seed_UfSoParaEstadual()
    {
        foreach (DiaSeed dia in Carregar().DiasNaoUteis)
        {
            if (dia.Abrangencia == "ESTADUAL")
            {
                dia.Uf.Should().NotBeNullOrWhiteSpace($"{dia.Data} é estadual e precisa declarar a UF");
            }
            else
            {
                dia.Uf.Should().BeNull($"{dia.Data} não é estadual — UF só se aplica a essa abrangência");
            }
        }
    }

    [Fact(DisplayName = "A referência de município só aparece em dia municipal, e sempre completa")]
    public void Seed_ReferenciaMunicipalSoParaMunicipal()
    {
        foreach (DiaSeed dia in Carregar().DiasNaoUteis)
        {
            if (dia.Abrangencia == "MUNICIPAL")
            {
                dia.MunicipioIbge.Should().Be(IbgeDeMaraba, $"{dia.Data}: só Marabá está no escopo desta issue");
                dia.MunicipioNome.Should().NotBeNullOrWhiteSpace($"{dia.Data} precisa do nome do município");
                dia.MunicipioUf.Should().Be("PA", $"{dia.Data} é município do Pará");
            }
            else
            {
                dia.MunicipioIbge.Should().BeNull($"{dia.Data} não é municipal — referência de município não se aplica");
                dia.MunicipioNome.Should().BeNull($"{dia.Data} não é municipal — referência de município não se aplica");
                dia.MunicipioUf.Should().BeNull($"{dia.Data} não é municipal — referência de município não se aplica");
            }
        }
    }

    [Fact(DisplayName = "Só o aniversário de Marabá é feriado municipal — o restante do Anexo Único é ponto facultativo")]
    public void Seed_ApenasAniversarioDeMarabaEMunicipal()
    {
        // Trava o conjunto exato, não só a forma: 19/out (Pós-Círio) é a mesma cor de
        // ponto facultativo que 28/out e o Carnaval no Anexo Único da Portaria
        // Unifesspa nº 058/2026 — feriado municipal de verdade é verde, como 05/abr.
        // Uma reclassificação futura de qualquer um dos dois só passa aqui se for
        // deliberada o bastante para editar também esta lista.
        DatasetSeed dataset = Carregar();

        IReadOnlyList<string> municipais =
            [.. dataset.DiasNaoUteis.Where(d => d.Abrangencia == "MUNICIPAL").Select(d => d.Data).Order(StringComparer.Ordinal)];
        municipais.Should().BeEquivalentTo(["2026-04-05"]);

        IReadOnlyList<string> estaduais =
            [.. dataset.DiasNaoUteis.Where(d => d.Abrangencia == "ESTADUAL").Select(d => d.Data).Order(StringComparer.Ordinal)];
        estaduais.Should().BeEquivalentTo(["2026-08-15"]);
    }

    [Fact(DisplayName = "Nenhum dia não útil está duplicado")]
    public void Seed_SemDataDuplicada()
    {
        (string Data, string Abrangencia, string? MunicipioIbge, string? Uf)[] chaves =
            [.. Carregar().DiasNaoUteis.Select(d => (d.Data, d.Abrangencia, d.MunicipioIbge, d.Uf))];

        chaves.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Toda descrição do seed é não vazia e respeita o limite do agregado")]
    public void Seed_DescricaoDentroDoLimite()
    {
        foreach (DiaSeed dia in Carregar().DiasNaoUteis)
        {
            dia.Descricao.Should().NotBeNullOrWhiteSpace();
            dia.Descricao.Length.Should().BeLessThanOrEqualTo(
                DescricaoMaxLength, $"{dia.Data}: descrição maior que o agregado aceita");
        }
    }

    [Fact(DisplayName = "Toda data do dataset pertence ao ano de referência (2026)")]
    public void Seed_TodaDataEmDoisMilESeis()
    {
        foreach (DiaSeed dia in Carregar().DiasNaoUteis)
        {
            DateOnly.Parse(dia.Data, CultureInfo.InvariantCulture).Year.Should().Be(
                2026, $"{dia.Data} deveria pertencer ao dataset de referência 2026");
        }
    }

    private static DatasetSeed Carregar()
    {
        // Path.Combine descarta os segmentos anteriores quando um deles é enraizado.
        // O precedente é OpenApiEndpointTests.ResolveRepoPath, que valida o mesmo.
        Path.IsPathRooted(CaminhoRelativo).Should().BeFalse(
            "o caminho do seed é relativo à raiz do repositório");

        string caminho = Path.Combine(SolutionRootLocator.Locate(), CaminhoRelativo);

        File.Exists(caminho).Should().BeTrue($"o seed do calendário de dias úteis precisa existir em {CaminhoRelativo}");

        DatasetSeed[]? datasets = JsonSerializer.Deserialize<DatasetSeed[]>(File.ReadAllText(caminho), Opcoes);

        datasets.Should().NotBeNull();
        datasets!.Should().ContainSingle("o arquivo semeia um único dataset de calendário de referência");
        return datasets[0];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada por System.Text.Json ao desserializar o arquivo de seed.")]
    private sealed record DatasetSeed(string VersaoDataset, DiaSeed[] DiasNaoUteis);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada por System.Text.Json ao desserializar o arquivo de seed.")]
    private sealed record DiaSeed(
        string Abrangencia,
        string? MunicipioIbge,
        string? MunicipioNome,
        string? MunicipioUf,
        string? Uf,
        string Data,
        string Descricao);
}
