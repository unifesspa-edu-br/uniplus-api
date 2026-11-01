namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Fitness function do DTO administrativo (ADR-0153): nenhuma propriedade lida direto de uma coluna fica abaixo da
/// classificação dela no retrato versionado do modelo, e o valor derivado declara a transformação.
/// </summary>
public sealed class DtoAdministrativoClassificadoTests
{

    [Fact(DisplayName = "Toda propriedade classificada do DTO administrativo respeita a classificação da coluna de onde é lida")]
    public void PropriedadeLidaDiretoNaoFicaAbaixoDaFonte()
    {
        IReadOnlyList<string> violacoes = ConferenciaDoDtoClassificado.Conferir(TiposDeDto(), RetratoVersionado());

        violacoes.Should().BeEmpty("a resposta não pode classificar o dado abaixo de onde ele está guardado. Violações:\n" + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "A descoberta encontra DTOs administrativos que declaram classificação")]
    public void DescobertaEncontraOsDtos() =>
        TiposDeDto().Should().NotBeEmpty("sem DTO descoberto, a conferência passaria vazia");

    [Fact(DisplayName = "Todo item classificado mantém natureza, classificação e base legal fora do JSON")]
    public void ItemClassificado_NaoSerializaAProtecao()
    {
        PropertyInfo[] expostas =
        [
            .. AssembliesDeApplication().SelectMany(static a => a.GetExportedTypes())
                .Where(static t => typeof(IItemClassificado).IsAssignableFrom(t) && t is { IsAbstract: false, IsClass: true })
                .SelectMany(static t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Where(static p => p.Name is nameof(IItemClassificado.Natureza) or nameof(IItemClassificado.Classificacao) or nameof(IItemClassificado.BaseLegal)
                    && p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is null),
        ];

        expostas.Should().BeEmpty("a proteção decide a saída e não é para sair: " + string.Join(", ", expostas.Select(static p => $"{p.DeclaringType!.Name}.{p.Name}")));
    }

    private static Type[] TiposDeDto() =>
    [
        .. AssembliesDeApplication()
            .SelectMany(static a => a.GetExportedTypes())
            .Where(static t => t.GetProperties().Any(static p => p.GetCustomAttribute<ProtegidoComoAttribute>() is not null)),
    ];

    private static IEnumerable<Assembly> AssembliesDeApplication() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.Application.dll").Select(Assembly.LoadFrom);

    private static Dictionary<string, ClassificacaoDeProtecao> RetratoVersionado() =>
        File.ReadAllLines(Path.Join(SolutionRootLocator.Locate(), "tests/Unifesspa.UniPlus.ArchTests/SolutionRules/ClassificacaoDeProtecaoDoModelo.retrato.txt"))
            .Where(static l => l.Length > 0 && l[0] != '#')
            .Select(static l => l.Split(": ", 2))
            .Where(static p => Enum.TryParse(p[1], out ClassificacaoDeProtecao _))
            .ToDictionary(static p => p[0], static p => Enum.Parse<ClassificacaoDeProtecao>(p[1]), StringComparer.Ordinal);
}
