namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Fábricas dos contratos que toda decisão recebe, com os valores neutros do
/// caso feliz — cada teste altera só o que o seu cenário exige, e o que ele não
/// menciona não influencia o resultado.
/// </summary>
internal static class CenariosDeDecisao
{
    public const string Permissao = "selecao:motivos-decisao-isencao:manter";
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
        ClassificacaoDeProtecao sensibilidade = ClassificacaoDeProtecao.Interno,
        Guid? unidadeProprietariaId = null,
        Guid? processoId = null,
        Guid? chamadaId = null,
        Guid? recursoId = null)
        => ResourceContext.From(
            RecursoTipo,
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId,
            recursoId).Value!;

    public static PermissionRequirement Requisito(
        ClassificacaoDeProtecao sensibilidade = ClassificacaoDeProtecao.Interno,
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

    /// <summary>Concessão da permissão do cenário: de unidade quando a unidade é informada, institucional nos demais casos.</summary>
    public static EffectiveGrant Concessao(
        string? permissaoCodigo = null,
        FonteGrant fonte = FonteGrant.Concessao,
        Guid? grantId = null,
        Guid? escopoUnidadeId = null,
        Guid? escopoProcessoId = null,
        Guid? escopoChamadaId = null,
        string? recursoTipoRestricao = null,
        DateTimeOffset? validoAte = null,
        Guid? perfilId = null)
        => EffectiveGrant.From(
            permissaoCodigo ?? Permissao,
            fonte,
            grantId,
            escopoUnidadeId,
            escopoProcessoId,
            escopoChamadaId,
            recursoTipoRestricao,
            validoAte,
            escopoInstitucional: escopoUnidadeId is null,
            perfilId: perfilId).Value!;

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

    /// <summary>Porta das inscrições com os processos em que a pessoa está inscrita combinados pelo teste.</summary>
    public sealed class InscricoesEmMemoria(params Guid[] processosInscritos) : IInscricaoDoSujeitoNoProcesso
    {
        public int Consultas { get; private set; }

        public Task<bool> TemInscricaoEnviadaAsync(string emissor, string subject, Guid processoId, CancellationToken cancellationToken)
        {
            Consultas++;
            return Task.FromResult(processosInscritos.Contains(processoId));
        }
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
