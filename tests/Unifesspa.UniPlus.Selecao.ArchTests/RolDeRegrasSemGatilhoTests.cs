namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness test da fronteira append-only do <c>rol_de_regras</c> (#854): a
/// imutabilidade do catálogo é imposta por <em>convenção</em> — ausência de API
/// de mutação e leitura por <c>IRegraCatalogoReader</c> —, nunca por gatilho de
/// banco. O XML-doc de <c>RegraCatalogo</c> chegou a afirmar um gatilho
/// <c>BEFORE UPDATE OR DELETE</c> inexistente (o gatilho real protege
/// <c>versoes_configuracao</c>); este teste trava a afirmação na verdade: se
/// alguma migration passar a criar um gatilho sobre <c>rol_de_regras</c>, ele
/// falha, forçando a correção do texto e a revisão da decisão.
/// </summary>
public sealed class RolDeRegrasSemGatilhoTests
{
    /// <summary>
    /// Detecta um <c>CREATE TRIGGER</c> cujo alvo (<c>ON …</c>) é
    /// <c>rol_de_regras</c>. Aceita as variantes válidas do PostgreSQL
    /// (<c>OR REPLACE</c>, <c>CONSTRAINT</c>) para não deixar um gatilho real
    /// escapar por forma sintática.
    /// </summary>
    private static readonly Regex GatilhoSobreRolDeRegras = new(
        @"create\s+(or\s+replace\s+)?(constraint\s+)?trigger\b[^;]*?\bon\b[^;]*?rol_de_regras",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    [Fact(DisplayName = "Nenhuma migration de Seleção cria gatilho de banco sobre rol_de_regras")]
    public void Migrations_NaoCriamGatilhoSobreRolDeRegras()
    {
        string pastaDeMigrations = PastaDeMigrations();
        Directory.Exists(pastaDeMigrations).Should().BeTrue(
            $"as migrations do módulo Seleção vivem em {pastaDeMigrations}");

        // Comentários C# são removidos antes do match: uma nota explicando a
        // AUSÊNCIA de gatilho não pode ser lida como sua presença.
        List<string> infratoras = Directory
            .EnumerateFiles(pastaDeMigrations, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(arquivo => GatilhoSobreRolDeRegras.IsMatch(SemComentarios(arquivo)))
            .Select(arquivo => Path.GetFileName(arquivo)!)
            .ToList();

        infratoras.Should().BeEmpty(
            "o append-only do rol_de_regras é por convenção; nenhum gatilho o protege no banco — "
            + $"e o XML-doc de RegraCatalogo depende disso. Migrations que criam gatilho: {string.Join(", ", infratoras)}");
    }

    [Theory(DisplayName = "O detector reconhece as formas válidas de CREATE TRIGGER sobre rol_de_regras")]
    [InlineData("CREATE TRIGGER t BEFORE UPDATE ON selecao.rol_de_regras FOR EACH ROW EXECUTE FUNCTION f();")]
    [InlineData("CREATE OR REPLACE TRIGGER t BEFORE UPDATE ON rol_de_regras FOR EACH ROW EXECUTE FUNCTION f();")]
    [InlineData("create constraint trigger t after insert on selecao.rol_de_regras for each row execute function f();")]
    public void Detector_ReconheceFormasValidas(string sql)
    {
        GatilhoSobreRolDeRegras.IsMatch(sql).Should().BeTrue(
            "toda variante sintática de CREATE TRIGGER sobre rol_de_regras deve ser detectada");
    }

    [Theory(DisplayName = "O detector não confunde gatilho de outra tabela com um sobre rol_de_regras")]
    [InlineData("CREATE TRIGGER t BEFORE UPDATE ON selecao.versoes_configuracao FOR EACH ROW EXECUTE FUNCTION f();")]
    [InlineData("SELECT * FROM selecao.rol_de_regras;")]
    public void Detector_NaoAcusaFalsoPositivo(string sql)
    {
        GatilhoSobreRolDeRegras.IsMatch(sql).Should().BeFalse(
            "só um gatilho cujo alvo é rol_de_regras deve ser acusado");
    }

    private static string SemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string PastaDeMigrations([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Persistence",
            "Migrations"));
}
