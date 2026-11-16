namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Runtime.CompilerServices;

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
        string sigla, string codigo, string slug, string nome, string tipo,
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        Sigla = sigla;
        Codigo = codigo;
        Slug = slug;
        Nome = nome;
        Tipo = tipo;
        CidadeCodigoIbge = cidadeCodigoIbge;
        CidadeNome = cidadeNome;
        CidadeUf = cidadeUf;
    }

    public string Sigla { get; }
    public string Codigo { get; }
    public string Slug { get; }
    public string Nome { get; }
    public string Tipo { get; }
    public string? CidadeCodigoIbge { get; }
    public string? CidadeNome { get; }
    public string? CidadeUf { get; }

    /// <summary>
    /// Sigla, código, slug, nome e tipo vêm do cadastro vivo da unidade, que já os exige: vazios são defeito de quem chama, não recusa a
    /// um cliente, e lançam <see cref="ArgumentException"/>. A referência de cidade é a que pode falhar como resultado.
    /// </summary>
    public static Result<UnidadeAdministradoraSnapshot> Criar(
        string sigla, string codigo, string slug, string nome, string tipo,
        string? cidadeCodigoIbge = null, string? cidadeNome = null, string? cidadeUf = null)
    {
        Result cidadeValidacao = ValidarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidadeValidacao.IsFailure)
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(cidadeValidacao.Error!);
        }

        (string? cidadeIbge, string? nomeCidade, string? uf) = NormalizarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);

        return Result<UnidadeAdministradoraSnapshot>.Success(new UnidadeAdministradoraSnapshot(
            Obrigatorio(sigla), Obrigatorio(codigo), Obrigatorio(slug), Obrigatorio(nome), Obrigatorio(tipo), cidadeIbge, nomeCidade, uf));
    }

    private static string Obrigatorio(string? valor, [CallerArgumentExpression(nameof(valor))] string? campo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, campo);
        return valor.Trim();
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

    /// <summary>Normaliza o trio já validado por <see cref="ValidarReferenciaCidade"/>: ou os três presentes, ou nenhum.</summary>
    private static (string? Codigo, string? Nome, string? Uf) NormalizarReferenciaCidade(
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf) =>
        string.IsNullOrWhiteSpace(cidadeCodigoIbge)
            ? (null, null, null)
            : (cidadeCodigoIbge.Trim(), cidadeNome?.Trim(), cidadeUf?.Trim().ToUpperInvariant());
}
