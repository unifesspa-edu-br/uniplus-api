namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// O verificador de acesso dos testes: a identidade informada tem as permissões listadas e nenhuma outra. Guarda o que o
/// verificador real gravaria como desfecho.
/// </summary>
internal sealed class VerificadorDeTeste(string sub, string[] permitidas) : IVerificadorDeAcesso
{
    public List<IReadOnlyCollection<RegistroDecisaoAcesso>> Gravados { get; } = [];

    public Task<DecisaoDeAcesso> AvaliarAsync(
        PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default)
    {
        ResourceContext alvo = resource ?? ResourceContext.From(ResourceTipos.Operacao, requisito.Sensibilidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", sub).Value!, "jti-teste").Value!;
        AuthorizationDecision decisao = permitidas.Contains(requisito.Permissao)
            ? AuthorizationDecision.Permitir(EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!)
            : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);
        AuthorizationRequestContext contexto = AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!;

        return Task.FromResult(DecisaoDeAcesso.Tomada(
            requisito, alvo, RegistroDecisaoAcesso.De(sujeito, requisito, alvo, contexto, decisao)));
    }

    // O que o verificador real gravaria: a política de registro aplicada às decisões do desfecho.
    public Task RegistrarDesfechoAsync(
        IReadOnlyCollection<DecisaoDeAcesso> decisoes, IntencaoDoRegistro intencao, CancellationToken cancellationToken = default)
    {
        Gravados.Add(PoliticaDeRegistroDeAcesso.Desfecho(decisoes, intencao));
        return Task.CompletedTask;
    }

    public Task<ResultadoDoAcesso> ExigirAsync(PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DecisaoDeAcesso>> AvaliarEmAlgumaUnidadeAsync(PermissionRequirement requisito, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ResultadoDoAcesso> PodeAsync(PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task RegistrarParaCadaAsync(
        DecisaoPermitida permitida, string resourceTipo, IReadOnlyCollection<Guid> resourceIds, bool resourceEhInscricao = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
