namespace Unifesspa.UniPlus.Authorization.Errors;

/// <summary>
/// Códigos estáveis de erro de validação das fábricas dos tipos formais do
/// contrato de autorização (ADR-0078). Distintos do <c>MotivoNegativa</c>: estes
/// sinalizam construção inválida de um value object; aquele explica uma negativa
/// de acesso já decidida.
/// </summary>
public static class AuthorizationErrorCodes
{
    public const string UsuarioRefEmissorObrigatorio = "Authorization.UsuarioRef.EmissorObrigatorio";
    public const string UsuarioRefSubjectObrigatorio = "Authorization.UsuarioRef.SubjectObrigatorio";

    public const string EffectiveGrantPermissaoObrigatoria = "Authorization.EffectiveGrant.PermissaoObrigatoria";
    public const string EffectiveGrantValidadeObrigatoria = "Authorization.EffectiveGrant.ValidadeObrigatoria";

    public const string EscopoAuditoriaEscopoObrigatorio = "Authorization.EscopoAuditoriaVigente.EscopoObrigatorio";

    public const string AtuacaoUnidadeObrigatoria = "Authorization.AtuacaoVigente.UnidadeObrigatoria";

    public const string DualApprovalPermissaoObrigatoria = "Authorization.DualApprovalGrant.PermissaoObrigatoria";
    public const string DualApprovalRecursoHashObrigatorio = "Authorization.DualApprovalGrant.RecursoHashObrigatorio";
    public const string DualApprovalAprovadoresIguais = "Authorization.DualApprovalGrant.AprovadoresIguais";
    public const string DualApprovalValidadeNaoPosterior = "Authorization.DualApprovalGrant.ValidadeNaoPosterior";
    public const string DualApprovalValidadeAcimaDoLimite = "Authorization.DualApprovalGrant.ValidadeAcimaDoLimite";
}
