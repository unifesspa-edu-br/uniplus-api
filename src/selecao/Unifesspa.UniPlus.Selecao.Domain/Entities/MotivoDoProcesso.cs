namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Motivo que o processo escolheu do cadastro para um tipo de decisão que usa (UNI-REQ-0285):
/// cópia por valor (ADR-0061) do código, da descrição, do sentido e da subclassificação, que
/// congela na publicação. Mudar o cadastro depois não muda o processo, e o motivo desativado
/// continua utilizável onde já foi congelado (UNI-REQ-0122).
/// </summary>
/// <remarks>
/// Enquanto não congelada, a cópia é refeita ao publicar (ou retificar), como as demais cópias do
/// cadastro: o processo publica a descrição vigente naquele momento. A regra de quando incluir e
/// retirar é de <see cref="EscolhaDosMotivos"/>, que enxerga o estado do processo.
/// </remarks>
public sealed class MotivoDoProcesso : EntityBase, IAuditableEntity
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Id do motivo no cadastro, sem chave estrangeira: a cópia não depende da linha viva.</summary>
    public Guid MotivoDeDecisaoOrigemId { get; private set; }

    public TipoMotivoDeDecisao Tipo { get; private set; }

    public CodigoMotivoDecisao Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = string.Empty;

    public string? Subclassificacao { get; private set; }

    public SentidoDoMotivo Sentido { get; private set; }

    /// <summary>Verdadeiro desde a publicação (ou o fechamento da retificação) que o incluiu.</summary>
    public bool Congelado { get; private set; }

    /// <summary>
    /// Por que o motivo entrou depois da publicação (UNI-REQ-0124). Uso interno da administração:
    /// não é apresentada ao candidato. Nula na escolha anterior à publicação.
    /// </summary>
    public string? JustificativaDaInclusao { get; private set; }

    public string? CreatedBy { get; private set; }

    public string? UpdatedBy { get; private set; }

    private MotivoDoProcesso()
    {
    }

    internal static MotivoDoProcesso Copiar(Guid processoSeletivoId, MotivoDeDecisao origem, string? justificativa) => new()
    {
        ProcessoSeletivoId = processoSeletivoId,
        MotivoDeDecisaoOrigemId = origem.Id,
        Tipo = origem.Tipo,
        Codigo = origem.Codigo,
        Descricao = origem.Descricao,
        Subclassificacao = origem.Subclassificacao,
        Sentido = origem.Sentido,
        JustificativaDaInclusao = justificativa,
    };

    /// <summary>Refaz a cópia da descrição vigente no cadastro; só a não congelada se refaz.</summary>
    internal void Recopiar(MotivoDeDecisao origem)
    {
        if (!Congelado)
        {
            Descricao = origem.Descricao;
        }
    }

    internal void Congelar() => Congelado = true;
}
