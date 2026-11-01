namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Um verificador de acesso de teste que decide cada permissão pelo que a persona tem concedido, devolvendo a decisão sem
/// registro que o ponto de decisão devolveria. Quem não tem a permissão recebe a negativa.
/// </summary>
internal sealed class DecisoesDeAcessoDeTeste
{
    private static readonly Guid Unidade = Guid.CreateVersion7();

    private readonly HashSet<string> _concedidas;

    public DecisoesDeAcessoDeTeste(string persona, params PermissaoSelecao[] concedidas)
    {
        _concedidas = [.. concedidas.Select(static p => p.Codigo)];
        Persona = persona;
        Verificador = Substitute.For<IVerificadorDeAcesso>();
        Verificador.DecidirSemRegistrarAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => Task.FromResult(Decidir(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1)!)));
        Verificador.VerificarAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => Task.FromResult(
                Decidir(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1)!).Resultado));
    }

    public string Persona { get; }

    public IVerificadorDeAcesso Verificador { get; }

    public static Guid UnidadeDona => Unidade;

    public DecisaoDeAcesso Decidir(PermissionRequirement requisito, ResourceContext resource)
    {
        bool permitida = _concedidas.Contains(requisito.Permissao);
        EffectiveGrant concessao = EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", Persona).Value!, "jti", concessoesEfetivas: [concessao]).Value!;
        AuthorizationRequestContext contexto = AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!;
        AuthorizationDecision decisao = permitida
            ? AuthorizationDecision.Permitir(concessao)
            : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);

        return DecisaoDeAcesso.Tomada(
            permitida ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado,
            RegistroDecisaoAcesso.De(sujeito, requisito, resource, contexto, decisao));
    }
}
