namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// A decisão de acesso avaliada, montada com o desfecho que o cenário pede: permitida, negada, impedida ou sem sujeito.
/// Usa uma persona fictícia do portal como solicitante.
/// </summary>
internal static class DecisaoDeAcessoDeTeste
{
    private static readonly AuthorizationRequestContext Requisicao =
        AuthorizationRequestContext.From("req-1", new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), OrigemRequisicao.Api).Value!;

    public static DecisaoDeAcesso Tomada(
        ResultadoDoAcesso resultado, PermissionRequirement requisito, ResourceContext? resource = null)
    {
        ArgumentNullException.ThrowIfNull(requisito);

        if (resultado == ResultadoDoAcesso.IdentidadeIncompleta)
        {
            return SemSujeito.Instancia;
        }

        ResourceContext alvo = resource ?? ResourceContext.From(ResourceTipos.Operacao, requisito.Sensibilidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "diogo.souza").Value!, "jti-teste").Value!;
        AuthorizationDecision decisao = resultado switch
        {
            ResultadoDoAcesso.Permitido => AuthorizationDecision.Permitir(
                EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!),
            ResultadoDoAcesso.Impedido => AuthorizationDecision.Negar(MotivoNegativa.ImpedidoPorInscricaoNoProcesso),
            _ => AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel),
        };

        return DecisaoDeAcesso.Tomada(requisito, alvo, RegistroDecisaoAcesso.De(sujeito, requisito, alvo, Requisicao, decisao));
    }

    public static DecisaoPermitida Permitida(PermissionRequirement requisito, ResourceContext? resource = null) =>
        (DecisaoPermitida)Tomada(ResultadoDoAcesso.Permitido, requisito, resource);
}
