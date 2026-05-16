namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Kernel.Results;
using ValueObjects;

/// <summary>
/// Placeholder mínimo da entidade <c>ObrigatoriedadeLegal</c> para destravar
/// a compilação da Story #459 (avaliador de conformidade). A forma plena
/// — herdando <c>EntityBase</c>, com <c>Vigencia</c>, <c>Hash</c> canônico,
/// <c>TipoEditalCodigo</c>, <c>Categoria</c>, governança per ADR-0057 e
/// audit interceptor — entra em <c>#460</c> (US-F4-02).
/// </summary>
/// <remarks>
/// <para>Em V1 a entidade é um <c>sealed record</c> imutável sem persistência
/// (não há EF mapping, repositório nem migration nesta Story). O avaliador
/// recebe a coleção de regras como argumento — o caller é responsável por
/// hidratá-las (em testes, manualmente; em #461 admin CRUD via repository).</para>
/// <para>Construção via factory <see cref="Criar"/> retornando
/// <see cref="Result{T}"/> (path canônico) <strong>ou</strong> diretamente via
/// construtor que valida e lança <see cref="ArgumentException"/> em entradas
/// inválidas. Os dois caminhos aplicam a mesma rede de invariantes — não há
/// bypass mesmo via deserialização polimórfica STJ.</para>
/// </remarks>
public sealed record ObrigatoriedadeLegal
{
    public string RegraCodigo { get; }
    public PredicadoObrigatoriedade Predicado { get; }
    public string BaseLegal { get; }
    public string DescricaoHumana { get; }
    public string? PortariaInternaCodigo { get; }

    public ObrigatoriedadeLegal(
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string baseLegal,
        string descricaoHumana,
        string? portariaInternaCodigo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regraCodigo);
        ArgumentNullException.ThrowIfNull(predicado);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLegal);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricaoHumana);

        RegraCodigo = regraCodigo.Trim();
        Predicado = predicado;
        BaseLegal = baseLegal.Trim();
        DescricaoHumana = descricaoHumana.Trim();
        PortariaInternaCodigo = string.IsNullOrWhiteSpace(portariaInternaCodigo)
            ? null
            : portariaInternaCodigo.Trim();
    }

    /// <summary>
    /// Factory canônica retornando <see cref="Result{T}"/> com
    /// <see cref="DomainError"/> codificado (compatível com
    /// <c>SelecaoDomainErrorRegistration</c>). Wrappers de aplicação devem
    /// preferir esta API à construção direta.
    /// </summary>
    public static Result<ObrigatoriedadeLegal> Criar(
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string baseLegal,
        string descricaoHumana,
        string? portariaInternaCodigo = null)
    {
        if (string.IsNullOrWhiteSpace(regraCodigo))
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.RegraCodigoObrigatorio",
                "RegraCodigo é obrigatório."));

        if (predicado is null)
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.PredicadoObrigatorio",
                "Predicado é obrigatório."));

        if (string.IsNullOrWhiteSpace(baseLegal))
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.BaseLegalObrigatoria",
                "BaseLegal é obrigatória."));

        if (string.IsNullOrWhiteSpace(descricaoHumana))
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.DescricaoHumanaObrigatoria",
                "DescricaoHumana é obrigatória."));

        return Result<ObrigatoriedadeLegal>.Success(new ObrigatoriedadeLegal(
            regraCodigo,
            predicado,
            baseLegal,
            descricaoHumana,
            portariaInternaCodigo));
    }
}
