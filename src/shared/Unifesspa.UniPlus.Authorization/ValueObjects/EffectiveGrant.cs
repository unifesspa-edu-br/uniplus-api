namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessão efetiva avaliada pela decisão de autorização (ADR-0078): uma
/// permissão que o sujeito de fato possui, com sua origem rastreável
/// (<see cref="Fonte"/>), o escopo em que vale (<see cref="Escopo"/>) e a validade.
/// Cada origem tem a sua fábrica, que aplica a regra própria dela: o vínculo de
/// grupo e a concessão excepcional são sempre temporários e exigem
/// <see cref="ValidoAte"/>; a concessão de perfil (<see cref="FonteGrant.Concessao"/>)
/// tem validade opcional; a titularidade (<see cref="FonteGrant.Titularidade"/>)
/// só tem a permissão.
/// </summary>
public sealed record EffectiveGrant
{
    /// <summary>Código da permissão concedida.</summary>
    public string PermissaoCodigo { get; }

    /// <summary>Origem rastreável da concessão.</summary>
    public FonteGrant Fonte { get; }

    /// <summary>Identificador da concessão de origem, quando aplicável. Opcional.</summary>
    public Guid? GrantId { get; }

    /// <summary>Perfil de acesso que reuniu a permissão, quando a concessão é de perfil. Opcional.</summary>
    public Guid? PerfilId { get; }

    /// <summary>Versão do perfil a que a concessão está presa; nula quando a concessão não é de perfil comum.</summary>
    public Guid? VersaoId { get; private init; }

    /// <summary>Validade da concessão. Obrigatória para vínculo de grupo e concessão excepcional; opcional para a concessão de perfil.</summary>
    public DateTimeOffset? ValidoAte { get; }

    /// <summary>Quem concedeu, para concessões excepcionais. Opcional.</summary>
    public UsuarioRef? ConcedidoPor { get; }

    /// <summary>Onde a permissão vale.</summary>
    public EscopoDaPermissaoEfetiva Escopo { get; }

    private EffectiveGrant(
        string permissaoCodigo,
        FonteGrant fonte,
        Guid? grantId,
        Guid? perfilId,
        DateTimeOffset? validoAte,
        UsuarioRef? concedidoPor,
        EscopoDaPermissaoEfetiva escopo)
    {
        PermissaoCodigo = permissaoCodigo;
        Fonte = fonte;
        GrantId = grantId;
        PerfilId = perfilId;
        ValidoAte = validoAte;
        ConcedidoPor = concedidoPor;
        Escopo = escopo;
    }

    /// <summary>
    /// A concessão de perfil de uma permissão que a concessão gravada dá, com a versão do perfil a que está presa.
    /// Valem as recusas de <see cref="DaConcessaoDePerfil"/>, precedidas pelas do escopo.
    /// </summary>
    public static Result<EffectiveGrant> DaConcessao(string? permissao, ConcessaoAtiva concessao)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        Result<EscopoDaPermissaoEfetiva> escopo = EscopoDaPermissaoEfetiva.Criar(concessao.UnidadeId, null, null, null, concessao.Institucional);
        if (escopo.IsFailure)
        {
            return Result<EffectiveGrant>.Failure(escopo.Error!);
        }

        Result<EffectiveGrant> grant = DaConcessaoDePerfil(
            permissao, escopo.Value!, concessao.ConcessaoId, concessao.PerfilId, concessao.ValidaAte);

