namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// A semente do catálogo (<c>HasData</c>) dá só o valor inicial de tipo de documento e fato (ADR-0153): depois da
/// aprovação, a classificação, a hipótese legal, a finalidade, o nome e a descrição só mudam pela tela. Uma migration
/// que reescrevesse esses campos de um item já existente sobrescreveria em silêncio uma proteção aprovada, então
/// nenhuma migration a partir da que criou o circuito de aprovação pode fazê-lo.
/// </summary>
public sealed partial class SementeDoCatalogoNaoSobrescreveProtecaoAprovadaTests
{
    // Timestamp da migration que criou o circuito de aprovação: as anteriores já foram aplicadas e a carga inicial não tinha aprovação.
    private const string PrimeiraMigrationSobAprovacao = "20261101040702";

    private static readonly Dictionary<string, string[]> ColunasDaProtecaoPorTabela = new(StringComparer.Ordinal)
    {
        ["rol_de_fatos_candidato"] = ["classificacao_protecao", "hipotese_legal", "finalidade_tratamento", "nome", "descricao"],
        ["tipo_documento"] = ["classificacao_protecao", "hipotese_legal", "finalidade_tratamento", "nome", "descricao"],
    };

    // As migrations que podem tocar uma coluna protegida. A primeira torna a descrição obrigatória e a dá aos fatos semeados que não a
    // tinham; a segunda reclassifica o nome social de sistema como pessoal, decisão da proteção de dados que a restrição do domínio
    // passa a exigir da linha semeada; a terceira troca o nome e a descrição da idade do candidato, que o comprovante mostra ao titular e
    // dizia "faixa etária" para um dado que o sistema apura como número de anos completos. Nenhuma toca a hipótese legal nem a finalidade.
    private static readonly Dictionary<string, string[]> ColunasLiberadasPorMigration = new(StringComparer.Ordinal)
    {
        ["20261101065734_DeclaraComponenteDeExibicaoEDescricaoObrigatoriaDoFato.cs"] = ["descricao"],
        ["20261101222613_NomeSocialPessoal.cs"] = ["classificacao_protecao"],
        ["20261107013658_AtualizaORotuloDaFaixaEtaria.cs"] = ["nome", "descricao"],
    };

    [Fact(DisplayName = "Nenhuma migration posterior ao circuito de aprovação reescreve a proteção, o nome ou a descrição de item do catálogo")]
    public void MigrationsNaoReescrevemAProtecaoDoCatalogo()
    {
        string pasta = Path.Join(
            SolutionRootLocator.Locate(), "src", "configuracao", "Unifesspa.UniPlus.Configuracao.Infrastructure", "Persistence", "Migrations");

        string[] migrations =
        [
            .. Directory.EnumerateFiles(pasta, "*.cs")
                .Where(c => !c.EndsWith(".Designer.cs", StringComparison.Ordinal) && !c.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal))
                .Where(c => string.CompareOrdinal(Path.GetFileName(c), PrimeiraMigrationSobAprovacao) >= 0),
        ];
        migrations.Should().NotBeEmpty("o filtro de data não pode esvaziar a conferência");

        List<string> violacoes = [];
        foreach (string migration in migrations)
        {
            foreach (Match atualizacao in UpdateData().Matches(File.ReadAllText(migration)))
            {
                string tabela = atualizacao.Groups["tabela"].Value;
                if (!ColunasDaProtecaoPorTabela.TryGetValue(tabela, out string[]? protegidas))
                {
                    continue;
                }

                string[] liberadas = ColunasLiberadasPorMigration.GetValueOrDefault(Path.GetFileName(migration)) ?? [];
                string[] tocadas = [.. protegidas.Except(liberadas, StringComparer.Ordinal)
                    .Where(coluna => atualizacao.Value.Contains($"\"{coluna}\"", StringComparison.Ordinal))];
                violacoes.AddRange(tocadas.Select(coluna => $"{Path.GetFileName(migration)}: UpdateData de {tabela}.{coluna}"));
            }
        }

        violacoes.Should().BeEmpty(
            "a semente só define o valor inicial; mudar a proteção de item existente é pela tela e pela aprovação da proteção de dados. "
            + "Violações:\n" + string.Join("\n", violacoes));
    }

    [GeneratedRegex(@"UpdateData\((?<corpo>(?:(?!\);).)*?table:\s*""(?<tabela>[a-z_]+)""(?:(?!\);).)*)\);", RegexOptions.Singleline)]
    private static partial Regex UpdateData();
}
