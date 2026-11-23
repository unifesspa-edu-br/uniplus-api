namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;
using Unifesspa.UniPlus.TestSupport;

/// <summary>
/// Fitness functions da classificação de proteção declarada no modelo (ADR-0153): toda coluna mapeada de
/// todo <see cref="DbContext"/> resolve uma classificação, e o mapa coluna → classificação fica num arquivo
/// versionado — qualquer mudança, inclusive um rebaixamento, reprova até o arquivo ser atualizado no mesmo PR.
/// </summary>
public sealed class ClassificacaoDeProtecaoDoModeloTests
{
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

        string caminho = ArquivoDaClassificacaoDeProtecao.Caminho();
        string? versionado = File.Exists(caminho) ? ArquivoDaClassificacaoDeProtecao.LerTexto() : null;
        if (!string.Equals(atual, versionado, StringComparison.Ordinal))
        {
            string resultado = ResultadoDeConferencia.Gravar(Path.GetFileName(caminho), atual);
            atual.Should().Be(versionado, $"confira a mudança e obtenha aprovação de proteção de dados em caso de rebaixamento; {ResultadoDeConferencia.Mensagem(resultado, "tests/Unifesspa.UniPlus.Contratos.UnitTests/Conferencias/ClassificacaoDeProtecaoDoModelo.txt")}");
        }
    }

    private static List<(string Contexto, IReadOnlyList<string> Violacoes, string Conteudo)> Analisar()
    {
        using MonolitoDbContext monolito = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        using PortalDbContext portal = new PortalDbContextDesignTimeFactory().CreateDbContext([]);
        (IReadOnlyList<string> Violacoes, string ConteudoDoArquivo) conferenciaMonolito = ConferenciaDaClassificacaoDoModelo.Conferir(monolito.GetService<IDesignTimeModel>().Model);
        (IReadOnlyList<string> Violacoes, string ConteudoDoArquivo) conferenciaPortal = ConferenciaDaClassificacaoDoModelo.Conferir(portal.GetService<IDesignTimeModel>().Model);
        return [
            (nameof(MonolitoDbContext), conferenciaMonolito.Violacoes, conferenciaMonolito.ConteudoDoArquivo),
            (nameof(PortalDbContext), conferenciaPortal.Violacoes, conferenciaPortal.ConteudoDoArquivo),
        ];
    }
}
