namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) do calendário de dias úteis vigente no momento em que a
/// versão é publicada (UNI-REQ-0080). Carrega a lista inteira de dias não úteis, não uma
/// referência ao dataset de origem.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que por valor.</b> Guardar apenas <see cref="OrigemId"/> e
/// <see cref="VersaoDataset"/> não reproduz contagem nenhuma: o dataset que deixa de ser
/// vigente pode ser removido do cadastro, e a versão publicada é imutável — não há como
/// completar depois o dado que sumiu. Uma recontagem feita nesse estado dependeria do catálogo
/// vivo, contra a garantia de reprodutibilidade do UNI-REQ-0093.
/// </para>
/// <para>
/// <b>Origem é rastreio, não referência.</b> <see cref="OrigemId"/> e
/// <see cref="VersaoDataset"/> dizem de onde a lista veio, para auditoria. Nada no cálculo os
/// dereferencia, e não existe caminho que releia a origem a partir deles.
/// </para>
/// <para>
/// <b>Ordem canônica.</b> Os dias são ordenados por data, abrangência, município e UF, com
/// comparação ordinal para textos. A ordem é do snapshot, não do que o reader devolveu: o
/// envelope é comparado byte a byte, e uma ordem que variasse com o plano de execução do
/// Postgres faria a mesma configuração produzir bytes diferentes.
/// </para>
/// </remarks>
public sealed record CalendarioDiasUteisCongelado
{
    /// <summary>
    /// Limite da versão do dataset, igual ao do cadastro de origem e ao da coluna que o persiste.
    /// Aceitar mais aqui deixaria o decoder reidratar um calendário que o cadastro não consegue
    /// gravar — e a prova byte a byte não acusaria, porque o encoder reemitiria o texto tal qual.
    /// </summary>
    private const int VersaoDatasetMaxLength = 60;

    private CalendarioDiasUteisCongelado(
        Guid origemId,
        string versaoDataset,
        IReadOnlyList<DiaNaoUtilCongelado> diasNaoUteis)
    {
        OrigemId = origemId;
        VersaoDataset = versaoDataset;
        DiasNaoUteis = diasNaoUteis;
    }

    /// <summary>Identificador do dataset de origem — rastreabilidade, sem FK e sem releitura.</summary>
    public Guid OrigemId { get; }

    /// <summary>Versão do dataset de origem, no mesmo regime de rastreio.</summary>
    public string VersaoDataset { get; }

    /// <summary>Os dias não úteis copiados por valor, na ordem canônica.</summary>
    public IReadOnlyList<DiaNaoUtilCongelado> DiasNaoUteis { get; }

    /// <summary>
    /// Congela o calendário a partir dos dias já validados, ordenando-os canonicamente e
    /// recusando duplicata.
    /// </summary>
    /// <remarks>
    /// Duplicata é definida pela chave <c>(data, abrangência, município, UF)</c> e é recusada
    /// em vez de deduplicada em silêncio: dois registros iguais no dataset de origem indicam
    /// cadastro inconsistente, e absorvê-los aqui esconderia o defeito dentro de um artefato
    /// imutável. O mesmo dia pode aparecer mais de uma vez com abrangências diferentes — um
    /// feriado nacional que também é municipal são dois fatos, com fundamentos distintos.
    /// </remarks>
    public static Result<CalendarioDiasUteisCongelado> Criar(
        Guid origemId,
        string? versaoDataset,
        IReadOnlyList<DiaNaoUtilCongelado>? diasNaoUteis)
    {
        if (origemId == Guid.Empty)
        {
            return Falha("OrigemObrigatoria", "O identificador do dataset de origem é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Falha("VersaoDatasetObrigatoria", "A versão do dataset de origem é obrigatória.");
        }

        if (versaoDataset.Trim().Length > VersaoDatasetMaxLength)
        {
            return Falha(
                "VersaoDatasetTamanho",
                $"A versão do dataset excede {VersaoDatasetMaxLength} caracteres — o cadastro de origem não a gravaria.");
        }

        if (diasNaoUteis is null)
        {
            return Falha("DiasObrigatorios", "A lista de dias não úteis é obrigatória.");
        }

        // Mesma cardinalidade do agregado de origem: um dataset sem nenhum dia não útil não
        // conta nada. Congelá-lo faria a versão publicada afirmar que todo dia é útil, e a
        // contagem produziria prazo diferente do calendário que rege o certame — de forma
        // imutável, e sem nada acusar.
        if (diasNaoUteis.Count == 0)
        {
            return Falha(
                "SemDiaNaoUtil",
                "O calendário congelado precisa de ao menos um dia não útil — um calendário vazio não conta nada.");
        }

        List<DiaNaoUtilCongelado> ordenados = [.. Ordenar(diasNaoUteis)];

        for (int i = 1; i < ordenados.Count; i++)
        {
            if (MesmaChave(ordenados[i - 1], ordenados[i]))
            {
                return Falha(
                    "DiaDuplicado",
                    $"O dia {ordenados[i].Data:yyyy-MM-dd} aparece mais de uma vez com a mesma abrangência e o mesmo recorte territorial.");
            }
        }

        return Result<CalendarioDiasUteisCongelado>.Success(
            new CalendarioDiasUteisCongelado(origemId, versaoDataset.Trim(), ordenados));
    }

    /// <summary>A ordem canônica do bloco, também usada pelo codec do envelope.</summary>
    public static IEnumerable<DiaNaoUtilCongelado> Ordenar(IEnumerable<DiaNaoUtilCongelado> dias) =>
        dias
            .OrderBy(static d => d.Data)
            .ThenBy(static d => d.Abrangencia, StringComparer.Ordinal)
            .ThenBy(static d => d.MunicipioIbge ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static d => d.Uf ?? string.Empty, StringComparer.Ordinal);

    private static bool MesmaChave(DiaNaoUtilCongelado a, DiaNaoUtilCongelado b) =>
        a.Data == b.Data
        && string.Equals(a.Abrangencia, b.Abrangencia, StringComparison.Ordinal)
        && string.Equals(a.MunicipioIbge, b.MunicipioIbge, StringComparison.Ordinal)
        && string.Equals(a.Uf, b.Uf, StringComparison.Ordinal);

    private static Result<CalendarioDiasUteisCongelado> Falha(string sufixo, string mensagem) =>
        Result<CalendarioDiasUteisCongelado>.Failure(
            new DomainError($"CalendarioDiasUteisCongelado.{sufixo}", mensagem));
}
