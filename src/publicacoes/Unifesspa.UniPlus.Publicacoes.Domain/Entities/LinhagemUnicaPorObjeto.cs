namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A vaga que um objeto reserva para uma única linhagem de atos de um tipo
/// <c>unico_por_objeto</c> (ADR-0107). Materializa, numa chave que o banco sabe
/// travar, a invariante "para tipos assim, um objeto é tratado por uma só linhagem
/// de atos".
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que uma tabela, e não um índice sobre o vínculo.</b> A invariante atravessa
/// duas tabelas — o atributo <c>unico_por_objeto</c> e a linhagem estão no ato; o objeto,
/// no vínculo — e o Postgres não indexa unicidade entre tabelas. Denormalizar "este ato é
/// raiz" no vínculo <b>não</b> resolve: bastaria publicar uma raiz sem vínculo e, na
/// retificação dela, vincular o objeto já tratado por outra linhagem — a retificação não é
/// raiz, escaparia do índice, e o objeto acabaria com duas linhagens vivas. A chave certa
/// é a linhagem, não a posição do ato nela.
/// </para>
/// <para>
/// <b>Linhagem, não ato.</b> <see cref="RaizId"/> identifica a cadeia de retificação
/// inteira (o Id da sua raiz). Uma retificação da linhagem que já detém a vaga reencontra
/// a mesma <see cref="RaizId"/> e segue; um ato de outra linhagem colide, e é recusado com
/// 409 (ADR-0103: uma nova versão do ato vivo é uma retificação, não um ato novo).
/// </para>
/// <para>
/// Insert-only (<see cref="IForensicEntity"/>, ADR-0063), como tudo neste módulo: a vaga
/// não se transfere nem se libera — não há revogação de ato publicado.
/// </para>
/// </remarks>
public sealed class LinhagemUnicaPorObjeto : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Tipo da entidade tratada, opaco para o módulo (ver <see cref="VinculoAtoEntidade"/>).</summary>
    public string EntidadeTipo { get; private init; } = null!;

    /// <summary>Identificador da entidade tratada, opaco para o módulo.</summary>
    public Guid EntidadeId { get; private init; }

    /// <summary>
    /// Código do tipo de ato, copiado por valor do ato (ADR-0075). A vaga é por
    /// <c>(objeto, tipo)</c>: um processo seletivo tem um edital de abertura vivo e,
    /// ainda assim, quantos avisos forem precisos.
    /// </summary>
    public string TipoCodigo { get; private init; } = null!;

    /// <summary>Raiz da cadeia de retificação que ocupa a vaga — a identidade da linhagem.</summary>
    public Guid RaizId { get; private init; }

    /// <summary>
    /// Ato que abriu a vaga. Não é necessariamente a raiz: uma raiz publicada sem vínculo
    /// e vinculada só na retificação faz a própria retificação abrir a vaga — em nome da
    /// linhagem, cuja raiz continua sendo <see cref="RaizId"/>.
    /// </summary>
    public Guid AtoId { get; private init; }

    // EF Core materialization
    private LinhagemUnicaPorObjeto()
    {
    }

    /// <summary>
    /// Abre a vaga do objeto de <paramref name="vinculo"/> em nome da linhagem
    /// <paramref name="raizId"/>. O tipo de ato e o ato que a abre vêm do próprio
    /// <paramref name="ato"/> — não são recebidos soltos, e por isso não podem divergir
    /// dele.
    /// </summary>
    public static LinhagemUnicaPorObjeto Criar(AtoNormativo ato, VinculoAtoEntidade vinculo, Guid raizId)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(vinculo);

        if (raizId == Guid.Empty)
        {
            throw new ArgumentException("Raiz da linhagem não pode ser vazia.", nameof(raizId));
        }

        if (!ato.UnicoPorObjeto)
        {
            throw new InvalidOperationException(
                "Só um ato de tipo único por objeto reserva a vaga de um objeto.");
        }

        return new LinhagemUnicaPorObjeto
        {
            EntidadeTipo = vinculo.EntidadeTipo,
            EntidadeId = vinculo.EntidadeId,
            TipoCodigo = ato.TipoCodigo,
            RaizId = raizId,
            AtoId = ato.Id,
        };
    }
}
