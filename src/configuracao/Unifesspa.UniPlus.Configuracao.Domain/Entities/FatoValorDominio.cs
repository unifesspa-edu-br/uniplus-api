namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um valor do conjunto fechado de um <see cref="FatoCandidato"/> categórico
/// <b>estático</b> (ADR-0116) — ex.: <c>PRETA</c> dentro de <c>COR_RACA</c>. Carrega
/// a descrição por valor que orienta a escolha do candidato quando o fato pai é
/// <see cref="Enums.OrigemFato.Declarado"/> (requisito de acessibilidade/clareza).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EntityBase"/> puro (sem soft-delete): assim como o
/// <see cref="FatoCandidato"/> pai, é seed-governado e append-only.
/// </para>
/// <para>
/// Só nasce por <see cref="FatoCandidato.AdicionarValorDominio"/> — o único que
/// conhece a <see cref="FatoCandidato.Origem"/> do pai (para exigir descrição) e os
/// irmãos já adicionados (para a unicidade do <see cref="Codigo"/>). Por isso a
/// validação de negócio vive no agregado pai; esta factory só recusa o que nenhum
/// contexto de negócio tornaria válido (Guid vazio, código em branco).
/// </para>
/// </remarks>
public sealed class FatoValorDominio : EntityBase
{
    /// <summary>Tamanho máximo do <see cref="Codigo"/> — mesmo teto de <c>CodigoFatoCandidato</c>.</summary>
    public const int CodigoMaxLength = 50;

    /// <summary>Tamanho máximo da <see cref="Descricao"/> — mais curta que a do <c>FatoCandidato</c> pai (1000), por ser um rótulo de valor, não um parágrafo.</summary>
    public const int DescricaoMaxLength = 500;

    public Guid FatoCandidatoId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public int Ordem { get; private set; }
    public bool Ativo { get; private set; }

    private FatoValorDominio()
    {
    }

    /// <summary>
    /// Materializa um valor já validado pelo agregado pai (<see cref="FatoCandidato"/>).
    /// Os únicos guardas aqui são os que nenhum contexto de negócio tornaria válidos —
    /// a validação de negócio (unicidade, descrição obrigatória, coerência com o
    /// domínio do pai) é responsabilidade exclusiva de
    /// <see cref="FatoCandidato.AdicionarValorDominio"/>.
    /// </summary>
    internal static FatoValorDominio Criar(
        Guid fatoCandidatoId, string codigo, string? descricao, int ordem, bool ativo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (fatoCandidatoId == Guid.Empty)
        {
            throw new ArgumentException("FatoCandidatoId não pode ser Guid vazio.", nameof(fatoCandidatoId));
        }

        return new FatoValorDominio
        {
            FatoCandidatoId = fatoCandidatoId,
            Codigo = codigo,
            Descricao = descricao,
            Ordem = ordem,
            Ativo = ativo,
        };
    }
}
