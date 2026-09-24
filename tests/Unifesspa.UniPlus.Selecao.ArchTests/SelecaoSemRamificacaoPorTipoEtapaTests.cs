namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness test da issue #1071: trava as duas regressões possíveis do defeito original em
/// <c>AvaliadorConformidadeLegal.AvaliarEtapaObrigatoria</c> (comparação por
/// <c>EtapaProcesso.Nome</c>, texto livre editável, em vez do código congelado do tipo) — (a)
/// o enum órfão <c>TipoEtapa</c> voltar a existir como fonte de verdade em
/// <c>Selecao.Domain/Enums</c>, e (b) o corpo do método voltar a acessar <c>.Nome</c>.
/// Trava também a volta da classe <c>TipoEtapaCodigo</c> e do literal <c>"NOTA_ENEM"</c> em
/// <c>src/selecao</c>: a etapa de nota do ENEM é reconhecida pelo atributo congelado no
/// snapshot da etapa (ADR-0123, Emenda 2). Outro código de tipo escrito como literal escapa
/// da varredura, e é revisão que o pega.
/// Mesma filosofia regex-sobre-texto de <c>SelecaoSemRamificacaoPorTipoProcessoTests</c>
/// (ADR-0103) — sem ArchUnitNET nem Roslyn.
/// </summary>
/// <remarks>
/// A varredura cobre o CORPO INTEIRO do método (do início da assinatura até a assinatura do
/// próximo método <c>Avaliar*</c> em <c>AvaliadorConformidadeLegal</c>), não uma única linha —
/// escapa de refactors triviais como quebrar a chamada em múltiplas linhas ou introduzir uma
/// variável local para o valor de <c>e.Nome</c>. Limitação documentada: extrair a comparação
/// para um método auxiliar em OUTRO arquivo escapa da varredura — não vale o custo de um
/// analisador Roslyn (fora do toolkit deste projeto de testes) para esse caso residual.
/// </remarks>
public sealed partial class SelecaoSemRamificacaoPorTipoEtapaTests
{
    [GeneratedRegex("enum\\s+TipoEtapa\\b")]
    private static partial Regex DeclaracaoDoEnum();

    [GeneratedRegex(
        """(?s)\(bool, string\?, string\?\) AvaliarEtapaObrigatoria\([^)]*\)(?<corpo>.*?)(?=private static \(bool|\z)""")]
    private static partial Regex CorpoDeAvaliarEtapaObrigatoria();

    [GeneratedRegex("""\.Nome\b""")]
    private static partial Regex AcessoANome();

    [GeneratedRegex("""TipoEtapa\.OrigemId\b""")]
    private static partial Regex AcessoAIdentidadeCongelada();

    [GeneratedRegex("\\bclass\\s+TipoEtapaCodigo\\b|\"NOTA_ENEM\"")]
    private static partial Regex CodigoDeTipoDeEtapaNoFonte();

    [Theory(DisplayName = "O detector acusa a classe TipoEtapaCodigo e o literal NOTA_ENEM — canários positivos")]
    [InlineData("public static class TipoEtapaCodigo\n{\n    public const string NotaEnem = \"NOTA_ENEM\";\n}")]
    [InlineData("internal static class TipoEtapaCodigo { }")]
    [InlineData("public bool DeclaraNotaDoEnem => string.Equals(TipoEtapa.Codigo, \"NOTA_ENEM\", StringComparison.Ordinal);")]
    public void Detector_AcusaCodigoDeTipoDeEtapaNoFonte(string canario)
    {
        ArgumentNullException.ThrowIfNull(canario);

        CodigoDeTipoDeEtapaNoFonte().IsMatch(SemComentarios(canario)).Should().BeTrue(
            "cada canário volta a reconhecer a etapa de nota do ENEM pelo código do tipo");
    }

