namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Registro histórico de um identificador (<see cref="TipoIdentificador"/>) de
/// uma <see cref="Unidade"/>. Cada vez que Slug, Sigla, Codigo ou Alias muda,
/// a vigência do valor anterior é encerrada e uma nova entrada é aberta — o
/// histórico responde à pergunta "qual sigla esta unidade tinha na data D".
/// </summary>
/// <remarks>
/// Entidade interna ao agregado <see cref="Unidade"/>; criada apenas por
/// métodos do agregado. Construtor privado mantém o invariante.
/// Não implementa soft-delete: deriva de <see cref="EntityBase"/> (não de
/// <c>SoftDeletableEntity</c>), portanto não carrega as colunas
/// is_deleted/deleted_at/deleted_by. O histórico é imutável (append-only):
/// entradas fechadas não são deletadas — a vigência é encerrada por
/// <see cref="VigenciaFim"/> e mantida para auditoria.
/// </remarks>
public sealed class UnidadeIdentificadorHistorico : EntityBase
{
    private const int MotivoMudancaMaxLength = 500;

    public Guid UnidadeId { get; private set; }
    public TipoIdentificador TipoIdentificador { get; private set; }
    public string Valor { get; private set; } = string.Empty;
    public DateOnly VigenciaInicio { get; private set; }
    public DateOnly? VigenciaFim { get; private set; }
    public string? MotivoMudanca { get; private set; }

    // EF Core materialization
    private UnidadeIdentificadorHistorico()
    {
    }

    /// <summary>
    /// Valida o motivo da mudança, opcional, contra o tamanho da coluna. Público porque
    /// o handler de atualização o roda junto de <see cref="Unidade.ValidarCampos"/>
    /// antes de buscar a Unidade.
    /// </summary>
    public static Result ValidarMotivoMudanca(string? motivo) =>
        motivo is not null && motivo.Trim().Length > MotivoMudancaMaxLength
            ? Result.ValidationFailure([new FieldError("motivoMudancaIdentificador", new DomainError(
                UnidadeErrorCodes.MotivoMudancaTamanho,
                $"Motivo da mudança de identificador não pode ter mais que {MotivoMudancaMaxLength} caracteres."))])
            : Result.Success();

    internal static UnidadeIdentificadorHistorico Abrir(
        Guid unidadeId,
        TipoIdentificador tipo,
        string valor,
        DateOnly vigenciaInicio,
        string? motivoMudanca = null)
    {
        return new UnidadeIdentificadorHistorico
        {
            UnidadeId = unidadeId,
            TipoIdentificador = tipo,
            Valor = valor,
            VigenciaInicio = vigenciaInicio,
            VigenciaFim = null,
            MotivoMudanca = motivoMudanca,
        };
    }

    // Encerra a vigência desta entrada — chamado pelo agregado ao renomear.
    internal void FecharVigencia(DateOnly dataFechamento)
    {
        VigenciaFim = dataFechamento;
    }
}
