namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
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
    private readonly HashSet<Guid> _bancasDesignadas = [];

    public DecisoesDeAcessoDeTeste(string persona, params PermissaoSelecao[] concedidas)
    {
        _concedidas = [.. concedidas.Select(static p => p.Codigo)];
        Persona = persona;
        Verificador = Substitute.For<IVerificadorDeAcesso>();
        Verificador.AvaliarAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => Task.FromResult(Decidir(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1)!)));
        Verificador.ExigirAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => Task.FromResult(
                Decidir(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1)!).Resultado));
        Verificador.PodeAsync(default!, default, default)
            .ReturnsForAnyArgs(chamada => Task.FromResult(Pode(chamada.ArgAt<PermissionRequirement>(0), chamada.ArgAt<ResourceContext?>(1))));
    }

    public string Persona { get; }

    public IVerificadorDeAcesso Verificador { get; }

    public static Guid UnidadeDona => Unidade;

    /// <summary>A persona está designada a esta banca: é o que a verificação de designação confere nas permissões que a declaram.</summary>
    public DecisoesDeAcessoDeTeste DesignadaA(Guid bancaId)
    {
        _bancasDesignadas.Add(bancaId);
        return this;
    }

    private ResultadoDoAcesso Pode(PermissionRequirement requisito, ResourceContext? resource) =>
        resource is null
            ? (_concedidas.Contains(requisito.Permissao) ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado)
            : Decidir(requisito, resource).Resultado;

    private bool ForaDaDesignacao(PermissionRequirement requisito, ResourceContext resource) =>
        requisito.VerificacoesDeContexto.Contains(DesignacaoNaBancaCheck.NomeCanonico)
        && (resource.Banca is not { } banca || !_bancasDesignadas.Contains(banca.BancaId));

    public DecisaoDeAcesso Decidir(PermissionRequirement requisito, ResourceContext resource)
    {
        bool permitida = _concedidas.Contains(requisito.Permissao) && !ForaDaDesignacao(requisito, resource);
        EffectiveGrant concessao = EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", Persona).Value!, "jti", concessoesEfetivas: [concessao]).Value!;
        AuthorizationRequestContext contexto = AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!;
        AuthorizationDecision decisao = permitida
            ? AuthorizationDecision.Permitir(concessao)
            : AuthorizationDecision.Negar(
                _concedidas.Contains(requisito.Permissao) ? MotivoNegativa.ForaDaDesignacao : MotivoNegativa.SemConcessaoAplicavel);

        return DecisaoDeAcesso.Tomada(requisito, resource, RegistroDecisaoAcesso.De(sujeito, requisito, resource, contexto, decisao));
    }
}
