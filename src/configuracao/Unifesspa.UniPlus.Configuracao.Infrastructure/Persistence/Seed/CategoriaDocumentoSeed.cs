namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Fonte única do seed das dez categorias do catálogo de tipos de documento
/// (UNI-REQ-0013). Consumida tanto pela configuração EF Core (que materializa as
/// linhas via <c>HasData</c> na migration) quanto pelos testes (que conferem o
/// seed do banco contra esta lista), garantindo uma única definição por categoria.
/// </summary>
/// <remarks>
/// <para>A categoria é cadastro <b>CRUD-administrado e seed-governado</b>, no
/// mesmo molde de <see cref="PrecedenciaFaseSeed"/>: estas dez recortam o catálogo
/// consolidado dos sistemas legados e não dependem de ato operacional pós-deploy
/// para existir, enquanto o CRUD admin continua disponível para acrescentar
/// outras.</para>
/// <para>Os <see cref="Guid"/> são fixos determinísticos (não
/// <c>Guid.CreateVersion7</c>) porque seed precisa de identidade estável entre
/// ambientes — o mesmo molde de <see cref="ModalidadeSeed"/> e
/// <see cref="FatoCandidatoSeed"/>.</para>
/// <para><c>OUTROS</c> nasce sem nenhum tipo de documento associado e permanece
/// como escape para o que não se enquadrar no futuro: categoria de escape vazia é
/// sinal de bom recorte, não de sobra.</para>
/// </remarks>
public static class CategoriaDocumentoSeed
{
    // Prefixo determinístico próprio do catálogo de categorias (distinto dos
    // prefixos de modalidade, fato do candidato, valor de domínio, precedência de
    // fase e regra do catálogo, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"ca7e0000-0000-7000-8000-{n:D12}");

    /// <summary>
    /// As dez categorias, na ordem de exibição do catálogo: identidade da pessoa
    /// primeiro, depois formação e trajetória, depois condição socioeconômica e
    /// características pessoais, por fim o que instrui o processo e o que é
    /// produzido para avaliação.
    /// </summary>
    public static IReadOnlyList<CategoriaDocumentoSeedItem> Itens { get; } =
    [
        new(SeedId(1), "IDENTIFICACAO", "Identificação", 1),
        new(SeedId(2), "ESCOLARIDADE", "Escolaridade", 2),
        new(SeedId(3), "TITULACAO_EXPERIENCIA", "Titulação e experiência", 3),
        new(SeedId(4), "RENDA", "Renda", 4),
        new(SeedId(5), "RESIDENCIA", "Residência", 5),
        new(SeedId(6), "RACA_ETNIA", "Raça/etnia", 6),
        new(SeedId(7), "SAUDE", "Saúde", 7),
        new(SeedId(8), "DOCUMENTO_PROCESSUAL", "Documento processual", 8),
        new(SeedId(9), "PRODUCAO_AVALIATIVA", "Produção avaliativa", 9),
        new(SeedId(10), "OUTROS", "Outros", 10),
    ];
}

/// <summary>
/// Definição de uma categoria do seed (fonte única), na forma da entidade
/// <c>CategoriaDocumento</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio — formato do código,
/// tamanho do nome, ordem não negativa — é garantida por teste, que revalida cada
/// item pela própria factory.
/// </summary>
public sealed record CategoriaDocumentoSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    int Ordem);
