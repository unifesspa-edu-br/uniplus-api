namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.IO;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

/// <summary>
/// Fitness functions da classificação de proteção declarada no modelo (ADR-0153): toda coluna mapeada de
/// todo <see cref="DbContext"/> resolve uma classificação, e o mapa coluna → classificação fica num arquivo
/// versionado — qualquer mudança, inclusive um rebaixamento, reprova até o arquivo ser atualizado no mesmo PR.
/// </summary>
public sealed class ClassificacaoDeProtecaoDoModeloTests
{
    private const string AtualizarArquivoEnvVar = "UPDATE_ARQUIVO_CLASSIFICACAO_PROTECAO";

    // Os modelos são construídos uma vez por execução: cada DbContext custa um OnModelCreating completo.
    private static readonly Lazy<IReadOnlyList<(string Contexto, IReadOnlyList<string> Violacoes, string Conteudo)>> Modelos = new(Analisar);

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

    [Fact(DisplayName = "O arquivo versionado bate com a classificação do modelo; rebaixar exige atualizá-lo")]
    public void ArquivoVersionadoBateComOModelo()
    {
        string atual = string.Concat(Modelos.Value.Select(c => $"# {c.Contexto}\n{c.Conteudo}\n"));

        if (Environment.GetEnvironmentVariable(AtualizarArquivoEnvVar) == "1")
        {
            ArquivoDaClassificacaoDeProtecao.Regravar(atual);
        }

        string versionado = ArquivoDaClassificacaoDeProtecao.LerTexto();

        atual.Should().Be(
            versionado,
            $"a classificação de uma coluna mudou: confira a diferença, obtenha a aprovação da proteção de dados se for rebaixamento "
            + $"e regenere o arquivo com {AtualizarArquivoEnvVar}=1.");
    }

    [Fact(DisplayName = "Todo projeto de infraestrutura da solução está referenciado pelo projeto de contratos")]
    public void TodoProjetoDeInfraestruturaEstaReferenciado()
    {
        string[] semAssembly =
        [
            .. ModelosDaSolucao.ProjetosDeInfraestrutura()
                .Select(Path.GetFileNameWithoutExtension)
                .Where(nome => !File.Exists(Path.Join(AppContext.BaseDirectory, nome + ".dll")))
                .Order(StringComparer.Ordinal)!,
        ];

        semAssembly.Should().BeEmpty(
            "um projeto de infraestrutura fora do projeto de contratos teria os DbContexts dele sem a conferência de classificação. Adicione a referência a: "
            + string.Join(", ", semAssembly));
    }

    [Fact(DisplayName = "Todo DbContext concreto da solução tem IDesignTimeDbContextFactory")]
    public void TodoDbContextTemFactory()
    {
        HashSet<Type> comFactory = [.. ModelosDaSolucao.FabricasDeContexto().Select(f => f.Contexto)];

        string[] semFactory =
        [
            .. ModelosDaSolucao.AssembliesDeInfraestrutura()
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(DbContext)) && !comFactory.Contains(t))
                .Select(t => t.FullName!)
                .Order(StringComparer.Ordinal),
        ];

        semFactory.Should().BeEmpty(
            "o DbContext sem IDesignTimeDbContextFactory ficaria fora da conferência de classificação: " + string.Join(", ", semFactory));
    }

    // Uma única análise por modelo: a conferência e o arquivo versionado partem do mesmo resultado.
    private static List<(string Contexto, IReadOnlyList<string> Violacoes, string Conteudo)> Analisar() =>
    [
        .. ModelosDaSolucao.Modelos()
            .Select(m => (m.Contexto, Conferencia: ConferenciaDaClassificacaoDoModelo.Conferir(m.Modelo)))
            .Select(m => (m.Contexto, m.Conferencia.Violacoes, m.Conferencia.ConteudoDoArquivo)),
    ];
}
