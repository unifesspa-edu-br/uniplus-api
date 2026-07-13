namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// <b>Repor configuração congelada sem provar que se repôs é proibido</b> (Story #859;
/// ADR-0110 D1/D2).
/// </summary>
/// <remarks>
/// <para>
/// <c>ProcessoSeletivo.RestaurarConfiguracaoCongelada</c> aceita um
/// <c>GrafoConfiguracao</c> e uma <c>VersaoConfiguracao</c> — e valida que a versão é
/// <b>do processo</b>, mas <b>não</b> tem como saber que o grafo veio <b>daquela</b>
/// versão: o Domain não canonicaliza (ADR-0042). Um grafo inventado, acompanhado de uma
/// versão legítima, passaria.
/// </para>
/// <para>
/// O que fecha essa porta é a <b>prova de round-trip</b>, e ela só existe em
/// <c>RestauradorDeConfiguracao</c> (Application), onde o codec e o agregado coexistem.
/// Este fitness é o que garante que o caminho <b>não</b> seja contornado — a mesma
/// mecânica que a D4 da ADR-0110 escolheu para o carregamento de mutação: um teste que
/// prova que todo caller passa pela porta certa.
/// </para>
/// </remarks>
public sealed class RestauracaoSempreProvadaTests
{
    private const string MetodoDoDominio = "RestaurarConfiguracaoCongelada";

    /// <summary>
    /// Os três lugares onde o nome do método pode aparecer legitimamente em <c>src/</c> —
    /// declarados como caminhos relativos com <c>/</c>, e comparados contra o caminho do
    /// arquivo já normalizado (<see cref="Normalizar"/>).
    /// </summary>
    private static readonly string[] CallersAutorizados =
    [
        // A própria declaração, no agregado.
        "Unifesspa.UniPlus.Selecao.Domain/Entities/ProcessoSeletivo.cs",

        // O único caller: decodifica, repõe e PROVA — numa operação só.
        "Unifesspa.UniPlus.Selecao.Application/Services/RestauradorDeConfiguracao.cs",

        // A porta que o descreve.
        "Unifesspa.UniPlus.Selecao.Application/Abstractions/IRestauradorDeConfiguracao.cs",
    ];

    /// <summary>Caminho com separador <c>/</c>, para comparar do mesmo jeito em qualquer host.</summary>
    private static string Normalizar(string caminho) => caminho.Replace('\\', '/');

    [Fact(DisplayName = "Nenhum código de produção repõe configuração congelada sem passar pela prova de round-trip")]
    public void RestaurarConfiguracao_SoPeloRestaurador()
    {
        // O detector procura o IDENTIFICADOR, não a sintaxe de chamada. Casar apenas
        // `.Metodo(` deixaria a porta aberta pelo lado: capturar o método como method group
        // (`Func<...> f = processo.RestaurarConfiguracaoCongelada;`) e invocar o delegate
        // pularia a prova e passaria no teste. Procurar o nome nu não tem esse ponto cego —
        // e a chance de falso positivo (a palavra aparecer por outro motivo) é preferível a
        // um falso negativo numa invariante com peso jurídico.
        Regex chamada = new(
            $@"\b{MetodoDoDominio}\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        List<string> infratores = [];

        foreach (string arquivo in Directory.EnumerateFiles(RaizDoSrc(), "*.cs", SearchOption.AllDirectories))
        {
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (CallersAutorizados.Any(autorizado => Normalizar(arquivo).EndsWith(autorizado, StringComparison.Ordinal)))
            {
                continue;
            }

            if (chamada.IsMatch(CodigoSemComentarios(arquivo)))
            {
                infratores.Add(Path.GetRelativePath(RaizDoSrc(), arquivo));
            }
        }

        infratores.Should().BeEmpty(
            $"'{MetodoDoDominio}' repõe as seis dimensões da configuração a partir de um grafo que o agregado NÃO " +
            "consegue autenticar — ele não canonicaliza (ADR-0042), então não sabe se aquele grafo veio mesmo " +
            "daquela versão. Quem fecha essa porta é IRestauradorDeConfiguracao, que decodifica, repõe e PROVA o " +
            "round-trip byte-a-byte antes de aceitar. Chamar o método do domínio direto pula a prova — que é a " +
            "única coisa que impede um descarte de destruir configuração em silêncio (ADR-0110). " +
            $"Infratores: {string.Join(", ", infratores)}");
    }

    /// <summary>
    /// A detecção é por <b>literal</b>, e este teste prova que ela detecta: um regex que não
    /// casasse com nada passaria em silêncio e o fitness inteiro seria decoração.
    /// </summary>
    [Fact(DisplayName = "O detector funciona — ele encontra a chamada legítima no RestauradorDeConfiguracao")]
    public void Detector_EncontraOCallerLegitimo()
    {
        string restaurador = Path.Join(
            RaizDoSrc(),
            "selecao",
            "Unifesspa.UniPlus.Selecao.Application",
            "Services",
            "RestauradorDeConfiguracao.cs");

        File.Exists(restaurador).Should().BeTrue();

        File.ReadAllText(restaurador).Should().Contain($".{MetodoDoDominio}(",
            "se o caller legítimo deixar de existir (renomeado, movido), o fitness acima passaria a proteger nada — " +
            "e a ausência de infratores seria indistinguível da ausência de detecção");
    }

    /// <summary>
    /// E o caller legítimo tem de <b>provar</b> — não basta ele existir. Um
    /// <c>RestauradorDeConfiguracao</c> que repusesse sem recanonicalizar passaria no fitness
    /// acima e deixaria a Feature inteira sem a sua única garantia.
    /// </summary>
    [Fact(DisplayName = "O caller legítimo recanonicaliza e compara com os bytes congelados — a reposição é PROVADA")]
    public void CallerLegitimo_Prova()
    {
        string restaurador = Path.Join(
            RaizDoSrc(),
            "selecao",
            "Unifesspa.UniPlus.Selecao.Application",
            "Services",
            "RestauradorDeConfiguracao.cs");

        string fonte = File.ReadAllText(restaurador);

        fonte.Should().Contain("Recodificar(",
            "sem recanonicalizar, não há prova — e uma reidratação sem prova é a que destrói configuração em silêncio");
        fonte.Should().Contain("ConfiguracaoCongeladaCanonica",
            "a prova é a comparação com os BYTES congelados; comparar com outra coisa não prova nada");
        fonte.Should().Contain("SombraParaVerificacao(",
            "a prova roda ANTES de tocar a raiz viva — provar depois deixaria o agregado tracked empobrecido quando " +
            "a prova falhasse, e a atomicidade dependeria de o handler lembrar de não salvar");
    }

    /// <summary>
    /// Descarta as linhas de comentário antes de procurar o identificador. Um
    /// <c>&lt;see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/&gt;</c> num XML doc
    /// é <b>documentação</b>, não uma chamada — tratá-lo como infração faria o teste
    /// castigar justamente quem se deu ao trabalho de explicar a regra.
    /// </summary>
    private static string CodigoSemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string RaizDoSrc([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", "..", "src"));
}
