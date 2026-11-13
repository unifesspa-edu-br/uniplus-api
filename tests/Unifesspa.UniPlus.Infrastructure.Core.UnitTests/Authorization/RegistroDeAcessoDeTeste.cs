namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Monta o registro de acesso de uma persona fictícia, com o que o cenário do teste varia.</summary>
internal static class RegistroDeAcessoDeTeste
{
    public const string LeituraPorTitularidade = "autorizacao:pedidos:ler-proprios";

    private const string BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III";

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
        PermissionRequirement requisito = PermissionRequirement.From("selecao:inscricoes:consultar", sensibilidade, BaseLegal).Value!;
        ResourceContext resource = ResourceContext.From("inscricao", sensibilidade, resourceId: resourceId, inscricaoId: resourceId).Value!;
        AuthorizationRequestContext requisicao = AuthorizationRequestContext.From(
            requestId, Instante, OrigemRequisicao.Api, execucaoId: execucaoId ?? Execucao, clienteOidc: clienteOidc).Value!;

        return RegistroDecisaoAcesso.De(Sujeito(subject), requisito, resource, requisicao, Decisao(requisito, resultado));
    }

    /// <summary>O registro da leitura que a parte faz do próprio pedido, sem concessão, como o verificador de acesso o monta.</summary>
    public static RegistroDecisaoAcesso PorTitularidade(Guid pedido)
    {
        PermissionRequirement operacao = PermissionRequirement.From(LeituraPorTitularidade, ClassificacaoDeProtecao.Pessoal).Value!;
        ResourceContext alvo = ResourceContext.From("pedido", ClassificacaoDeProtecao.Pessoal, resourceId: pedido).Value!;
        AuthorizationRequestContext requisicao = AuthorizationRequestContext.From("req-1", Instante, OrigemRequisicao.Api, execucaoId: Execucao).Value!;
        AuthorizationDecision titularidade = AuthorizationDecision.Permitir(EffectiveGrant.From(LeituraPorTitularidade, FonteGrant.Titularidade).Value!);

        return RegistroDecisaoAcesso.De(Sujeito("leandro.viana"), operacao, alvo, requisicao, titularidade);
    }

    private static DateTimeOffset Instante => new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static AuthorizationSubject Sujeito(string subject) =>
        AuthorizationSubject.From(UsuarioRef.From("https://idp.exemplo/realms/unifesspa", subject).Value!, "jti-teste").Value!;

    private static AuthorizationDecision Decisao(PermissionRequirement requisito, ResultadoDoAcesso resultado) => resultado switch
    {
        ResultadoDoAcesso.Permitido => AuthorizationDecision.Permitir(
            EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!),
        ResultadoDoAcesso.Impedido => AuthorizationDecision.Negar(MotivoNegativa.ImpedidoPorInscricaoNoProcesso),
        _ => AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel),
    };
}
