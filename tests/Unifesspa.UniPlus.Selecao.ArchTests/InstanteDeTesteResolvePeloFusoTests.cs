namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Issue #1376: nos testes cujo resultado depende do dia civil, o instante é construído a partir da
/// zona institucional resolvida — nunca de um offset escrito à mão.
/// </summary>
/// <remarks>
/// <para>
/// A base de fusos registra 27 períodos de horário de verão em <c>America/Belem</c>, o último
/// encerrado em 06/02/1988. Offset fixo não é frágil apenas diante de um decreto futuro: dentro
/// daqueles períodos a zona esteve em <c>-02:00</c>.
/// </para>
/// <para>
/// <b>O que garante a correção não é este gate</b>, e sim duas asserções que falham quando a
/// derivação está errada, independentemente de como o instante foi escrito: a assimétrica de
/// <c>DadosEditalTests</c> — mesmo instante, dia 11 em UTC e dia 10 na zona, ambos contra literais —
/// e <see cref="Helper_ResolvePelaZonaEmCadaEpoca"/>, que exercita o helper numa data em que Belém
/// não estava em UTC-3. Este gate impede a reincidência distraída: a forma que alguém reescreveria
/// sem pensar.
/// </para>
/// <para>
/// <b>Até onde vai.</b> São detectores textuais, e o alcance termina na expressão. Offset ou valor
/// esperado que passem por variável intermediária não são acusados: rastrear a origem de um valor
/// exige análise semântica — Roslyn —, que o projeto decidiu não adotar para gates. Perseguir cada
/// grafia por regex é corrida sem fim, e o limite fica declarado aqui em vez de fingido em mais um
/// padrão. <b>O canário vem primeiro</b>, como em
/// <see cref="CronogramaSemLiteralInstitucionalTests"/>: cada detector prova que acusa um trecho
/// plantado antes de o silêncio dele significar alguma coisa.
/// </para>
/// </remarks>
public sealed class InstanteDeTesteResolvePeloFusoTests
{
    /// <summary>
    /// Os testes cujo instante alimenta derivação de dia civil. Lista fechada de propósito: nos
    /// demais arquivos o instante é só um momento qualquer (hash canônico, ordenação, criadoEm) e
    /// trocá-lo seria ruído sem ganho (CA-04 da issue).
    /// </summary>
    private static readonly string[] ArquivosAlvo =
    [
        Path.Join("Unifesspa.UniPlus.Selecao.Domain.UnitTests", "ValueObjects", "DadosEditalTests.cs"),
        Path.Join("Unifesspa.UniPlus.Selecao.Domain.UnitTests", "Entities", "JanelaDeSolicitacaoDeIsencaoTests.cs"),
        Path.Join("Unifesspa.UniPlus.Selecao.Application.UnitTests", "Queries", "ObterConformidadeLegalProcessoSeletivoQueryHandlerTests.cs"),
        Path.Join("Unifesspa.UniPlus.Selecao.IntegrationTests", "ProcessosSeletivos", "ConformidadeLegalCongelamentoPersistenciaTests.cs"),
    ];

    private static readonly string[] Helper = [Path.Join("Shared", "InstanteEmBelem.cs")];

