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
    public const string Permissao = "selecao:motivos-de-decisao:manter";
    public const string ResourceTipo = "motivo-de-decisao";

    /// <summary>Instante fixo do acesso — a decisão inteira se explica por ele.</summary>
    public static DateTimeOffset Agora { get; } = new(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);

    public static AuthorizationRequestContext Requisicao(DateTimeOffset? dataAcesso = null)
        => AuthorizationRequestContext.From(
            "req-1244",
            dataAcesso ?? Agora,
            OrigemRequisicao.Api,
            ipOrigem: "200.129.0.1",
            userAgent: "uniplus-web/1.0").Value!;

    public static ResourceContext Resource(
        ClassificacaoDeProtecao sensibilidade = ClassificacaoDeProtecao.Interno,
        Guid? unidadeProprietariaId = null,
        Guid? processoId = null,
        Guid? chamadaId = null,
        Guid? resourceId = null)
        => ResourceContext.From(
            ResourceTipo,
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId,
            resourceId).Value!;

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

    /// <summary>Concessão de perfil da permissão do cenário, com a instituição como escopo.</summary>
    public static EffectiveGrant ConcessaoInstitucional(
        string? permissao = null, Guid? grantId = null, DateTimeOffset? validoAte = null)
        => DeConcessaoDePerfil(EscopoDaPermissaoEfetiva.Instituicao, permissao, grantId, validoAte: validoAte);

    /// <summary>Concessão de perfil da permissão do cenário, com a unidade como escopo.</summary>
    public static EffectiveGrant ConcessaoDaUnidade(
        Guid unidadeId,
        string? permissao = null,
        Guid? grantId = null,
        Guid? perfilId = null,
        DateTimeOffset? validoAte = null)
        => DeConcessaoDePerfil(EscopoDaPermissaoEfetiva.DaUnidade(unidadeId).Value!, permissao, grantId, perfilId, validoAte);

    /// <summary>Concessão de perfil institucional restrita a um processo.</summary>
    public static EffectiveGrant ConcessaoDoProcesso(Guid processoId, Guid? grantId = null)
        => DeConcessaoDePerfil(
            EscopoDaPermissaoEfetiva.Criar(null, processoId, null, null, institucional: true).Value!, grantId: grantId);

    /// <summary>Concessão de perfil institucional restrita a um tipo de resource.</summary>
    public static EffectiveGrant ConcessaoDoTipoDeResource(string resourceTipo)
        => DeConcessaoDePerfil(EscopoDaPermissaoEfetiva.Criar(null, null, null, resourceTipo, institucional: true).Value!);

    /// <summary>Concessão excepcional da permissão do cenário, no escopo e até a validade dados.</summary>
    public static EffectiveGrant ConcessaoExcepcional(
        EscopoDaPermissaoEfetiva escopo, DateTimeOffset validoAte, string? permissao = null)
        => EffectiveGrant.Excepcional(permissao ?? Permissao, escopo, validoAte).Value!;

    /// <summary>Titularidade da permissão do cenário: o registro de quem acessa o que é seu, que nunca autoriza.</summary>
    public static EffectiveGrant ConcessaoDeTitularidade()
        => EffectiveGrant.PorTitularidade(Permissao).Value!;

    private static EffectiveGrant DeConcessaoDePerfil(
        EscopoDaPermissaoEfetiva escopo,
        string? permissao = null,
        Guid? grantId = null,
        Guid? perfilId = null,
        DateTimeOffset? validoAte = null)
        => EffectiveGrant.DaConcessaoDePerfil(permissao ?? Permissao, escopo, grantId, perfilId, validoAte).Value!;

    public static AuthorizationSubject Sujeito(
        bool mfaSatisfeito = false,
        params EffectiveGrant[] concessoes)
        => AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "conta-opaco-123").Value!,
            "jti-abc",
            mfaSatisfeito,
            concessoesEfetivas: concessoes).Value!;

    /// <summary>Registro que apenas guarda o que recebeu, para os testes conferirem.</summary>
    public sealed class RegistroEmMemoria : IRegistroOperacionalRestrito
    {
        public List<RegistroDecisaoAcesso> Registrados { get; } = [];

        /// <summary>Quando definida, a gravação falha com ela.</summary>
        public Exception? Falha { get; set; }

        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
        {
            if (Falha is not null)
            {
                throw Falha;
            }

            Registrados.AddRange(registros);
            return Task.CompletedTask;
        }
    }

    /// <summary>Porta das inscrições com os processos em que a pessoa está inscrita combinados pelo teste.</summary>
    public sealed class InscricoesEmMemoria(params Guid[] processosInscritos) : IInscricaoDoSujeitoNoProcesso
    {
        public int Consultas { get; private set; }

        public Task<bool> TemInscricaoEnviadaAsync(string emissor, string contaId, Guid processoId, CancellationToken cancellationToken)
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

    /// <summary>Porta das designações com as bancas, no lugar que a designação vale, combinadas pelo teste; a resposta muda quando o teste a altera.</summary>
    public sealed class DesignacoesEmMemoria : IDesignacaoDeBanca
    {
        private readonly HashSet<(Guid Processo, VinculosDaBanca Banca)> _designacoes = [];

        public int Consultas { get; private set; }

        public DesignacoesEmMemoria Designar(Guid processoId, VinculosDaBanca banca)
        {
            _designacoes.Add((processoId, banca));
            return this;
        }

        public void Revogar(Guid processoId, VinculosDaBanca banca) => _designacoes.Remove((processoId, banca));

        public Task<bool> TemDesignacaoVigenteAsync(
            string emissor, string contaId, Guid processoId, VinculosDaBanca banca, CancellationToken cancellationToken)
        {
            Consultas++;
            return Task.FromResult(_designacoes.Contains((processoId, banca)));
        }

        public Task<bool> ExisteDesignacaoVigenteAsync(Guid bancaId, CancellationToken cancellationToken) =>
            Task.FromResult(_designacoes.Any(designacao => designacao.Banca.BancaId == bancaId));
    }
}