    [Theory(DisplayName = "O detector não acusa a comparação com o código do predicado nem o limite do envelope")]
    [InlineData("bool aprovada = string.Equals(e.TipoEtapa.Codigo, predicado.TipoEtapaCodigo, StringComparison.Ordinal);")]
    [InlineData("string codigo = leitor.TextoNaoVazio(tipo, \"codigo\", tipoPath, LimitesDoEnvelope.TipoEtapaCodigo);")]
    [InlineData("public bool DeclaraNotaDoEnem => TipoEtapa.NotaDeOrigemNoEnem;")]
    [InlineData("// antes o tipo era reconhecido pelo código \"NOTA_ENEM\"")]
    public void Detector_NaoAcusaUsoLegitimo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        CodigoDeTipoDeEtapaNoFonte().IsMatch(SemComentarios(codigo)).Should().BeFalse(
            "o código que o predicado de obrigatoriedade guarda e o limite de tamanho do envelope não são " +
            "comportamento chaveado por um tipo de etapa específico");
    }

    [Fact(DisplayName = "Selecao não declara TipoEtapaCodigo nem o literal NOTA_ENEM — a etapa do ENEM vem do atributo congelado")]
    public void Selecao_NaoDeclaraCodigoDeTipoDeEtapa()
    {
        string raizSelecao = Path.Join(RaizDoRepo(), "src", "selecao");

        Directory.Exists(raizSelecao).Should().BeTrue($"a pasta '{raizSelecao}' precisa existir para ser varrida");

        List<string> violacoes = [.. ArquivosFonte(raizSelecao)
            .Where(static arquivo => CodigoDeTipoDeEtapaNoFonte().IsMatch(SemComentarios(File.ReadAllText(arquivo))))
            .Select(static arquivo => Path.GetFileName(arquivo))];

        violacoes.Should().BeEmpty(
            "a etapa de nota do ENEM é reconhecida pelo atributo do cadastro congelado no snapshot " +
            "da etapa (ADR-0123, Emenda 2), não pelo código do tipo");
    }

    [Theory(DisplayName = "O detector acusa o corpo do método acessando .Nome, mesmo refatorado — canários positivos")]
    [InlineData(
        "private static (bool, string?, string?) AvaliarEtapaObrigatoria(ProcessoSeletivo processo, EtapaObrigatoria predicado)\n" +
        "{\n" +
        "    bool aprovada = processo.Etapas.Any(e => string.Equals(e.Nome, predicado.TipoEtapaCodigo, StringComparison.OrdinalIgnoreCase));\n" +
        "    return (aprovada, aprovada ? null : $\"etapa '{predicado.TipoEtapaCodigo}' ausente\", null);\n" +
        "}\n" +
        "private static (bool, string?, string?) AvaliarModalidadesMinimas(ProcessoSeletivo processo, ModalidadesMinimas predicado)")]
    [InlineData(
        "private static (bool, string?, string?) AvaliarEtapaObrigatoria(ProcessoSeletivo processo, EtapaObrigatoria predicado)\n" +
        "{\n" +
        "    string nomeDaEtapa = processo.Etapas.First().Nome;\n" +
        "    bool aprovada = string.Equals(nomeDaEtapa, predicado.TipoEtapaCodigo, StringComparison.OrdinalIgnoreCase);\n" +
        "    return (aprovada, null, null);\n" +
        "}\n" +
        "private static (bool, string?, string?) AvaliarModalidadesMinimas(ProcessoSeletivo processo, ModalidadesMinimas predicado)")]
    public void Detector_AcusaAcessoANomeNoCorpoDoMetodo_MesmoRefatorado(string canario)
    {
        ArgumentNullException.ThrowIfNull(canario);

        AcessaNome(canario).Should().BeTrue(
            "cada canário reintroduz a comparação pelo rótulo editorial, escrita de um jeito que " +
            "uma checagem de uma única linha deixaria passar (quebra de linha, variável local)");
    }

    [Fact(DisplayName = "O detector não acusa a versão corrigida — comparação pelo código congelado")]
    public void Detector_NaoAcusaComparacaoPeloCodigoCongelado()
    {
        const string corrigido =
            "private static (bool, string?, string?) AvaliarEtapaObrigatoria(ProcessoSeletivo processo, EtapaObrigatoria predicado)\n" +
            "{\n" +
            "    bool aprovada = processo.Etapas.Any(\n" +
            "        e => string.Equals(e.TipoEtapa.Codigo, predicado.TipoEtapaCodigo, StringComparison.Ordinal));\n" +
            "    return (aprovada, aprovada ? null : $\"etapa do tipo '{predicado.TipoEtapaCodigo}' ausente\", null);\n" +
            "}\n" +
            "private static (bool, string?, string?) AvaliarModalidadesMinimas(ProcessoSeletivo processo, ModalidadesMinimas predicado)";

        AcessaNome(corrigido).Should().BeFalse(
            "a versão corrigida só acessa TipoEtapa.Codigo, nunca .Nome, dentro do corpo do método");
    }

    [Fact(DisplayName = "enum TipoEtapa não pode ser recriado em Selecao.Domain — o cadastro configurável é a única fonte de verdade")]
    public void Selecao_NaoRecriaEnumTipoEtapa()
    {
        string raizDoRepo = RaizDoRepo();
        string raizEnums = Path.Join(raizDoRepo, "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "Enums");

        Directory.Exists(raizEnums).Should().BeTrue($"a pasta '{raizEnums}' precisa existir para ser varrida");

        List<string> violacoes = [];
        foreach (string arquivo in ArquivosFonte(raizEnums))
        {
            string codigo = SemComentarios(File.ReadAllText(arquivo));
            if (DeclaracaoDoEnum().IsMatch(codigo))
            {
                violacoes.Add(Path.GetFileName(arquivo));
            }
        }

        violacoes.Should().BeEmpty(
            "TipoEtapa passou a ser cadastro configurável em Configuração (ADR-0123/ADR-0061) — " +
            "recriar o enum aqui reintroduziria vocabulário institucional preso em deploy (issue #1071)");
    }

    [Fact(DisplayName = "AvaliadorConformidadeLegal real: AvaliarEtapaObrigatoria não acessa .Nome e decide por TipoEtapa.OrigemId")]
    public void Selecao_AvaliarEtapaObrigatoria_DecidePelaIdentidadeCongelada()
    {
        string raizDoRepo = RaizDoRepo();
        string arquivo = Path.Join(
            raizDoRepo, "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "Services", "AvaliadorConformidadeLegal.cs");

        File.Exists(arquivo).Should().BeTrue($"o arquivo '{arquivo}' precisa existir para ser varrido");

        string codigo = SemComentarios(File.ReadAllText(arquivo));

        AcessaNome(codigo).Should().BeFalse(
            "AvaliarEtapaObrigatoria não pode voltar a comparar pelo rótulo editorial da etapa");

        string corpo = CorpoDeAvaliarEtapaObrigatoria().Match(codigo).Groups["corpo"].Value;
        corpo.Should().NotBeNullOrEmpty("o método AvaliarEtapaObrigatoria precisa existir no arquivo real");
        AcessoAIdentidadeCongelada().IsMatch(corpo).Should().BeTrue(
            "a decisão é por identidade da origem (ADR-0129) — ausência prova que o método mudou de forma " +
            "que este detector não reconhece mais (falso-negativo silencioso é pior que falso-positivo)");
    }

    private static bool AcessaNome(string codigo)
    {
        string corpo = CorpoDeAvaliarEtapaObrigatoria().Match(codigo).Groups["corpo"].Value;
        return AcessoANome().IsMatch(corpo);
    }

    /// <summary>Remove comentários de linha e de bloco — mesma técnica do precedente de TipoProcesso.</summary>
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
