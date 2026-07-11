namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Vínculo entre um ato publicado e uma entidade de outro domínio — o par
/// <c>(entidade_tipo, entidade_id)</c> que Publicações guarda <b>sem
/// interpretar</b> (ADR-0105). É o que serve a consulta unificada "todos os atos
/// deste certame" sem que o módulo documental conheça <c>ProcessoSeletivo</c>,
/// <c>Chamada</c> ou configuração de certame.
/// </summary>
/// <remarks>
/// <para>
/// <b>O tipo da entidade é opaco.</b> Não há enumerado fechado, tabela de valores
/// permitidos nem chave estrangeira para tabela de domínio: só a <i>forma</i> do
/// rótulo é verificada (UPPER_SNAKE). Quem dá sentido ao par é o domínio que o
/// populou — <c>Selecao</c> ao publicar um edital, <c>Ingresso</c> ao convocar uma
/// chamada. A única chave estrangeira desta entidade aponta para o próprio ato.
/// </para>
/// <para>
/// <b>Append-only</b> (<see cref="IForensicEntity"/>, ADR-0063), como o ato que o
/// carrega: o vínculo nasce no registro do ato, na mesma transação, e não se muta
/// nem se remove. Um vínculo equivocado tem o mesmo destino de um ato equivocado —
/// permanece, e a correção é publicar um novo ato.
/// </para>
/// <para>
/// Construído exclusivamente a partir do próprio ato
/// (<see cref="Criar(AtoNormativo, string, Guid)"/>): a identidade do ato não é
/// recebida solta, e por isso não há como um vínculo apontar para um ato que não é
/// o seu.
/// </para>
/// </remarks>
public sealed partial class VinculoAtoEntidade : IForensicEntity
{
    private const int EntidadeTipoMaxLength = 60;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Ato publicado a que este vínculo pertence. Única chave estrangeira da entidade.</summary>
    public Guid AtoId { get; private init; }

    /// <summary>
    /// Rótulo opaco do tipo da entidade vinculada (ex.: o processo seletivo, a
    /// chamada). Verificada apenas a forma — o módulo não sabe, e não deve saber,
    /// o que o rótulo significa.
    /// </summary>
    public string EntidadeTipo { get; private init; } = null!;

    /// <summary>Identificador da entidade vinculada, opaco para o módulo (sem chave estrangeira).</summary>
    public Guid EntidadeId { get; private init; }

    // EF Core materialization
    private VinculoAtoEntidade()
    {
    }

    /// <summary>
    /// Cria o vínculo de <paramref name="ato"/> com a entidade
    /// <paramref name="entidadeTipo"/>/<paramref name="entidadeId"/>. Invariantes de
    /// última linha — o validator já recusa o mesmo antes, com 422.
    /// </summary>
    public static VinculoAtoEntidade Criar(AtoNormativo ato, string entidadeTipo, Guid entidadeId)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);

        string tipoNorm = entidadeTipo.Trim();
        if (tipoNorm.Length > EntidadeTipoMaxLength)
        {
            throw new ArgumentException(
                $"Tipo da entidade deve ter no máximo {EntidadeTipoMaxLength} caracteres.",
                nameof(entidadeTipo));
        }

        if (!FormatoDoTipo().IsMatch(tipoNorm))
        {
            throw new ArgumentException(
                "Tipo da entidade deve ser um rótulo em UPPER_SNAKE.",
                nameof(entidadeTipo));
        }

        if (entidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Identificador da entidade vinculada não pode ser vazio.",
                nameof(entidadeId));
        }

        return new VinculoAtoEntidade
        {
            AtoId = ato.Id,
            EntidadeTipo = tipoNorm,
            EntidadeId = entidadeId,
        };
    }

    /// <summary>
    /// Forma do rótulo, não o seu valor: letras maiúsculas e dígitos em grupos
    /// separados por <c>_</c>. Fixa uma grafia canônica — sem ela,
    /// <c>PROCESSO_SELETIVO</c> e <c>processo-seletivo</c> seriam objetos distintos
    /// na consulta, e a mesma entidade se partiria em duas.
    /// </summary>
    [GeneratedRegex(@"^[A-Z0-9]+(_[A-Z0-9]+)*$")]
    private static partial Regex FormatoDoTipo();
}
