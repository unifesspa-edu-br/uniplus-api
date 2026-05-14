namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Linha da junction table <c>{parent}_areas_de_interesse</c>: vincula uma
/// entidade área-scoped a uma área de interesse com validade temporal
/// (ADR-0057 Pattern 3, ADR-0060). É temporal por construção — adicionar ou
/// remover um binding é <c>INSERT</c> de nova linha / <c>UPDATE valid_to</c>,
/// nunca <c>DELETE</c>; o estado atual e o histórico são a mesma tabela, com
/// queries diferentes.
/// </summary>
/// <typeparam name="TParent">
/// A entidade área-scoped dona da junction. Fechar o genérico (ex.:
/// <c>AreaDeInteresseBinding&lt;Modalidade&gt;</c>) produz um tipo CLR distinto
/// e, portanto, uma tabela EF distinta — alinhado a ADR-0060 (uma junction
/// table por entidade área-scoped).
/// </typeparam>
public sealed class AreaDeInteresseBinding<TParent>
    where TParent : class, IAreaScopedEntity
{
    /// <summary>FK para a entidade área-scoped dona (parte da PK composta).</summary>
    public Guid ParentId { get; private set; }

    /// <summary>Área de interesse vinculada (parte da PK composta).</summary>
    public AreaCodigo AreaCodigo { get; private set; }

    /// <summary>Início da janela de validade do vínculo (parte da PK composta).</summary>
    public DateTimeOffset ValidoDe { get; private set; }

    /// <summary>Fim da janela de validade, ou <see langword="null"/> para o vínculo vigente.</summary>
    public DateTimeOffset? ValidoAte { get; private set; }

    /// <summary>Identificador (<c>sub</c> do JWT) do admin que adicionou o vínculo.</summary>
    public string AdicionadoPor { get; private set; }

    private AreaDeInteresseBinding(
        Guid parentId,
        AreaCodigo areaCodigo,
        DateTimeOffset validoDe,
        string adicionadoPor)
    {
        ParentId = parentId;
        AreaCodigo = areaCodigo;
        ValidoDe = validoDe;
        AdicionadoPor = adicionadoPor;
    }

    // Construtor de materialização do EF Core.
    private AreaDeInteresseBinding() => AdicionadoPor = string.Empty;

    /// <summary>
    /// Abre um vínculo vigente (<see cref="ValidoAte"/> nulo) entre a entidade
    /// área-scoped e a área de interesse.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Factory method em tipo genérico — mesmo padrão de Result<T> no Kernel; "
            + "a alternativa (construtor público) viola a convenção de factory + construtor privado.")]
    public static AreaDeInteresseBinding<TParent> Criar(
        Guid parentId,
        AreaCodigo areaCodigo,
        DateTimeOffset validoDe,
        string adicionadoPor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adicionadoPor);

        return new AreaDeInteresseBinding<TParent>(parentId, areaCodigo, validoDe, adicionadoPor);
    }

    /// <summary>
    /// Encerra a janela de validade do vínculo. O encerramento é a forma
    /// canônica de "remover" um binding — a linha permanece como histórico.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="validoAte"/> não é posterior a
    /// <see cref="ValidoDe"/> — uma janela vazia ou invertida não tem
    /// significado e o <c>tstzrange</c> do exclusion constraint a rejeitaria
    /// tarde, no banco.
    /// </exception>
    public void Encerrar(DateTimeOffset validoAte)
    {
        if (validoAte <= ValidoDe)
        {
            throw new ArgumentException(
                $"validoAte ({validoAte:O}) deve ser posterior a ValidoDe ({ValidoDe:O}).",
                nameof(validoAte));
        }

        ValidoAte = validoAte;
    }
}
