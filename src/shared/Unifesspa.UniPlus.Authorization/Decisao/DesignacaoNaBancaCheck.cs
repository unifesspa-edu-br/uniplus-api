namespace Unifesspa.UniPlus.Authorization.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Recusa o ato sobre a banca a quem não está designado a ela: a concessão da permissão diz que a pessoa pode atuar
/// como membro de banca na unidade, e a designação diz em qual banca.
/// </summary>
/// <remarks>
/// Declarada pela permissão que a exige, depois da concessão e do impedimento por inscrição. Resource sem processo ou
/// sem os vínculos da banca é recusado por <see cref="MotivoNegativa.ContextoObrigatorioAusente"/>, nunca aprovado: o
/// que não situa a banca não prova designação. Os campos que cada ato exige além desses (etapa, inscrição) são
/// declarados pela permissão em <c>EscopoContextoObrigatorio</c> e conferidos antes das verificações de contexto.
/// </remarks>
public sealed class DesignacaoNaBancaCheck : IPermissaoCheck
{
    /// <summary>Nome da verificação, o mesmo que o catálogo declara.</summary>
    public const string NomeCanonico = "designacao-na-banca";

    private readonly IDesignacaoDeBanca _designacoes;

    /// <summary>Cria a verificação sobre a porta das designações.</summary>
    public DesignacaoNaBancaCheck(IDesignacaoDeBanca designacoes)
    {
        ArgumentNullException.ThrowIfNull(designacoes);

        _designacoes = designacoes;
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

        if (resource.ProcessoId is not { } processoId || resource.Banca is not { } banca)
        {
            return CheckResult.Reprovado(MotivoNegativa.ContextoObrigatorioAusente);
        }

        bool designado = await _designacoes
            .TemDesignacaoVigenteAsync(subject.Usuario.Emissor, subject.Usuario.Subject, processoId, banca, cancellationToken)
            .ConfigureAwait(false);

        return designado
            ? CheckResult.Aprovado()
            : CheckResult.Reprovado(MotivoNegativa.ForaDaDesignacao);
    }
}
