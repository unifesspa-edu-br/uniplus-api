namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) do calendário de dias úteis vigente no momento em que a
/// versão é publicada (UNI-REQ-0080). Carrega a lista inteira de dias não úteis, não uma
/// referência ao calendário de origem.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que por valor.</b> Guardar apenas <see cref="OrigemId"/> e
/// <see cref="VersaoDoCalendario"/> não reproduz contagem nenhuma: o calendário que deixa de ser
/// vigente pode ser removido do cadastro, e a versão publicada é imutável — não há como
/// completar depois o dado que sumiu. Uma recontagem feita nesse estado dependeria do catálogo
/// vivo, contra a garantia de reprodutibilidade do UNI-REQ-0093.
/// </para>
/// <para>
/// <b>Origem é rastreio, não referência.</b> <see cref="OrigemId"/> e
/// <see cref="VersaoDoCalendario"/> dizem de onde a lista veio, para auditoria. Nada no cálculo os
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
    /// Limite da versão do calendário, igual ao do cadastro de origem e ao da coluna que o persiste.
    /// Aceitar mais aqui deixaria o decoder reidratar um calendário que o cadastro não consegue
    /// gravar — e a prova byte a byte não acusaria, porque o encoder reemitiria o texto tal qual.
    /// </summary>
    private const int VersaoDoCalendarioMaxLength = 60;

    private CalendarioDiasUteisCongelado(
        Guid origemId,
        string versaoDoCalendario,
        IReadOnlyList<DiaNaoUtilCongelado> diasNaoUteis)
    {
        OrigemId = origemId;
        VersaoDoCalendario = versaoDoCalendario;
        DiasNaoUteis = diasNaoUteis;
    }

    /// <summary>Identificador do calendário de origem — rastreabilidade, sem FK e sem releitura.</summary>
    public Guid OrigemId { get; }

    /// <summary>Versão do calendário de origem, no mesmo regime de rastreio.</summary>
    public string VersaoDoCalendario { get; }

    /// <summary>Os dias não úteis copiados por valor, na ordem canônica.</summary>
    public IReadOnlyList<DiaNaoUtilCongelado> DiasNaoUteis { get; }

    /// <summary>
    /// Congela o calendário a partir dos dias já validados, ordenando-os canonicamente e
    /// recusando duplicata.
    /// </summary>
    /// <remarks>
    /// Duplicata é definida pela chave <c>(data, abrangência, município, UF)</c> e é recusada
    /// em vez de deduplicada em silêncio: dois registros iguais no calendário de origem indicam
    /// cadastro inconsistente, e absorvê-los aqui esconderia o defeito dentro de um artefato
    /// imutável. O mesmo dia pode aparecer mais de uma vez com abrangências diferentes — um
    /// feriado nacional que também é municipal são dois fatos, com fundamentos distintos.
    /// </remarks>
    public static Result<CalendarioDiasUteisCongelado> Criar(
        Guid origemId,
        string? versaoDoCalendario,
        IReadOnlyList<DiaNaoUtilCongelado>? diasNaoUteis)
    {
        DomainError? erro = ValidarOrigem(origemId, versaoDoCalendario) ?? ValidarDias(diasNaoUteis);
        if (erro is not null)
        {
            return Result<CalendarioDiasUteisCongelado>.Failure(erro);
        }

        List<DiaNaoUtilCongelado> ordenados = [.. Ordenar(diasNaoUteis!)];
        DomainError? duplicado = DiaDuplicado(ordenados);
        return duplicado is not null
            ? Result<CalendarioDiasUteisCongelado>.Failure(duplicado)
            : Result<CalendarioDiasUteisCongelado>.Success(new CalendarioDiasUteisCongelado(origemId, versaoDoCalendario?.Trim() ?? string.Empty, ordenados));
    }

    private static DomainError? ValidarOrigem(Guid origemId, string? versaoDoCalendario)
    {
        if (origemId == Guid.Empty)
        {
            return new DomainError(CalendarioDiasUteisCongeladoErrorCodes.OrigemObrigatoria, "O identificador do calendário de origem é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(versaoDoCalendario))
        {
            return new DomainError(CalendarioDiasUteisCongeladoErrorCodes.VersaoDoCalendarioObrigatoria, "A versão do calendário de origem é obrigatória.");
        }

        return versaoDoCalendario.Trim().Length > VersaoDoCalendarioMaxLength
            ? new DomainError(CalendarioDiasUteisCongeladoErrorCodes.VersaoDoCalendarioTamanho, $"A versão do calendário excede {VersaoDoCalendarioMaxLength} caracteres — o cadastro de origem não a gravaria.")
            : null;
    }

    private static DomainError? ValidarDias(IReadOnlyList<DiaNaoUtilCongelado>? dias) => dias switch
    {
        null => new DomainError(CalendarioDiasUteisCongeladoErrorCodes.DiasObrigatorios, "A lista de dias não úteis é obrigatória."),
        { Count: 0 } => new DomainError(CalendarioDiasUteisCongeladoErrorCodes.SemDiaNaoUtil, "O calendário congelado precisa de ao menos um dia não útil — um calendário vazio não conta nada."),
        _ => null,
    };

    private static DomainError? DiaDuplicado(List<DiaNaoUtilCongelado> dias)
    {
        for (int i = 1; i < dias.Count; i++)
        {
            if (MesmaChave(dias[i - 1], dias[i]))
            {
                return new DomainError(CalendarioDiasUteisCongeladoErrorCodes.DiaDuplicado,
                    "O calendário tem um dia repetido com a mesma abrangência e o mesmo recorte territorial.");
            }
        }

        return null;
    }

    /// <summary>A ordem canônica do bloco, também usada pelo codec do envelope.</summary>
    public static IEnumerable<DiaNaoUtilCongelado> Ordenar(IEnumerable<DiaNaoUtilCongelado> dias) =>
        dias
            .OrderBy(static d => d.Data)
            .ThenBy(static d => d.Abrangencia.Codigo(), StringComparer.Ordinal)
            .ThenBy(static d => d.MunicipioIbge ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static d => d.Uf ?? string.Empty, StringComparer.Ordinal);

    private static bool MesmaChave(DiaNaoUtilCongelado a, DiaNaoUtilCongelado b) =>
        a.Data == b.Data
        && a.Abrangencia == b.Abrangencia
        && string.Equals(a.MunicipioIbge, b.MunicipioIbge, StringComparison.Ordinal)
        && string.Equals(a.Uf, b.Uf, StringComparison.Ordinal);

}
