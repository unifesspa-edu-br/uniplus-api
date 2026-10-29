namespace Unifesspa.UniPlus.Authorization.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Recusa a operação sobre um processo seletivo a quem tem nele uma inscrição enviada e não cancelada: o servidor
/// inscrito tem interesse direto no resultado e atua no processo só como candidato (Lei nº 9.784/1999, art. 18, I).
/// </summary>
/// <remarks>
/// <para>
/// É verificação <b>canônica</b>, como <see cref="GrantEfetivoAplicavelCheck"/>: roda em toda decisão cujo recurso traz
/// o processo, sem que a permissão a declare. Vem depois da concessão — quem não tem concessão continua recusado por
/// <see cref="MotivoNegativa.SemConcessaoAplicavel"/> — e nenhuma concessão a supera, nem a aprovada depois da
/// inscrição. Decisão sem processo no recurso não passa por ela.
/// </para>
/// <para>
/// A identidade é o par (emissor, <c>sub</c>) do sujeito da decisão, o mesmo que a inscrição guarda do candidato: o
/// <c>sub</c> sozinho só é único dentro do emissor que o expediu.
/// </para>
/// </remarks>
public sealed class ImpedimentoPorInscricaoCheck : IPermissaoCheck
{
    /// <summary>Nome da verificação.</summary>
    public const string NomeCanonico = "impedimento-por-inscricao-no-processo";

    private readonly IInscricaoDoSujeitoNoProcesso _inscricoes;

    /// <summary>Cria a verificação sobre a porta das inscrições.</summary>
    public ImpedimentoPorInscricaoCheck(IInscricaoDoSujeitoNoProcesso inscricoes)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        _inscricoes = inscricoes;
    }

    /// <inheritdoc />
    public string Nome => NomeCanonico;

    /// <inheritdoc />
    public async Task<CheckResult> CheckAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ProcessoId is not { } processoId)
        {
            return CheckResult.Aprovado();
        }

        bool inscrito = await _inscricoes
            .TemInscricaoEnviadaAsync(subject.Usuario.Emissor, subject.Usuario.Subject, processoId, cancellationToken)
            .ConfigureAwait(false);

        return inscrito
            ? CheckResult.Reprovado(MotivoNegativa.ImpedidoPorInscricaoNoProcesso)
            : CheckResult.Aprovado();
    }
}
