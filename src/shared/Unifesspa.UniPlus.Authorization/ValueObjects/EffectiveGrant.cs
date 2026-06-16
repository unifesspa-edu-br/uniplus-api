namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessão efetiva avaliada pela decisão de autorização (ADR-0078): uma
/// permissão que o sujeito de fato possui, com sua origem rastreável
/// (<see cref="Fonte"/>), escopo opcional (unidade, processo, chamada),
/// eventual restrição por tipo de recurso e validade. Concessões resolvidas no
/// servidor (<see cref="FonteGrant.OidcGroupBinding"/>,
/// <see cref="FonteGrant.PermissaoExcecional"/>) são sempre temporárias e
/// exigem <see cref="ValidoAte"/>; concessões vindas do token
/// (<see cref="FonteGrant.Token"/>) herdam a validade do token e podem não ter
/// validade própria.
/// </summary>
public sealed record EffectiveGrant
{
    /// <summary>Código da permissão concedida.</summary>
    public string PermissaoCodigo { get; }

    /// <summary>Origem rastreável da concessão.</summary>
    public FonteGrant Fonte { get; }

    /// <summary>Identificador da concessão de origem, quando aplicável. Opcional.</summary>
    public Guid? GrantId { get; }

    /// <summary>Escopo por unidade organizacional. Opcional.</summary>
    public Guid? EscopoUnidadeId { get; }

    /// <summary>Escopo por processo seletivo. Opcional.</summary>
    public Guid? EscopoProcessoId { get; }

    /// <summary>Escopo por chamada. Opcional.</summary>
    public Guid? EscopoChamadaId { get; }

    /// <summary>Restrição da concessão a um tipo de recurso específico. Opcional.</summary>
    public string? RecursoTipoRestricao { get; }

    /// <summary>Validade da concessão. Obrigatória para concessões server-side; opcional para token.</summary>
    public DateTimeOffset? ValidoAte { get; }

    /// <summary>Quem concedeu, para concessões excepcionais. Opcional.</summary>
    public UsuarioRef? ConcedidoPor { get; }

    private EffectiveGrant(
        string permissaoCodigo,
        FonteGrant fonte,
        Guid? grantId,
        Guid? escopoUnidadeId,
        Guid? escopoProcessoId,
        Guid? escopoChamadaId,
        string? recursoTipoRestricao,
        DateTimeOffset? validoAte,
        UsuarioRef? concedidoPor)
    {
        PermissaoCodigo = permissaoCodigo;
        Fonte = fonte;
        GrantId = grantId;
        EscopoUnidadeId = escopoUnidadeId;
        EscopoProcessoId = escopoProcessoId;
        EscopoChamadaId = escopoChamadaId;
        RecursoTipoRestricao = recursoTipoRestricao;
        ValidoAte = validoAte;
        ConcedidoPor = concedidoPor;
    }

    /// <summary>
    /// Constrói uma <see cref="EffectiveGrant"/> validada. Rejeita código de
    /// permissão vazio e concessão server-side
    /// (<see cref="FonteGrant.OidcGroupBinding"/> /
    /// <see cref="FonteGrant.PermissaoExcecional"/>) sem
    /// <paramref name="validoAte"/>. Adicionalmente, a concessão excepcional
    /// (<see cref="FonteGrant.PermissaoExcecional"/>) exige <b>ao menos um
    /// escopo</b> — unidade, processo, chamada ou tipo de recurso (ADR-0084):
    /// uma concessão fora do perfil padrão nunca é global irrestrita.
    /// <see cref="FonteGrant.Token"/> e <see cref="FonteGrant.OidcGroupBinding"/>
    /// podem ser permissões globais, sem escopo.
    /// </summary>
    public static Result<EffectiveGrant> From(
        string? permissaoCodigo,
        FonteGrant fonte,
        Guid? grantId = null,
        Guid? escopoUnidadeId = null,
        Guid? escopoProcessoId = null,
        Guid? escopoChamadaId = null,
        string? recursoTipoRestricao = null,
        DateTimeOffset? validoAte = null,
        UsuarioRef? concedidoPor = null)
    {
        if (string.IsNullOrWhiteSpace(permissaoCodigo))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria,
                "Código da permissão é obrigatório."));
        }

        if (EscopoVazioInformado(escopoUnidadeId)
            || EscopoVazioInformado(escopoProcessoId)
            || EscopoVazioInformado(escopoChamadaId))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        if (fonte is FonteGrant.OidcGroupBinding or FonteGrant.PermissaoExcecional && validoAte is null)
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria,
                "Concessão resolvida no servidor exige validade explícita (ValidoAte)."));
        }

        if (fonte is FonteGrant.PermissaoExcecional
            && escopoUnidadeId is null
            && escopoProcessoId is null
            && escopoChamadaId is null
            && string.IsNullOrWhiteSpace(recursoTipoRestricao))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoExcecionalObrigatorio,
                "Concessão excepcional exige ao menos um escopo (unidade, processo, chamada ou tipo de recurso)."));
        }

        return Result<EffectiveGrant>.Success(new EffectiveGrant(
            permissaoCodigo.Trim(),
            fonte,
            grantId,
            escopoUnidadeId,
            escopoProcessoId,
            escopoChamadaId,
            recursoTipoRestricao,
            validoAte,
            concedidoPor));
    }

    // Um escopo Guid é "informado mas vazio" quando vem preenchido com
    // Guid.Empty — nunca um identificador real (o projeto usa Guid v7).
    private static bool EscopoVazioInformado(Guid? escopo) => escopo is { } valor && valor == Guid.Empty;
}
