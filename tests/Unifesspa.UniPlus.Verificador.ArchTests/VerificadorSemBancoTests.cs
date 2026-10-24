namespace Unifesspa.UniPlus.Verificador.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>
/// O verificador manipula conteúdo sensível e não tem credencial nem conexão no banco da API
/// (ADR-0146). Percorre os assemblies que ele referencia, direta e transitivamente.
/// </summary>
public sealed class VerificadorSemBancoTests
{
    private static readonly string[] Proibidos =
    [
        "Unifesspa.UniPlus.Selecao",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    [Fact(DisplayName = "O verificador não referencia Seleção, EF Core nem Npgsql, nem transitivamente")]
    public void Verificador_NaoReferenciaModuloDeNegocioNemBanco()
    {
        Assembly verificador = typeof(VerificadorOptions).Assembly;

        IReadOnlyCollection<string> alcancados = AssembliesAlcancados(verificador);

        alcancados.Where(nome => Proibidos.Any(p => nome.StartsWith(p, StringComparison.Ordinal)))
            .Should().BeEmpty();
        alcancados.Should().Contain("Unifesspa.UniPlus.Verificador.Contratos", "o percurso precisa alcançar o que o verificador realmente referencia");
    }

    private static HashSet<string> AssembliesAlcancados(Assembly raiz)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        Queue<Assembly> fila = new([raiz]);
        while (fila.TryDequeue(out Assembly? atual))
        {
            foreach (AssemblyName referencia in atual.GetReferencedAssemblies())
            {
                if (referencia.Name is null || !vistos.Add(referencia.Name))
                {
                    continue;
                }

                try
                {
                    fila.Enqueue(Assembly.Load(referencia));
                }
                catch (FileNotFoundException)
                {
                    // Assembly de plataforma que não chega ao diretório de saída: não é dependência do pacote.
                }
            }
        }

        return vistos;
    }
}
