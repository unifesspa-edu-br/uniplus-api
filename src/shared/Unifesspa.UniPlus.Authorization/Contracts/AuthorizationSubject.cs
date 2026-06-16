namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Sujeito explícito de uma decisão de autorização (ADR-0078): quem pede o
/// acesso, com tudo o que a decisão consulta sobre ele. O ator é <b>sempre</b>
/// este sujeito explícito — nunca estado ambiental. Carrega a identidade
/// (<see cref="Usuario"/>), os grupos do token, as unidades administradas
/// (derivadas no servidor), os snapshots de auditoria e atuação vigentes, o
/// indicador de multifator, o identificador do token (<see cref="Jti"/>, usado
/// para revogação) e as <b>concessões efetivas</b> — que unificam, numa única
/// lista somente-leitura, concessões de fontes distintas (token, vínculo de
/// grupo, concessão excepcional), cada uma com fonte rastreável.
/// </summary>
public sealed record AuthorizationSubject
{
    /// <summary>Identidade do sujeito (emissor + subject do token).</summary>
    public UsuarioRef Usuario { get; }

    /// <summary>Grupos do token (strings de grupo, sem amarração a provedor).</summary>
    public IReadOnlySet<string> GruposOidc { get; }

    /// <summary>Unidades que o sujeito administra, derivadas no servidor.</summary>
    public IReadOnlySet<Guid> UnidadesAdministradas { get; }

    /// <summary>Escopos de auditoria vigentes (snapshots de contrato).</summary>
    public IReadOnlyList<EscopoAuditoriaVigente> EscoposAuditoria { get; }

    /// <summary>Atuação institucional ativa (sessão delegada). Opcional.</summary>
    public AtuacaoVigente? AtuacaoAtiva { get; }

    /// <summary>Multifator satisfeito nesta sessão.</summary>
    public bool MfaSatisfeito { get; }

    /// <summary>Identificador do token (<c>jti</c>) — string opaca, usada para revogação.</summary>
    public string Jti { get; }

    /// <summary>Concessões efetivas de todas as fontes, numa única lista somente-leitura.</summary>
    public IReadOnlyList<EffectiveGrant> ConcessoesEfetivas { get; }

    private AuthorizationSubject(
        UsuarioRef usuario,
        IReadOnlySet<string> gruposOidc,
        IReadOnlySet<Guid> unidadesAdministradas,
        IReadOnlyList<EscopoAuditoriaVigente> escoposAuditoria,
        AtuacaoVigente? atuacaoAtiva,
        bool mfaSatisfeito,
        string jti,
        IReadOnlyList<EffectiveGrant> concessoesEfetivas)
    {
        Usuario = usuario;
        GruposOidc = gruposOidc;
        UnidadesAdministradas = unidadesAdministradas;
        EscoposAuditoria = escoposAuditoria;
        AtuacaoAtiva = atuacaoAtiva;
        MfaSatisfeito = mfaSatisfeito;
        Jti = jti;
        ConcessoesEfetivas = concessoesEfetivas;
    }

    /// <summary>
    /// Constrói um <see cref="AuthorizationSubject"/> validado. Exige
    /// <paramref name="usuario"/> (a identidade é o âmago do sujeito explícito) e
    /// rejeita <paramref name="jti"/> em branco (identifica a sessão para
    /// revogação). O <paramref name="jti"/> é preservado <b>verbatim</b> (string
    /// opaca do token). As coleções recebem cópia defensiva imutável (nulas
    /// viram vazias), de modo que mutações na origem não afetam o sujeito.
    /// </summary>
    public static Result<AuthorizationSubject> From(
        UsuarioRef usuario,
        string? jti,
        bool mfaSatisfeito = false,
        IEnumerable<string>? gruposOidc = null,
        IEnumerable<Guid>? unidadesAdministradas = null,
        IEnumerable<EscopoAuditoriaVigente>? escoposAuditoria = null,
        IEnumerable<EffectiveGrant>? concessoesEfetivas = null,
        AtuacaoVigente? atuacaoAtiva = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (string.IsNullOrWhiteSpace(jti))
        {
            return Result<AuthorizationSubject>.Failure(new DomainError(
                AuthorizationErrorCodes.AuthorizationSubjectJtiObrigatorio,
                "Identificador do token (jti) é obrigatório."));
        }

        return Result<AuthorizationSubject>.Success(new AuthorizationSubject(
            usuario,
            ColecoesSomenteLeitura.Conjunto(gruposOidc),
            ColecoesSomenteLeitura.Conjunto(unidadesAdministradas),
            ColecoesSomenteLeitura.Lista(escoposAuditoria),
            atuacaoAtiva,
            mfaSatisfeito,
            jti,
            ColecoesSomenteLeitura.Lista(concessoesEfetivas)));
    }

    // Igualdade por valor (CA-01): o Equals sintetizado pelo record compararia as
    // coleções por referência, tornando sujeitos de conteúdo idêntico desiguais.
    // Conjuntos comparam-se por SetEquals (sem ordem); listas por sequência. O
    // hash acompanha: agregado independente de ordem para os conjuntos, em ordem
    // para as listas.

    /// <inheritdoc />
    public bool Equals(AuthorizationSubject? other) =>
        other is not null
        && Usuario == other.Usuario
        && MfaSatisfeito == other.MfaSatisfeito
        && Jti == other.Jti
        && AtuacaoAtiva == other.AtuacaoAtiva
        && GruposOidc.SetEquals(other.GruposOidc)
        && UnidadesAdministradas.SetEquals(other.UnidadesAdministradas)
        && EscoposAuditoria.SequenceEqual(other.EscoposAuditoria)
        && ConcessoesEfetivas.SequenceEqual(other.ConcessoesEfetivas);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Usuario);
        hash.Add(MfaSatisfeito);
        hash.Add(Jti);
        hash.Add(AtuacaoAtiva);
        hash.Add(HashIndependenteDeOrdem(GruposOidc));
        hash.Add(HashIndependenteDeOrdem(UnidadesAdministradas));
        foreach (EscopoAuditoriaVigente escopo in EscoposAuditoria)
        {
            hash.Add(escopo);
        }

        foreach (EffectiveGrant concessao in ConcessoesEfetivas)
        {
            hash.Add(concessao);
        }

        return hash.ToHashCode();
    }

    // Soma dos hashes dos elementos — independente de ordem, consistente com
    // SetEquals (que ignora ordem). Sem cancelamento (ao contrário do XOR).
    private static int HashIndependenteDeOrdem<T>(IReadOnlySet<T> conjunto)
    {
        int acumulado = 0;
        foreach (T item in conjunto)
        {
            acumulado += item?.GetHashCode() ?? 0;
        }

        return acumulado;
    }
}