        return grant.IsSuccess
            ? Result<EffectiveGrant>.Success(grant.Value! with { VersaoId = concessao.VersaoId })
            : grant;
    }

    /// <summary>
    /// A concessão de perfil com o escopo dado. O escopo tem a unidade ou a instituição — nunca nenhuma das duas, que
    /// seria uma permissão global, nem as duas ao mesmo tempo — e admite processo, chamada e tipo de resource adicionais.
    /// </summary>
    public static Result<EffectiveGrant> DaConcessaoDePerfil(
        string? permissao,
        EscopoDaPermissaoEfetiva escopo,
        Guid? grantId = null,
        Guid? perfilId = null,
        DateTimeOffset? validoAte = null)
    {
        ArgumentNullException.ThrowIfNull(escopo);
        DomainError? recusa = ConferirBase(permissao, grantId) ?? EscopoDePerfil(escopo);

        return recusa is null
            ? Result<EffectiveGrant>.Success(new EffectiveGrant(
                permissao!.Trim(), FonteGrant.Concessao, grantId, perfilId, validoAte, null, escopo))
            : Result<EffectiveGrant>.Failure(recusa);
    }

    /// <summary>A concessão resolvida por vínculo de grupo OIDC, sempre temporária: exige <paramref name="validoAte"/>.</summary>
    public static Result<EffectiveGrant> DoVinculoDeGrupo(
        string? permissao, EscopoDaPermissaoEfetiva escopo, DateTimeOffset? validoAte, Guid? grantId = null)
    {
        ArgumentNullException.ThrowIfNull(escopo);
        DomainError? recusa = ConferirBase(permissao, grantId) ?? ValidadeObrigatoria(validoAte);

        return recusa is null
            ? Result<EffectiveGrant>.Success(new EffectiveGrant(
                permissao!.Trim(), FonteGrant.OidcGroupBinding, grantId, null, validoAte, null, escopo))
            : Result<EffectiveGrant>.Failure(recusa);
    }

    /// <summary>
    /// A concessão excepcional (ADR-0084): sempre temporária e com ao menos uma restrição — unidade, processo, chamada
    /// ou tipo de resource.
    /// </summary>
    public static Result<EffectiveGrant> Excepcional(
        string? permissao,
        EscopoDaPermissaoEfetiva escopo,
        DateTimeOffset? validoAte,
        UsuarioRef? concedidoPor = null,
        Guid? grantId = null)
    {
        ArgumentNullException.ThrowIfNull(escopo);
        DomainError? recusa = ConferirBase(permissao, grantId) ?? ValidadeObrigatoria(validoAte) ?? EscopoExcepcional(escopo);

        return recusa is null
            ? Result<EffectiveGrant>.Success(new EffectiveGrant(
                permissao!.Trim(), FonteGrant.PermissaoExcecional, grantId, null, validoAte, concedidoPor, escopo))
            : Result<EffectiveGrant>.Failure(recusa);
    }

    /// <summary>
    /// A titularidade (ADR-0166): o acesso da parte ao que é seu, sem concessão. Só tem a permissão, sem escopo, validade
    /// nem concedente.
    /// </summary>
    public static Result<EffectiveGrant> PorTitularidade(string? permissao)
    {
        DomainError? recusa = ConferirBase(permissao, null);

        return recusa is null
            ? Result<EffectiveGrant>.Success(new EffectiveGrant(
                permissao!.Trim(), FonteGrant.Titularidade, null, null, null, null, EscopoDaPermissaoEfetiva.Nenhum))
            : Result<EffectiveGrant>.Failure(recusa);
    }

    private static DomainError? ConferirBase(string? permissao, Guid? grantId)
    {
        if (string.IsNullOrWhiteSpace(permissao))
        {
            return new DomainError(
                AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria,
                "Código da permissão é obrigatório.");
        }

        return grantId == Guid.Empty
            ? new DomainError(
                AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido,
                "GrantId informado não pode ser Guid.Empty — use um identificador real ou nulo.")
            : null;
    }

    private static DomainError? ValidadeObrigatoria(DateTimeOffset? validoAte) =>
        validoAte is null
            ? new DomainError(
                AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria,
                "Concessão resolvida no servidor exige validade explícita (ValidoAte).")
            : null;

    private static DomainError? EscopoExcepcional(EscopoDaPermissaoEfetiva escopo) =>
        escopo.TemRestricao
            ? null
            : new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoExcepcionalObrigatorio,
                "Concessão excepcional exige ao menos um escopo (unidade, processo, chamada ou tipo de resource).");

    private static DomainError? EscopoDePerfil(EscopoDaPermissaoEfetiva escopo)
    {
        if (escopo.UnidadeId is null && !escopo.Institucional)
        {
            return new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoObrigatorio,
                "Concessão exige a unidade ou a instituição como escopo.");
        }

        return escopo.UnidadeId is not null && escopo.Institucional
            ? new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoAmbiguo,
                "O escopo é a unidade ou a instituição, não os dois.")
            : null;
    }
}
