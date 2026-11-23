namespace Unifesspa.UniPlus.Verificador.ArchTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>
/// O verificador manipula conteúdo sensível e não tem credencial nem conexão no banco da API
/// (ADR-0146). Percorre os assemblies que ele referencia, direta e transitivamente, pelos nomes: uma referência
/// proibida conta mesmo quando o assembly dela não chega ao diretório de saída.
/// </summary>
public sealed class VerificadorSemBancoTests
{
    private const string Contratos = "Unifesspa.UniPlus.Verificador.Contratos";

    private static readonly string[] Proibidos =
    [
        "Unifesspa.UniPlus.Selecao",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    [Fact(DisplayName = "O verificador não referencia Seleção, EF Core nem Npgsql, nem transitivamente")]
    public void Verificador_NaoReferenciaModuloDeNegocioNemBanco()
    {
        HashSet<string> alcancados = AssembliesAlcancados.DoAssembly(typeof(VerificadorOptions).Assembly);

        ProibidosEm(alcancados).Should().BeEmpty();
        alcancados.Should().Contain(Contratos, "o percurso precisa alcançar o que o verificador realmente referencia");
    }

    [Fact(DisplayName = "A regra reprova a referência proibida de um assembly intermediário cujo alvo está carregado")]
    public void Regra_AlvoProibidoCarregadoNoIntermediario_ReprovaPeloNome()
    {
        File.Exists(Path.Join(AppContext.BaseDirectory, "Npgsql.Sintetico.dll")).Should().BeTrue();

        HashSet<string> alcancados = AssembliesAlcancados.DoAssembly(typeof(Sintetico.Raiz).Assembly);

        ProibidosEm(alcancados).Should().Contain("Npgsql.Sintetico");
    }

    [Fact(DisplayName = "A regra reprova a referência proibida de um assembly intermediário cujo alvo não chega ao diretório de saída")]
    public void Regra_AlvoProibidoAusenteDoDiretorio_ReprovaPeloNome()
    {
        File.Exists(Path.Join(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.Sintetico.dll")).Should().BeFalse();

        HashSet<string> alcancados = AssembliesAlcancados.DoAssembly(typeof(Sintetico.Raiz).Assembly);

        ProibidosEm(alcancados).Should().Contain("Microsoft.EntityFrameworkCore.Sintetico");
    }

    private static string[] ProibidosEm(IEnumerable<string> nomes) =>
        [.. nomes.Where(nome => Proibidos.Any(proibido => nome.StartsWith(proibido, StringComparison.Ordinal)))];
}
