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
        RegistroDecisaoAcesso registro = RegistroDecisaoAcesso.De(Sujeito(), requisito, alvo, Requisicao, Decisao(resultado, requisito));

        return DecisaoDeAcesso.Tomada(requisito, alvo, registro);
    }

    public static DecisaoPermitida Permitida(PermissionRequirement requisito, ResourceContext? resource = null) =>
        (DecisaoPermitida)Tomada(ResultadoDoAcesso.Permitido, requisito, resource);

    private static AuthorizationSubject Sujeito() =>
        AuthorizationSubject.From(UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "diogo.souza").Value!, "jti-teste").Value!;

    private static AuthorizationDecision Decisao(ResultadoDoAcesso resultado, PermissionRequirement requisito) =>
        resultado switch
        {
            ResultadoDoAcesso.Permitido => AuthorizationDecision.Permitir(
                EffectiveGrant.DaConcessaoDePerfil(requisito.Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value!),
            ResultadoDoAcesso.Impedido => AuthorizationDecision.Negar(MotivoNegativa.ImpedidoPorInscricaoNoProcesso),
            _ => AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel),
        };
}
