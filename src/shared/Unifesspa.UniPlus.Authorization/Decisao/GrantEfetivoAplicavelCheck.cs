namespace Unifesspa.UniPlus.Authorization.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Seleciona, entre as concessões efetivas do sujeito, uma que autorize a
/// permissão exigida sobre o resource alvo (passos 3 e 4 do algoritmo da
/// ADR-0078).
/// </summary>
/// <remarks>
/// <para>
/// É a verificação <b>canônica</b>: o ponto de decisão sempre a executa, e ela
/// não se declara em <c>decision_checks</c> — o catálogo só lista as
/// verificações contextuais adicionais (ADR-0151).
/// </para>
/// <para>
/// A concessão é comparada contra a lista do <b>sujeito</b>, não contra o token
/// nem contra o banco lidos de novo aqui: o agregador da borda monta o sujeito
/// com as concessões gravadas (<see cref="FonteGrant.Concessao"/>), e o sujeito
/// explícito da ADR-0078 é o que a decisão enxerga. Ler a identidade dentro da
/// verificação reintroduziria o estado ambiental que o modelo elimina.
/// </para>
/// <para>
/// O escopo decide pela unidade dona do resource, sem herança pela hierarquia
/// (ADR-0079): a concessão de unidade alcança só o resource daquela unidade, e a
/// institucional, só o resource sem unidade proprietária. Não há concessão sem
/// escopo, e portanto nenhuma que alcance qualquer resource.
/// </para>
/// </remarks>
public sealed class GrantEfetivoAplicavelCheck : IPermissaoCheck
{
    /// <summary>Nome da verificação.</summary>
    public const string NomeCanonico = "grant-efetivo-aplicavel";

    /// <inheritdoc />
    public string Nome => NomeCanonico;

    /// <inheritdoc />
    public Task<CheckResult> CheckAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(request);

        EffectiveGrant? selecionada = null;
        int especificidadeSelecionada = -1;
        bool houveExpirada = false;

        foreach (EffectiveGrant grant in subject.ConcessoesEfetivas)
        {
            if (!string.Equals(grant.PermissaoCodigo, requirement.Permissao, StringComparison.Ordinal)
                || !EscopoAlcancaResource(grant, resource))
            {
                continue;
            }

            // A validade é comparada com o instante do acesso que veio no
            // contexto, e não com uma leitura nova do relógio: a decisão inteira
            // se explica por um único instante, e o teste a reproduz.
            if (!ConcessaoAtiva.VigenteEm(grant.ValidoAte, request.DataAcesso))
            {
                houveExpirada = true;
                continue;
            }

            // Vence a concessão mais específica; o desempate é a ordem da lista
            // do sujeito, que é estável. Sem isso, duas concessões igualmente
            // aplicáveis fariam a trilha registrar ora uma, ora outra.
            int especificidade = Especificidade(grant);
            if (especificidade > especificidadeSelecionada)
            {
                selecionada = grant;
                especificidadeSelecionada = especificidade;
            }
        }

        if (selecionada is not null)
        {
            return Task.FromResult(CheckResult.Aprovado(selecionada));
        }

        // Distinguir os dois motivos importa ao diagnóstico: "expirou" manda
        // renovar a concessão; "não existe" manda concedê-la.
        return Task.FromResult(CheckResult.Reprovado(
            houveExpirada ? MotivoNegativa.ConcessaoExpirada : MotivoNegativa.SemConcessaoAplicavel));
    }

    // A concessão exige que o resource esteja NO escopo dela. A institucional
    // alcança o resource que não pertence a unidade; a de unidade, o da mesma
    // unidade. Resource sem o escopo que a concessão restringe não é alcançado — a
    // ausência não relaxa a restrição.
    private static bool EscopoAlcancaResource(EffectiveGrant grant, ResourceContext resource) =>
        UnidadeAlcanca(grant, resource.UnidadeProprietariaId)
        && EscopoAlcanca(grant.EscopoProcessoId, resource.ProcessoId)
        && EscopoAlcanca(grant.EscopoChamadaId, resource.ChamadaId)
        && TipoAlcanca(grant.ResourceTipoRestricao, resource.ResourceTipo);

    private static bool UnidadeAlcanca(EffectiveGrant grant, Guid? unidadeDoResource) =>
        grant.EscopoInstitucional
            ? unidadeDoResource is null
            : grant.EscopoUnidadeId is { } unidade && unidade == unidadeDoResource;

    private static bool EscopoAlcanca(Guid? escopoDaConcessao, Guid? escopoDoResource) =>
        escopoDaConcessao is null || escopoDaConcessao == escopoDoResource;

    private static bool TipoAlcanca(string? restricao, string resourceTipo) =>
        string.IsNullOrWhiteSpace(restricao)
        || string.Equals(restricao, resourceTipo, StringComparison.Ordinal);

    private static int Especificidade(EffectiveGrant grant) =>
        1
        + (grant.EscopoProcessoId is null ? 0 : 1)
        + (grant.EscopoChamadaId is null ? 0 : 1)
        + (string.IsNullOrWhiteSpace(grant.ResourceTipoRestricao) ? 0 : 1);
}
