namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Registro histórico de um identificador (<see cref="TipoIdentificador"/>) de
/// uma <see cref="Unidade"/>. Cada vez que Slug, Sigla, Codigo ou Alias muda,
/// a vigência do valor anterior é encerrada e uma nova entrada é aberta — o
/// histórico responde à pergunta "qual sigla esta unidade tinha na data D".
/// </summary>
/// <remarks>
/// Entidade interna ao agregado <see cref="Unidade"/>; criada apenas por
/// métodos do agregado. Construtor privado mantém o invariante.
/// Soft-delete herdado de <see cref="EntityBase"/> não se aplica: o histórico
/// é imutável (append-only). Entradas fechadas não são deletadas — mantidas
/// para auditoria.
/// </remarks>
public sealed class UnidadeIdentificadorHistorico : EntityBase
{
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
