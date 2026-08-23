namespace Unifesspa.UniPlus.Authorization.Decisao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Seleciona, entre as concessões efetivas do sujeito, uma que autorize a
/// permissão exigida sobre o recurso alvo (passos 3 e 4 do algoritmo da
/// ADR-0078).
/// </summary>
/// <remarks>
/// <para>
/// É a verificação <b>canônica</b>: o ponto de decisão sempre a executa, e ela
/// não se declara em <c>decision_checks</c> — o catálogo só lista as
/// verificações contextuais adicionais (ADR-0080).
/// </para>
/// <para>
/// A concessão é comparada contra a lista do <b>sujeito</b>, não contra o token
/// lido de novo aqui: nesta fatia o agregador da borda converte cada papel do
/// <i>client</i> da API em uma concessão de fonte <see cref="FonteGrant.Token"/>,
/// e o sujeito explícito da ADR-0078 é o que a decisão enxerga. Ler a identidade
/// dentro da verificação reintroduziria o estado ambiental que o modelo elimina.
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
                || !EscopoAlcancaRecurso(grant, resource))
            {
                continue;
            }

            // A validade é comparada com o instante do acesso que veio no
            // contexto, e não com uma leitura nova do relógio: a decisão inteira
            // se explica por um único instante, e o teste a reproduz.
            if (grant.ValidoAte is { } validade && validade < request.DataAcesso)
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

    // Uma concessão sem escopo é global e alcança qualquer recurso; uma com
    // escopo exige que o recurso esteja NAQUELE escopo. Recurso sem o escopo que
    // a concessão restringe não é alcançado — a ausência não relaxa a restrição.
    private static bool EscopoAlcancaRecurso(EffectiveGrant grant, ResourceContext resource) =>
        EscopoAlcanca(grant.EscopoUnidadeId, resource.UnidadeProprietariaId)
        && EscopoAlcanca(grant.EscopoProcessoId, resource.ProcessoId)
        && EscopoAlcanca(grant.EscopoChamadaId, resource.ChamadaId)
        && TipoAlcanca(grant.RecursoTipoRestricao, resource.RecursoTipo);

    private static bool EscopoAlcanca(Guid? escopoDaConcessao, Guid? escopoDoRecurso) =>
        escopoDaConcessao is null || escopoDaConcessao == escopoDoRecurso;

    private static bool TipoAlcanca(string? restricao, string recursoTipo) =>
        string.IsNullOrWhiteSpace(restricao)
        || string.Equals(restricao, recursoTipo, StringComparison.Ordinal);

    private static int Especificidade(EffectiveGrant grant) =>
        (grant.EscopoUnidadeId is null ? 0 : 1)
        + (grant.EscopoProcessoId is null ? 0 : 1)
        + (grant.EscopoChamadaId is null ? 0 : 1)
        + (string.IsNullOrWhiteSpace(grant.RecursoTipoRestricao) ? 0 : 1);
}
