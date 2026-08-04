namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness test da issue #850: nenhuma regra do módulo Seleção ramifica comportamento pelo
/// membro concreto de <c>TipoProcesso</c> — o comportamento vem da configuração declarada
/// (ex.: <c>ConfiguracaoClassificacao.BaseadoEmEnem</c>), nunca do rótulo de tipo do processo.
/// Mesma filosofia de <c>PublicacoesSemRamificacaoPorTipoAtoTests</c> (ADR-0103): regex pura
/// sobre texto, sem ArchUnitNET nem Roslyn — o projeto de testes não referencia
/// <c>Microsoft.CodeAnalysis.CSharp</c>, e este detector herda conscientemente a mesma
/// limitação do precedente ("detecção posicional deixa passar formas sintáticas
/// equivalentes" — <c>array.Contains(Tipo)</c>, <c>processo is { Tipo: TipoProcesso.SiSU }</c>,
/// despacho por dicionário, alias de <c>using</c> não são detectados).
/// </summary>
/// <remarks>
/// <para>
/// O que é proibido: <c>TipoProcesso.&lt;Membro&gt;</c> em posição de comparação/pattern-match
/// (<c>is</c>, <c>is not</c>, <c>==</c>, <c>!=</c>, <c>case</c>, braço de switch-expression
/// <c>=&gt;</c>, <c>.Equals(...)</c>) — <b>exceto</b> comparações com o sentinela
/// <c>TipoProcesso.Nenhum</c>, que são validação de campo obrigatório (mesmo padrão de
/// <c>OrigemCandidatos.Nenhuma</c>), não ramificação de comportamento.
/// </para>
/// <para>
/// O que continua legítimo: seleção declarativa por <c>Tipo</c> como CHAVE de lookup — ex.
/// <c>processo.Tipo.ToString()</c> para buscar <c>ObrigatoriedadeLegal</c> por tipo no
/// cadastro (ADR-0114) — não é comparação com um membro específico. O detector não acusa
/// <c>.ToString()</c>, passagem de <c>Tipo</c> como parâmetro nem factory/DTO que apenas
/// recebe <c>TipoProcesso.X</c> como argumento posicional.
/// </para>
/// </remarks>
public sealed partial class SelecaoSemRamificacaoPorTipoProcessoTests
{
    /// <summary>
    /// Casa <c>TipoProcesso.&lt;Membro&gt;</c> em qualquer uma das posições de
    /// comparação/pattern-match — cada alternativa cobre uma forma sintática da tabela de
    /// canários. O nome do membro é capturado no grupo nomeado <c>m</c> para permitir a
    /// exceção do sentinela <c>Nenhum</c> depois do match.
    /// </summary>
    [GeneratedRegex(
        """(?:(?:==|!=)\s*TipoProcesso\.(?<m>\w+))|(?:TipoProcesso\.(?<m>\w+)\s*(?:==|!=))|(?:\bis(?:\s+not)?\s+TipoProcesso\.(?<m>\w+))|(?:\bcase\s+TipoProcesso\.(?<m>\w+)\s*:)|(?:TipoProcesso\.(?<m>\w+)\s*=>)|(?:\.Equals\(\s*TipoProcesso\.(?<m>\w+))""")]
    private static partial Regex RamificacaoPorTipoProcesso();

    private const string Sentinela = "Nenhum";

    [Theory(DisplayName = "O detector acusa a ramificação por TipoProcesso escrita de qualquer forma — canários")]
    [InlineData("bool baseadoEmEnem = Tipo is TipoProcesso.SiSU or TipoProcesso.PSVR;")]
    [InlineData("if (Tipo == TipoProcesso.SiSU) { return true; }")]
    [InlineData("if (TipoProcesso.SiSU == Tipo) { return true; }")]
    [InlineData("if (Tipo != TipoProcesso.PSIQ) { return false; }")]
    [InlineData("switch (Tipo) { case TipoProcesso.SiSU: return 1; }")]
    [InlineData("return Tipo switch { TipoProcesso.SiSU => true, _ => false };")]
    [InlineData("if (Tipo.Equals(TipoProcesso.SiSU)) { return true; }")]
    public void Detector_AcusaRamificacaoPorTipoProcesso_TodasAsFormas(string canario)
    {
        ArgumentNullException.ThrowIfNull(canario);

        Violacoes(canario).Should().NotBeEmpty(
            "cada canário é o mesmo ramo por TipoProcesso, escrito de um jeito que uma detecção " +
            "posicional ingênua deixaria passar");
    }

