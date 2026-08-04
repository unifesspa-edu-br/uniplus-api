namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) da Unidade administradora viva do módulo
/// Organização Institucional (<c>IUnidadeReader</c>, ADR-0056) no momento em que o
/// Processo Seletivo é criado — os dados de identificação ficam congelados
/// independentemente de atualizações futuras do cadastro de origem.
/// </summary>
/// <remarks>
/// Ao contrário de <see cref="ReferenciaReservaDemograficaSnapshot"/>, não carrega
/// <c>OrigemId</c> internamente: essa identidade já vive em
/// <c>ProcessoSeletivo.UnidadeAdministradoraOrigemId</c> (escalar de topo), e duplicá-la
/// aqui criaria duas fontes para a mesma grandeza.
/// </remarks>
public sealed record UnidadeAdministradoraSnapshot
{
    private UnidadeAdministradoraSnapshot(string sigla, string slug, string nome, string tipo)
    {
        Sigla = sigla;
        Slug = slug;
        Nome = nome;
        Tipo = tipo;
    }

    public string Sigla { get; }
    public string Slug { get; }
    public string Nome { get; }
    public string Tipo { get; }

    public static Result<UnidadeAdministradoraSnapshot> Criar(string sigla, string slug, string nome, string tipo)
    {
        if (string.IsNullOrWhiteSpace(sigla))
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(new DomainError(
                "UnidadeAdministradoraSnapshot.SiglaObrigatoria", "Sigla da unidade administradora é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(new DomainError(
                "UnidadeAdministradoraSnapshot.SlugObrigatorio", "Slug da unidade administradora é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(new DomainError(
                "UnidadeAdministradoraSnapshot.NomeObrigatorio", "Nome da unidade administradora é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(tipo))
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(new DomainError(
                "UnidadeAdministradoraSnapshot.TipoObrigatorio", "Tipo da unidade administradora é obrigatório."));
        }

        return Result<UnidadeAdministradoraSnapshot>.Success(new UnidadeAdministradoraSnapshot(
            sigla.Trim(), slug.Trim(), nome.Trim(), tipo.Trim()));
    }
}
