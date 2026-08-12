namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) da Unidade administradora viva do módulo
/// Organização Institucional (<c>IUnidadeReader</c>, ADR-0056) no momento em que o
/// Processo Seletivo é criado — os dados de identificação ficam congelados
/// independentemente de atualizações futuras do cadastro de origem.
/// </summary>
/// <remarks>
/// <para>
/// Ao contrário de <see cref="ReferenciaReservaDemograficaSnapshot"/>, não carrega
/// <c>OrigemId</c> internamente: essa identidade já vive em
/// <c>ProcessoSeletivo.UnidadeAdministradoraOrigemId</c> (escalar de topo), e duplicá-la
/// aqui criaria duas fontes para a mesma grandeza.
/// </para>
/// <para>
/// A cidade (issue #1114) é copiada por valor no mesmo instante — <c>Unidade</c> pode
/// ter cidade opcional, mas este VO em si só representa dois estados: com cidade
/// completa (trio válido) ou sem nenhuma. A obrigatoriedade de a Unidade administradora
/// TER cidade para um processo NOVO é regra de negócio do handler que monta este
/// snapshot (<c>CriarProcessoSeletivoCommandHandler</c>, CA-02), não deste VO — que
/// também precisa representar snapshots antigos (pré-#1114), legitimamente sem cidade.
/// </para>
/// </remarks>
public sealed record UnidadeAdministradoraSnapshot
{
    private UnidadeAdministradoraSnapshot(
        string sigla, string slug, string nome, string tipo,
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        Sigla = sigla;
        Slug = slug;
        Nome = nome;
        Tipo = tipo;
        CidadeCodigoIbge = cidadeCodigoIbge;
        CidadeNome = cidadeNome;
        CidadeUf = cidadeUf;
    }

    public string Sigla { get; }
    public string Slug { get; }
    public string Nome { get; }
    public string Tipo { get; }
    public string? CidadeCodigoIbge { get; }
    public string? CidadeNome { get; }
    public string? CidadeUf { get; }

    public static Result<UnidadeAdministradoraSnapshot> Criar(
        string sigla, string slug, string nome, string tipo,
        string? cidadeCodigoIbge = null, string? cidadeNome = null, string? cidadeUf = null)
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

        Result cidadeValidacao = ValidarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidadeValidacao.IsFailure)
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(cidadeValidacao.Error!);
        }

        // CA1062: ValidarReferenciaCidade, acima, já prova a bicondicional entre os três
        // parâmetros — o analisador não relaciona essa prova entre parâmetros distintos.
#pragma warning disable CA1062
        (string? codigo, string? nomeCidade, string? uf) = NormalizarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
#pragma warning restore CA1062

        return Result<UnidadeAdministradoraSnapshot>.Success(new UnidadeAdministradoraSnapshot(
            sigla.Trim(), slug.Trim(), nome.Trim(), tipo.Trim(), codigo, nomeCidade, uf));
    }

    /// <summary>
    /// Referência de cidade opcional all-or-nothing (issue #1114) — mesmo padrão de
    /// <c>Unidade.ValidarReferenciaCidade</c>/<c>Instituicao</c>.
    /// </summary>
    private static Result ValidarReferenciaCidade(string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        bool algumPresente = !string.IsNullOrWhiteSpace(cidadeCodigoIbge)
            || !string.IsNullOrWhiteSpace(cidadeNome)
            || !string.IsNullOrWhiteSpace(cidadeUf);

        return algumPresente
            ? ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf)
            : Result.Success();
    }

    /// <summary>
    /// Normaliza o trio já validado por <see cref="ValidarReferenciaCidade"/> — extraído
    /// para método privado porque o CA1062 não relaciona a prova da bicondicional entre
    /// parâmetros distintos dentro do método público <see cref="Criar"/>.
    /// </summary>
    private static (string? Codigo, string? Nome, string? Uf) NormalizarReferenciaCidade(
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        if (string.IsNullOrWhiteSpace(cidadeCodigoIbge))
        {
            return (null, null, null);
        }

        return (cidadeCodigoIbge.Trim(), cidadeNome!.Trim(), cidadeUf!.Trim().ToUpperInvariant());
    }
}