    [Theory(DisplayName = "O detector não acusa o sentinela Nenhum, construção nem seleção declarativa por chave")]
    [InlineData("if (tipo == TipoProcesso.Nenhum) { throw new ArgumentException(); }")]
    [InlineData("if (Tipo != TipoProcesso.Nenhum) { return; }")]
    [InlineData(@"RuleFor(x => x.Tipo).NotEqual(TipoProcesso.Nenhum);")]
    [InlineData(@"ProcessoSeletivo.Criar(""PS 2026"", TipoProcesso.SiSU, origem, id, snapshot);")]
    [InlineData("public TipoProcesso Tipo { get; private set; }")]
    [InlineData(@"new CriarProcessoSeletivoCommand(nome, TipoProcesso.PSVR);")]
    [InlineData("string chave = processo.Tipo.ToString();")]
    public void Detector_NaoAcusaSentinelaConstrucaoOuSelecaoDeclarativa(string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);

        Violacoes(trecho).Should().BeEmpty(
            "sentinela Nenhum, factory/DTO recebendo TipoProcesso como argumento, declaração de " +
            "propriedade e seleção por Tipo.ToString() como chave de lookup não são ramificação " +
            "de comportamento");
    }

    [Fact(DisplayName = "Selecao.Domain e Selecao.Application reais: zero ramificação por TipoProcesso")]
    public void Selecao_NaoRamificaPorTipoProcesso()
    {
        string raizDoRepo = RaizDoRepo();
        string[] camadas =
        [
            Path.Combine(raizDoRepo, "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain"),
            Path.Combine(raizDoRepo, "src", "selecao", "Unifesspa.UniPlus.Selecao.Application"),
        ];

        List<string> violacoes = [];
        int arquivosExaminados = 0;

        foreach (string raiz in camadas)
        {
            Directory.Exists(raiz).Should().BeTrue($"a camada '{raiz}' precisa existir para ser varrida");

            foreach (string arquivo in ArquivosFonte(raiz))
            {
                arquivosExaminados++;
                string codigo = SemComentarios(File.ReadAllText(arquivo));

                foreach (string membro in MembrosViolados(codigo))
                {
                    violacoes.Add($"{Path.GetFileName(arquivo)}: TipoProcesso.{membro}");
                }
            }
        }

        arquivosExaminados.Should().BeGreaterThan(10, "as duas camadas juntas têm dezenas de arquivos");

        violacoes.Should().BeEmpty(
            "TipoProcesso é rótulo, nunca ramo de comportamento — a decisão vem da configuração " +
            "declarada (ex.: ConfiguracaoClassificacao.BaseadoEmEnem, issue #850), exceto pelo " +
            "sentinela Nenhum (validação de campo obrigatório)");
    }

    private static IEnumerable<string> MembrosViolados(string codigo)
    {
        foreach (Match match in RamificacaoPorTipoProcesso().Matches(codigo))
        {
            string membro = match.Groups["m"].Value;
            if (!string.Equals(membro, Sentinela, StringComparison.Ordinal))
            {
                yield return membro;
            }
        }
    }

    private static IReadOnlyList<string> Violacoes(string codigo) => [.. MembrosViolados(SemComentarios(codigo))];

    /// <summary>Remove comentários de linha e de bloco — mesma técnica do precedente de Publicações.</summary>
    private static string SemComentarios(string codigo)
    {
        System.Text.StringBuilder sb = new();
        bool emBloco = false;

        foreach (string linhaOriginal in codigo.Split('\n'))
        {
            string atual = linhaOriginal;

            if (emBloco)
            {
                int fim = atual.IndexOf("*/", StringComparison.Ordinal);
                if (fim < 0)
                {
                    sb.AppendLine();
                    continue;
                }

                atual = atual[(fim + 2)..];
                emBloco = false;
            }

            int abre = atual.IndexOf("/*", StringComparison.Ordinal);
            if (abre >= 0)
            {
                int fecha = atual.IndexOf("*/", abre + 2, StringComparison.Ordinal);
                if (fecha < 0)
                {
                    emBloco = true;
                    atual = atual[..abre];
                }
                else
                {
                    atual = atual[..abre] + atual[(fecha + 2)..];
                }
            }

            int linhaComentario = atual.IndexOf("//", StringComparison.Ordinal);
            if (linhaComentario >= 0)
            {
                atual = atual[..linhaComentario];
            }

            sb.AppendLine(atual);
        }

        return sb.ToString();
    }

    private static IEnumerable<string> ArquivosFonte(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RaizDoRepo([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", ".."));
}
