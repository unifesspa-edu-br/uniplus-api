namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Guarda o arquivo de seed dos tipos de ato (<c>seeds/seed-tipos-ato.json</c>) contra
/// edições que passariam despercebidas: um código fora do formato, uma janela de
/// vigência incoerente, ou a quebra da invariante da ADR-0103.
/// </summary>
/// <remarks>
/// Estes testes leem o **arquivo real**. Um teste que construísse os dados em memória
/// e os verificasse contra si mesmo passaria por construção, e trocar um valor no JSON
/// não o quebraria — que é exatamente o acidente contra o qual o arquivo precisa de
/// proteção.
/// </remarks>
public sealed partial class SeedTiposAtoTests
{
    [GeneratedRegex(@"^[A-Z]+(_[A-Z]+)*$")]
    private static partial Regex FormatoCodigo();

    private const string CaminhoRelativo = "seeds/seed-tipos-ato.json";

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "O arquivo de seed existe e traz os dezenove tipos de ato")]
    public void Seed_TrazOsDezenoveTipos()
    {
        LinhaSeed[] linhas = Carregar();

        // Toda fase do ciclo que produz resultado precisa de um tipo de ato
        // correspondente para ser configurável num cronograma — sem ele a fase não
        // tem o que declarar como produto, e a invariante da conclusão recusa a
        // publicação (uniplus-api#1436).
        linhas.Should().HaveCount(19);
        linhas.Select(l => l.Codigo).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Todo código do seed respeita o formato que o agregado exige")]
    public void Seed_CodigosNoFormatoUpperSnake()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            FormatoCodigo().IsMatch(linha.Codigo).Should().BeTrue(
                $"'{linha.Codigo}' precisa ser UPPER_SNAKE, senão o POST do seed volta 422");
        }
    }

    [Fact(DisplayName = "Todo tipo do seed nasce com vigência aberta")]
    public void Seed_VigenciaAberta()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            linha.VigenciaInicio.Should().Be("2026-01-01", $"{linha.Codigo} deve compartilhar o início de vigência");
            linha.VigenciaFim.Should().BeNull($"{linha.Codigo} não pode nascer encerrado");
        }
    }

    [Fact(DisplayName = "Todo tipo do seed tem nome em pt-BR, não derivado do código")]
    public void Seed_NomesExplicitos()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            linha.Nome.Should().NotBeNullOrWhiteSpace();
            linha.Nome.Should().NotBe(linha.Codigo, $"{linha.Codigo} precisa de um nome legível, não do próprio código");
            linha.Nome.Length.Should().BeGreaterThanOrEqualTo(2);
        }
    }

    [Fact(DisplayName = "ADR-0103: congela(retificador) == congela(retificado)")]
    public void Seed_RetificadorECongelanteComoORetificado()
    {
        Dictionary<string, LinhaSeed> porCodigo = Carregar().ToDictionary(l => l.Codigo, StringComparer.Ordinal);

        // Não é o rótulo do tipo que protege a integridade da configuração publicada
        // (congelamento dos parâmetros na publicação, UNI-REQ-0019), é a classe de congelamento. Se um edital de abertura congela e a sua
        // retificação não, a retificação deixaria de produzir a nova versão congelada —
        // e nenhum teste de endpoint perceberia.
        porCodigo.Should().ContainKey("EDITAL_ABERTURA");
        porCodigo.Should().ContainKey("EDITAL_RETIFICACAO");

        porCodigo["EDITAL_RETIFICACAO"].CongelaConfiguracao
            .Should().Be(porCodigo["EDITAL_ABERTURA"].CongelaConfiguracao);

        porCodigo["EDITAL_ABERTURA"].CongelaConfiguracao.Should().BeTrue(
            "o edital de abertura é o ato que congela a configuração do certame");
    }

    [Fact(DisplayName = "Só os atos que encerram um resultado têm efeito irreversível")]
    public void Seed_EfeitoIrreversivelRestrito()
    {
        IReadOnlyList<string> irreversiveis =
            [.. Carregar().Where(l => l.EfeitoIrreversivel).Select(l => l.Codigo).Order(StringComparer.Ordinal)];

        // CONVOCACAO passa a integrar a lista (uniplus-api#1432): ela concede o
        // direito de ocupar a vaga, ato que não se desfaz — o UNI-REQ-0080 usa a
        // convocação como exemplo canônico de irreversibilidade.
        // O tipo nomeia a matéria e serve ao preliminar e ao definitivo, então um tipo de
        // resultado não carrega efeito irreversível: o preliminar se corrige e reabre o prazo
        // de recurso.
        irreversiveis.Should().BeEquivalentTo(["CONVOCACAO"]);
    }

    [Fact(DisplayName = "Só os atos que determinam a situação do candidato são resultado")]
    public void Seed_EhResultadoRestrito()
    {
        IReadOnlyList<string> resultado =
            [.. Carregar().Where(l => l.EhResultado).Select(l => l.Codigo).Order(StringComparer.Ordinal)];

        // false é proibição permanente: aquele ato nunca integra ciclo recursal, em
        // edital nenhum. CONVOCACAO e CHAMADA ficam de fora — concedem o direito de
        // ocupar a vaga, e o recurso é contra a habilitação que as antecede, não
        // contra elas. ERRATA fica de fora por decisão consciente: o recurso cabe
        // contra o resultado corrigido, não contra a errata. Os cinco códigos abaixo
        // entram porque cada um determina a situação do candidato na respectiva
        // matéria — isenção, heteroidentificação, habilitação, avaliação
        // biopsicossocial e homologação das inscrições (uniplus-api#1436).
        // Cada tipo nomeia a matéria; o papel (preliminar ou definitivo) é do produto que o
        // cronograma declara, e não do código do tipo.
        resultado.Should().BeEquivalentTo([
            "CONFIRMACAO_INTERESSE",
            "GABARITO",
            "HOMOLOGACAO_ANALISE_DOCUMENTAL",
            "HOMOLOGACAO_HABILITACAO",
            "HOMOLOGACAO_INSCRICOES",
            "HOMOLOGACAO_RECURSOS",
            "LISTA_ESPERA",
            "RESULTADO_AVALIACAO_BIOPSICOSSOCIAL",
            "RESULTADO_CLASSIFICACAO",
            "RESULTADO_HETEROIDENTIFICACAO",
            "RESULTADO_HOMOLOGACAO",
            "RESULTADO_ISENCAO",
        ]);
    }

    [Fact(DisplayName = "Só aviso, comunicado e errata são publicáveis como documento avulso")]
    public void Seed_PublicavelComoAvulsoRestrito()
    {
        LinhaSeed[] linhas = Carregar();

        // Convocação e chamada são do Ingresso, e o resultado e o edital têm fluxo próprio: nenhum
        // deles pode ser publicado como documento avulso, e o catálogo é quem diz isso (ADR-0103).
        linhas.Where(l => l.PublicavelComoAvulso).Select(l => l.Codigo)
            .Should().BeEquivalentTo(["AVISO", "COMUNICADO", "ERRATA"]);
        linhas.Where(l => l.PublicavelComoAvulso).Should().OnlyContain(
            l => !l.CongelaConfiguracao && !l.EhResultado,
            "o documento avulso não congela configuração nem determina a situação do candidato");
        linhas.Where(l => l.Codigo is "CONVOCACAO" or "CHAMADA").Should().OnlyContain(l => !l.PublicavelComoAvulso);
    }

    [Fact(DisplayName = "Só a errata exige o ato corrigido, e ela é publicável como documento avulso")]
    public void Seed_ExigeAtoCorrigidoRestrito()
    {
        LinhaSeed[] linhas = Carregar();

        linhas.Where(l => l.ExigeAtoCorrigido).Select(l => l.Codigo).Should().BeEquivalentTo(["ERRATA"]);
        linhas.Where(l => l.ExigeAtoCorrigido).Should().OnlyContain(l => l.PublicavelComoAvulso);
    }

    [Fact(DisplayName = "Só os atos que o objeto admite uma vez são únicos por objeto")]
    public void Seed_UnicoPorObjetoRestrito()
    {
        IReadOnlyList<string> unicos =
            [.. Carregar().Where(l => l.UnicoPorObjeto).Select(l => l.Codigo).Order(StringComparer.Ordinal)];

        // Isenção, heteroidentificação, habilitação, avaliação biopsicossocial e
        // homologação das inscrições podem recorrer por chamada num certame de
        // múltiplas convocações — únicos por objeto bloquearia uma segunda
        // publicação legítima. Mesma classe de HOMOLOGACAO_ANALISE_DOCUMENTAL e
        // HOMOLOGACAO_RECURSOS, não de HOMOLOGACAO_INSCRICOES.
        unicos.Should().BeEquivalentTo(["EDITAL_ABERTURA", "HOMOLOGACAO_INSCRICOES"]);

        // Único é só o ato que sai uma vez no processo, sem preliminar e definitivo. Os tipos
        // por matéria saem nos dois papéis ou em mais de um ciclo, então nunca são únicos; a
        // garantia deles é a publicação única de cada item declarado no edital. A homologação
        // das inscrições é o único resultado ainda único no seed.
        Carregar().Where(l => l.UnicoPorObjeto && l.EhResultado).Select(l => l.Codigo)
            .Should().BeEquivalentTo(["HOMOLOGACAO_INSCRICOES"]);
        Carregar().Where(l => l.Codigo is "GABARITO" or "RESULTADO_CLASSIFICACAO" or "RESULTADO_ISENCAO")
            .Should().OnlyContain(l => !l.UnicoPorObjeto);
    }

    [Fact(DisplayName = "Os códigos que repetiam o papel no tipo saíram do seed")]
    public void Seed_NaoRepeteOPapelNoCodigoDoTipo()
    {
        string[] codigos = [.. Carregar().Select(l => l.Codigo)];

        codigos.Should().Contain(["GABARITO", "RESULTADO_CLASSIFICACAO", "RESULTADO_ISENCAO"]);
        codigos.Should().NotContain(
        [
            "GABARITO_PRELIMINAR",
            "GABARITO_DEFINITIVO",
            "RESULTADO_PRELIMINAR",
            "RESULTADO_FINAL",
            "RESULTADO_PRELIMINAR_ISENCAO",
        ]);
    }

    private static LinhaSeed[] Carregar()
    {
        // Path.Combine descarta os segmentos anteriores quando um deles é enraizado.
        // O precedente é OpenApiEndpointTests.ResolveRepoPath, que valida o mesmo.
        Path.IsPathRooted(CaminhoRelativo).Should().BeFalse(
            "o caminho do seed é relativo à raiz do repositório");

        string caminho = Path.Combine(SolutionRootLocator.Locate(), CaminhoRelativo);

        File.Exists(caminho).Should().BeTrue($"o seed dos tipos de ato precisa existir em {CaminhoRelativo}");

        LinhaSeed[]? linhas = JsonSerializer.Deserialize<LinhaSeed[]>(File.ReadAllText(caminho), Opcoes);

        linhas.Should().NotBeNull();
        return linhas!;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada por System.Text.Json ao desserializar o arquivo de seed.")]
    private sealed record LinhaSeed(
        string Codigo,
        string Nome,
        bool CongelaConfiguracao,
        bool UnicoPorObjeto,
        bool EfeitoIrreversivel,
        bool EhResultado,
        bool PublicavelComoAvulso,
        bool ExigeAtoCorrigido,
        string VigenciaInicio,
        string? VigenciaFim,
        string? BaseLegal);
}
