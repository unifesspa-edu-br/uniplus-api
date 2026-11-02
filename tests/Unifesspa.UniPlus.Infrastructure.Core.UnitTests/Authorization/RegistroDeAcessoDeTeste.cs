namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Monta o registro de acesso de uma persona fictícia, com o que o cenário do teste varia.</summary>
internal static class RegistroDeAcessoDeTeste
{
    public static readonly Guid Execucao = Guid.Parse("0197f6c2-6f3a-7c1e-9d2b-3f4a5b6c7d8e");

    public static RegistroDecisaoAcesso Registro(
        Guid? execucaoId = null,
        ClassificacaoDeProtecao sensibilidade = ClassificacaoDeProtecao.Pessoal,
        ResultadoDoAcesso resultado = ResultadoDoAcesso.Permitido,
        string? clienteOidc = null,
        string requestId = "req-1",
        Guid? resourceId = null,
        string subject = "leandro.viana")
    {
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", subject).Value!, "jti-teste").Value!;
        PermissionRequirement requisito = PermissionRequirement.From(
            "selecao:inscricoes:consultar", sensibilidade, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!;
        ResourceContext resource = ResourceContext.From("inscricao", sensibilidade, resourceId: resourceId, inscricaoId: resourceId).Value!;
        AuthorizationRequestContext requisicao = AuthorizationRequestContext.From(
            requestId, new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), OrigemRequisicao.Api,
            execucaoId: execucaoId ?? Execucao, clienteOidc: clienteOidc).Value!;
        EffectiveGrant concessao = EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!;
        AuthorizationDecision decisao = resultado switch
        {
            ResultadoDoAcesso.Permitido => AuthorizationDecision.Permitir(concessao),
            ResultadoDoAcesso.Impedido => AuthorizationDecision.Negar(MotivoNegativa.ImpedidoPorInscricaoNoProcesso),
            _ => AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel),
        };

        return RegistroDecisaoAcesso.De(sujeito, requisito, resource, requisicao, decisao);
    }
}
