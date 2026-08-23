namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Fábricas dos contratos que toda decisão recebe, com os valores neutros do
/// caso feliz — cada teste altera só o que o seu cenário exige, e o que ele não
/// menciona não influencia o resultado.
/// </summary>
internal static class CenariosDeDecisao
{
    public const string Permissao = "configuracao:motivos-decisao-recursal:manter";
    public const string RecursoTipo = "MotivoDecisaoRecursal";

    /// <summary>Instante fixo do acesso — a decisão inteira se explica por ele.</summary>
    public static DateTimeOffset Agora { get; } = new(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);

    public static AuthorizationRequestContext Requisicao(DateTimeOffset? dataAcesso = null)
        => AuthorizationRequestContext.From(
            "req-1244",
            dataAcesso ?? Agora,
            OrigemRequisicao.Api,
            ipOrigem: "200.129.0.1",
            userAgent: "uniplus-web/1.0").Value!;

    public static ResourceContext Recurso(
        Sensibilidade sensibilidade = Sensibilidade.Interna,
        Guid? unidadeProprietariaId = null,
        Guid? processoId = null,
        Guid? chamadaId = null)
        => ResourceContext.From(
            RecursoTipo,
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId).Value!;

    public static PermissionRequirement Requisito(
        Sensibilidade sensibilidade = Sensibilidade.Interna,
        string? baseLegalPadrao = null,
        bool requerMfa = false,
        bool requerDuplaAprovacao = false,
        IEnumerable<string>? escopoContextoObrigatorio = null,
        IEnumerable<string>? verificacoesDeContexto = null)
        => PermissionRequirement.From(
            Permissao,
            sensibilidade,
            baseLegalPadrao,
            requerMfa,
            requerDuplaAprovacao,
            escopoContextoObrigatorio,
            verificacoesDeContexto).Value!;

    public static EffectiveGrant Concessao(
        string? permissaoCodigo = null,
        FonteGrant fonte = FonteGrant.Token,
        Guid? grantId = null,
        Guid? escopoUnidadeId = null,
        Guid? escopoProcessoId = null,
        Guid? escopoChamadaId = null,
        string? recursoTipoRestricao = null,
        DateTimeOffset? validoAte = null)
        => EffectiveGrant.From(
            permissaoCodigo ?? Permissao,
            fonte,
            grantId,
            escopoUnidadeId,
            escopoProcessoId,
            escopoChamadaId,
            recursoTipoRestricao,
            validoAte).Value!;

    public static AuthorizationSubject Sujeito(
        bool mfaSatisfeito = false,
        params EffectiveGrant[] concessoes)
        => AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!,
            "jti-abc",
            mfaSatisfeito,
            concessoesEfetivas: concessoes).Value!;

    /// <summary>Registro que apenas guarda o que recebeu, para os testes conferirem.</summary>
    public sealed class RegistroEmMemoria : IRegistroOperacionalRestrito
    {
        public List<RegistroDecisaoAcesso> Registrados { get; } = [];

        public void Registrar(RegistroDecisaoAcesso registro) => Registrados.Add(registro);
    }

    /// <summary>Verificação de contexto com resultado combinado pelo teste.</summary>
    public sealed class CheckDuplo(string nome, CheckResult resultado) : IPermissaoCheck
    {
        public string Nome { get; } = nome;

        public int Execucoes { get; private set; }

        public Task<CheckResult> CheckAsync(
            AuthorizationSubject subject,
            PermissionRequirement requirement,
            ResourceContext resource,
            AuthorizationRequestContext request,
            CancellationToken cancellationToken)
        {
            Execucoes++;
            return Task.FromResult(resultado);
        }
    }
}
