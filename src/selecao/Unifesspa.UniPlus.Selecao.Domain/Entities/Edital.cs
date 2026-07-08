namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Documento emitido pelo ato de publicação/retificação do
/// <see cref="ProcessoSeletivo"/> (RN08, Story #759) — entidade INTERNA ao
/// agregado: sem CRUD, repositório ou controller próprios. Só é criada e
/// anexada pela raiz, dentro de <see cref="ProcessoSeletivo.Publicar"/>
/// (T4, #785; a retificação chega na T5, #786).
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete) — mesmo padrão de
/// <see cref="EtapaProcesso"/>/<see cref="CriterioDesempate"/>: entidade
/// filha do agregado, criada e persistida exclusivamente pela raiz. Diferente
/// delas, não é substituível por <c>Definir*</c> — um Edital publicado é
/// append-only por natureza (ADR-0101): a única forma de "mudar" é emitir um
/// novo Edital de retificação, nunca editar um existente.
/// </remarks>
public sealed class Edital : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }
    public NaturezaEdital Natureza { get; private set; }
    public string? Numero { get; private set; }
    public DateTimeOffset? DataPublicacao { get; private set; }
    public Guid DocumentoEditalId { get; private set; }

    /// <summary>Nulo em Edital de abertura; obrigatório em retificação (ADR-0101, T5).</summary>
    public Guid? EditalRetificadoId { get; private set; }

    /// <summary>Nulo em Edital de abertura; obrigatório em retificação (ADR-0101, T5).</summary>
    public string? MotivoRetificacao { get; private set; }

    private Edital() { }

    /// <summary>
    /// Emite o Edital de abertura — a primeira publicação do processo.
    /// <see cref="EditalRetificadoId"/>/<see cref="MotivoRetificacao"/> ficam
    /// sempre nulos (contrato abertura×retificação, ADR-0101); a T5 (#786)
    /// introduz <c>EmitirRetificacao</c> para a variante que os exige.
    /// </summary>
    public static Result<Edital> EmitirAbertura(Guid processoSeletivoId, DadosEdital dados, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(clock);

        if (processoSeletivoId == Guid.Empty)
        {
            return Result<Edital>.Failure(new DomainError(
                "Edital.ProcessoSeletivoIdObrigatorio",
                "O Edital deve estar vinculado a um Processo Seletivo."));
        }

        return Result<Edital>.Success(new Edital
        {
            ProcessoSeletivoId = processoSeletivoId,
            Natureza = NaturezaEdital.Abertura,
            Numero = dados.Numero,
            DataPublicacao = clock.GetUtcNow(),
            DocumentoEditalId = dados.DocumentoEditalId,
            EditalRetificadoId = null,
            MotivoRetificacao = null,
        });
    }
}
