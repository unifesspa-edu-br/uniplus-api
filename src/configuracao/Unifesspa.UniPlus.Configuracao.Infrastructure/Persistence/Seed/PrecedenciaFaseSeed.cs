namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Fonte única do seed das seis arestas estruturais do grafo de precedências
/// entre fases canônicas (story #851, §3.3). Consumida tanto pela configuração EF
/// Core (que materializa as linhas via <c>HasData</c> na migration) quanto pelos
/// testes (que conferem o seed do banco contra esta lista), garantindo uma única
/// definição por aresta.
/// </summary>
/// <remarks>
/// Ao contrário de <c>FaseCanonica</c> (cadastro 100% CRUD-administrado, sem
/// seed), <c>PrecedenciaFase</c> <b>é</b> seed-governada: as seis arestas abaixo
/// já têm valores concretos aprovados e não dependem de ato operacional pós-deploy
/// para existir. O CRUD admin continua disponível para acrescentar novas arestas.
/// Os <see cref="Guid"/> são fixos determinísticos (não <c>Guid.CreateVersion7</c>)
/// porque seed precisa de identidade estável entre execuções — o mesmo molde de
/// <c>RegraCatalogoSeed</c>/<c>FatoCandidatoSeed</c>.
/// </remarks>
public static class PrecedenciaFaseSeed
{
    // Prefixo determinístico próprio do catálogo de precedências (distinto do
    // rol_de_regras e do rol_de_fatos_candidato, para não confundir identidades
    // entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"93ec0000-0000-7000-8000-{n:D12}");

    /// <summary>As seis arestas estruturais, na ordem canônica de §3.3.</summary>
    public static IReadOnlyList<PrecedenciaFaseSeedItem> Itens { get; } =
    [
        new(SeedId(1), "INSCRICAO", "HOMOLOGACAO", false),
        new(SeedId(2), "RESULTADO_PRELIMINAR", "RECURSOS", false),
        new(SeedId(3), "RECURSOS", "RESULTADO_FINAL", false),
        new(SeedId(4), "RESULTADO_FINAL", "HABILITACAO", false),
        new(SeedId(5), "HABILITACAO", "MATRICULA", false),
        new(SeedId(6), "HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL", false),
    ];
}

/// <summary>
/// Definição de uma aresta do seed (fonte única), na forma da entidade
/// <c>PrecedenciaFase</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio (self-loop, duplicata,
/// ciclo) é garantida por teste de unidade sobre a factory e, para este conjunto
/// específico, por construção (as seis arestas formam um grafo acíclico).
/// </summary>
public sealed record PrecedenciaFaseSeedItem(
    Guid Id,
    string AntecessoraCodigo,
    string SucessoraCodigo,
    bool PermiteSobreposicao);
