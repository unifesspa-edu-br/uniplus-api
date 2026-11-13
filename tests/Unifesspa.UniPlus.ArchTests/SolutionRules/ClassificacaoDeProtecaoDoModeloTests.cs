namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

/// <summary>
/// Fitness functions da classificação de proteção declarada no modelo (ADR-0153): toda coluna mapeada de
/// todo <see cref="DbContext"/> resolve uma classificação, e o mapa coluna → classificação fica num arquivo
/// versionado — qualquer mudança, inclusive um rebaixamento, reprova até o arquivo ser atualizado no mesmo PR.
/// </summary>
public sealed class ClassificacaoDeProtecaoDoModeloTests
{
    private const string AtualizarRetratoEnvVar = "UPDATE_RETRATO_CLASSIFICACAO";
    private const string ArquivoDoRetrato = "tests/Unifesspa.UniPlus.ArchTests/SolutionRules/ClassificacaoDeProtecaoDoModelo.retrato.txt";

    // Os modelos são construídos uma vez por execução: cada DbContext custa um OnModelCreating completo.
    private static readonly Lazy<IReadOnlyList<(string Contexto, IReadOnlyList<string> Violacoes, string Retrato)>> Modelos = new(Analisar);

    [Fact(DisplayName = "A descoberta encontra os DbContexts da solução")]
    public void DescobertaEncontraOsDbContexts() =>
        Modelos.Value.Should().NotBeEmpty("sem DbContext descoberto, as demais conferências passariam vazias");

    [Fact(DisplayName = "Toda coluna mapeada de todo DbContext resolve uma classificação de proteção")]
    public void TodaColunaMapeadaResolveClassificacao()
    {
        List<string> violacoes = [];

        foreach ((string contexto, IReadOnlyList<string> violacoesDoContexto, _) in Modelos.Value)
        {
            violacoes.AddRange(violacoesDoContexto.Select(v => $"{contexto}: {v}"));
        }

        violacoes.Should().BeEmpty(
            "toda coluna precisa de decisão de proteção antes de poder chegar a uma resposta administrativa (ADR-0153). "
            + "Violações:\n" + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "O retrato versionado bate com a classificação do modelo; rebaixar exige atualizá-lo")]
    public void RetratoVersionadoBateComOModelo()
    {
        string atual = string.Concat(Modelos.Value.Select(c => $"# {c.Contexto}\n{c.Retrato}\n"));
        string caminho = Path.Join(SolutionRootLocator.Locate(), ArquivoDoRetrato);

        if (Environment.GetEnvironmentVariable(AtualizarRetratoEnvVar) == "1")
        {
            File.WriteAllText(caminho, atual);
        }

        string versionado = File.ReadAllText(caminho).ReplaceLineEndings("\n");

        atual.Should().Be(
            versionado,
            $"a classificação de uma coluna mudou: confira a diferença, obtenha a aprovação da proteção de dados se for rebaixamento "
            + $"e regenere o arquivo com {AtualizarRetratoEnvVar}=1.");
    }

    [Fact(DisplayName = "Todo projeto de infraestrutura da solução está referenciado pelo ArchTests")]
    public void TodoProjetoDeInfraestruturaEstaReferenciado()
    {
        string[] semAssembly =
        [
            .. ProjetosDeInfraestrutura()
                .Select(Path.GetFileNameWithoutExtension)
                .Where(nome => !File.Exists(Path.Join(AppContext.BaseDirectory, nome + ".dll")))
                .Order(StringComparer.Ordinal)!,
        ];

        semAssembly.Should().BeEmpty(
            "um projeto de infraestrutura fora do ArchTests teria os DbContexts dele sem a conferência de classificação. Adicione a referência a: "
            + string.Join(", ", semAssembly));
    }

    [Fact(DisplayName = "Todo DbContext concreto da solução tem IDesignTimeDbContextFactory")]
    public void TodoDbContextTemFactory()
    {
        HashSet<Type> comFactory = [.. FabricasDeContexto().Select(f => f.Contexto)];

        string[] semFactory =
        [
            .. AssembliesDeInfraestrutura()
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(DbContext)) && !comFactory.Contains(t))
                .Select(t => t.FullName!)
                .Order(StringComparer.Ordinal),
        ];

        semFactory.Should().BeEmpty(
            "o DbContext sem IDesignTimeDbContextFactory ficaria fora da conferência de classificação: " + string.Join(", ", semFactory));
    }

    private static string[] ProjetosDeInfraestrutura() =>
        [.. Directory.EnumerateFiles(Path.Join(SolutionRootLocator.Locate(), "src"), "Unifesspa.UniPlus.*.Infrastructure.csproj", SearchOption.AllDirectories)];

    private static IEnumerable<Assembly> AssembliesDeInfraestrutura() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.Infrastructure.dll").Select(Assembly.LoadFrom);

    private static IEnumerable<(Type Fabrica, Type Contexto, MethodInfo Metodo)> FabricasDeContexto() =>
        AssembliesDeInfraestrutura()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>))
                .Select(i => (Fabrica: t, Contexto: i.GetGenericArguments()[0], Metodo: i.GetMethod(nameof(IDesignTimeDbContextFactory<DbContext>.CreateDbContext))!)));

    // Uma única análise por modelo: a conferência e o retrato partem do mesmo resultado.
    private static List<(string Contexto, IReadOnlyList<string> Violacoes, string Retrato)> Analisar() =>
    [
        .. FabricasDeContexto()
            .OrderBy(f => f.Contexto.Name, StringComparer.Ordinal)
            .Select(f => Modelo(f.Fabrica, f.Contexto, f.Metodo))
            .Select(m => (m.Contexto, Conferencia: ConferenciaDaClassificacaoDoModelo.Conferir(m.Modelo)))
            .Select(m => (m.Contexto, m.Conferencia.Violacoes, m.Conferencia.Retrato)),
    ];

    // O modelo de design-time guarda todas as anotações do modelo; o de runtime é otimizado e pode descartá-las.
    private static (string Contexto, IModel Modelo) Modelo(Type fabrica, Type tipoDoContexto, MethodInfo criar)
    {
        object instancia = Activator.CreateInstance(fabrica)!;
        using DbContext contexto = (DbContext)criar.Invoke(instancia, [Array.Empty<string>()])!;
        return (tipoDoContexto.Name, contexto.GetService<IDesignTimeModel>().Model);
    }
}