    /// <summary>Construção de instante com o offset escrito à mão no último argumento.</summary>
    /// <remarks>
    /// Aceita a forma target-typed (<c>DateTimeOffset x = new(..., offset)</c>) e argumento nomeado,
    /// além da explícita. Ancora no <c>TimeSpan</c>, então chamada comum que recebe duração —
    /// <c>IniciarPendente(id, clock, TimeSpan.FromMinutes(15))</c>, que existe num dos alvos — não
    /// casa, por não ser construção.
    /// </remarks>
    private static readonly Regex ConstrucaoComOffsetLiteral = new(
        @"new\s*(?:DateTimeOffset)?\s*\([^;)]*,\s*(?:\w+\s*:\s*)?(?<offset>TimeSpan\.\w+|new\s+TimeSpan)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex OffsetFixo = new(
        @"TimeSpan\.FromHours\(\s*-\s*3\s*\)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Assertiva que deriva o esperado pela mesma zona, em vez de compará-lo com literal.</summary>
    private static readonly Regex AssertivaTautologica = new(
        @"Should\(\)\s*\.\s*Be\([^;]*?(InstanteEmBelem\.|Belem\.|\.GetUtcOffset\()",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    [Fact(DisplayName = "Nos alvos, offset diferente de UTC vem do helper")]
    public void Alvos_NaoEscrevemOffsetAMao()
    {
        // Enumerar as grafias de UTC-3 (FromHours(-3), FromMinutes(-180), new TimeSpan(-3,0,0)...) é
        // corrida sem fim. A regra é invertida: o único offset que o teste escreve à mão é
        // TimeSpan.Zero, porque afirmar sobre UTC é legítimo e não depende da zona.
        List<string> achados = [];

        foreach (string caminho in CaminhosDe(ArquivosAlvo))
        {
            string conteudo = SemComentarios(caminho);

            foreach (Match encontro in ConstrucaoComOffsetLiteral.Matches(conteudo))
            {
                string offset = encontro.Groups["offset"].Value.Trim();
                if (offset is not "TimeSpan.Zero")
                {
                    achados.Add($"{Path.GetFileName(caminho)}: offset \"{offset}\"");
                }
            }
        }

        achados.Should().BeEmpty(
            "nesses testes o instante vem de InstanteEmBelem; escrever o offset à mão só é permitido "
                + $"para TimeSpan.Zero, que afirma sobre UTC. Achados: {string.Join(" | ", achados)}");
    }

    [Fact(DisplayName = "Nenhuma assertiva dos alvos deriva o esperado pela própria zona")]
    public void Alvos_NaoTemAssertivaTautologica()
    {
        List<string> achados = [];

        foreach (string caminho in CaminhosDe(ArquivosAlvo))
        {
            string conteudo = SemComentarios(caminho);

            foreach (Match encontro in AssertivaTautologica.Matches(conteudo))
            {
                achados.Add($"{Path.GetFileName(caminho)}: {Normalizar(encontro.Value)}");
            }
        }

        achados.Should().BeEmpty(
            "a assertiva precisa comparar contra valor literal: derivar os dois lados pela mesma zona "
                + $"passa mesmo com a produção errada. Achados: {string.Join(" | ", achados)}");
    }

    [Fact(DisplayName = "O helper não escreve o offset à mão")]
    public void Helper_NaoUsaOffsetFixo()
    {
        // Nos alvos quem cobre isso é a regra invertida, que não depende da grafia. Aqui sobra o
        // helper, onde construir com offset resolvido é legítimo e só o literal é proibido —
        // escrevê-lo aqui anularia a troca em todos os arquivos de uma vez.
        List<string> achados = [.. CaminhosDe(Helper)
            .Where(caminho => OffsetFixo.IsMatch(SemComentarios(caminho)))
            .Select(Path.GetFileName)
            .OfType<string>()];

        achados.Should().BeEmpty($"o helper resolve o offset pela zona. Arquivos: {string.Join(", ", achados)}");
    }

    [Theory(DisplayName = "O helper resolve pela zona em cada época, não por um offset fixo disfarçado")]
    [InlineData(2026, 1, 1, -3)]
    [InlineData(1985, 11, 3, -2)]
    public void Helper_ResolvePelaZonaEmCadaEpoca(int ano, int mes, int dia, int offsetEsperado)
    {
        // Único ponto do repositório onde o offset literal é legítimo: é a assertiva que o VERIFICA.
        // O caso de 1985 é o que dá sentido à troca — naquele dia Belém estava em UTC-2, e um helper
        // que devolvesse -03:00 fixo passaria em todos os arquivos alvo, porque todos usam 2026.
        TimeSpan esperado = TimeSpan.FromHours(offsetEsperado);
        DateTime local = new(ano, mes, dia, 12, 0, 0, DateTimeKind.Unspecified);

        InstanteEmBelem.OffsetEm(local).Should().Be(
            esperado, "o offset sai da base de fusos: 03/11/1985 cai num período de horário de verão em Belém");
        InstanteEmBelem.Em(ano, mes, dia, 12, 0, 0).Offset.Should().Be(
            esperado, "o instante nasce do offset vigente na data, não de uma constante");
    }

    [Theory(DisplayName = "O detector de construção ACUSA offset escrito à mão (canário obrigatório)")]
    [InlineData("""new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.FromHours(-3))""")]
    [InlineData("""new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.FromMinutes(-180))""")]
    [InlineData("""DateTimeOffset inicio = new(2026, 3, 2, 0, 0, 0, new TimeSpan(-3, 0, 0));""")]
    [InlineData("""DateTimeOffset inicio = new(2026, 3, 2, 0, 0, 0, offset: TimeSpan.FromMinutes(-180));""")]
    public void DetectorDeConstrucao_AcusaCanario(string trechoPlantado)
    {
        Match encontro = ConstrucaoComOffsetLiteral.Match(trechoPlantado);

        encontro.Success.Should().BeTrue($"a construção precisa ser reconhecida. Trecho: {trechoPlantado}");
        encontro.Groups["offset"].Value.Trim().Should().NotBe("TimeSpan.Zero");
    }

    [Theory(DisplayName = "O detector de construção aceita UTC e ignora duração de chamada comum")]
    [InlineData("""new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero)""", true)]
    [InlineData("""DateTimeOffset inicio = new(2026, 1, 1, 3, 0, 0, TimeSpan.Zero);""", true)]
    [InlineData("""DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));""", false)]
    [InlineData("""DateTimeOffset inicio = InstanteEmBelem.Em(2026, 3, 10, 22, 0, 0);""", false)]
    public void DetectorDeConstrucao_NaoAcusaLegitimo(string trecho, bool ehConstrucaoUtc)
    {
        Match encontro = ConstrucaoComOffsetLiteral.Match(trecho);

        encontro.Success.Should().Be(ehConstrucaoUtc, $"Trecho: {trecho}");

        if (ehConstrucaoUtc)
        {
            encontro.Groups["offset"].Value.Trim().Should().Be("TimeSpan.Zero");
        }
    }

    [Theory(DisplayName = "O detector de tautologia ACUSA uma assertiva plantada (canário obrigatório)")]
    [InlineData("""dados.Fim.Should().Be(InstanteEmBelem.Em(2026, 3, 7, 23, 59, 59));""")]
    [InlineData("""resultado.Should().Be(new DateTimeOffset(local, Belem.GetUtcOffset(local)));""")]
    [InlineData("dados.Fim.Should().Be(\n            InstanteEmBelem.Em(2026, 3, 7, 23, 59, 59));")]
    public void DetectorDeTautologia_AcusaCanario(string trechoPlantado) =>
        AssertivaTautologica.IsMatch(trechoPlantado).Should().BeTrue(
            $"o detector precisa acusar assertiva que deriva o esperado pela zona. Trecho: {trechoPlantado}");

    [Theory(DisplayName = "O detector de tautologia não acusa assertiva contra literal")]
    [InlineData("""dados.DiaDeReferenciaLegal(belem).Should().Be(new DateOnly(2026, 3, 10));""")]
    [InlineData("""pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias");""")]
    public void DetectorDeTautologia_NaoAcusaLiteral(string trecho) =>
        AssertivaTautologica.IsMatch(trecho).Should().BeFalse($"compara contra literal. Trecho: {trecho}");

    /// <remarks>
    /// Alvo ausente <b>falha</b>, em vez de sair da lista em silêncio: a lista é fechada de
    /// propósito, e um arquivo renomeado deixaria o gate verde cobrindo menos do que promete.
    /// </remarks>
    private static IEnumerable<string> CaminhosDe(string[] relativos)
    {
        foreach (string relativo in relativos)
        {
            string caminho = Path.Join(RaizDosTestes(), relativo);
            File.Exists(caminho).Should().BeTrue(
                $"o alvo {relativo} saiu do lugar; atualize a lista em vez de deixar o gate varrer um "
                    + "arquivo a menos sem avisar");

            yield return caminho;
        }
    }

    /// <summary>Uma nota explicando a ausência do padrão não pode ser lida como sua presença.</summary>
    private static string SemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>Colapsa a quebra de linha para o achado caber numa mensagem de falha legível.</summary>
    private static string Normalizar(string trecho) =>
        string.Join(' ', trecho.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string RaizDosTestes([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, ".."));
}
