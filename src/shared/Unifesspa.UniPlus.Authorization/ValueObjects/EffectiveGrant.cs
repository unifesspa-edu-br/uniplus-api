namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessão efetiva avaliada pela decisão de autorização (ADR-0078): uma
/// permissão que o sujeito de fato possui, com sua origem rastreável
/// (<see cref="Fonte"/>), escopo (a unidade ou a instituição, mais processo e
/// chamada opcionais), eventual restrição por tipo de resource e validade.
/// Concessões resolvidas por vínculo de grupo ou excepcionais são sempre
/// temporárias e exigem <see cref="ValidoAte"/>; a concessão de perfil
/// (<see cref="FonteGrant.Concessao"/>) tem validade opcional.
/// </summary>
public sealed record EffectiveGrant
{
    /// <summary>Código da permissão concedida.</summary>
    public string PermissaoCodigo { get; }

    /// <summary>Origem rastreável da concessão.</summary>
    public FonteGrant Fonte { get; }

    /// <summary>Identificador da concessão de origem, quando aplicável. Opcional.</summary>
    public Guid? GrantId { get; }

    /// <summary>Escopo por unidade organizacional; nulo quando o escopo é a instituição.</summary>
    public Guid? EscopoUnidadeId { get; }

    /// <summary>O escopo é a instituição: a concessão alcança só os resources sem unidade proprietária.</summary>
    public bool EscopoInstitucional { get; }

    /// <summary>Perfil de acesso que reuniu a permissão, quando a concessão é de perfil. Opcional.</summary>
    public Guid? PerfilId { get; }

    /// <summary>Versão do perfil a que a concessão está presa; nula quando a concessão não é de perfil comum.</summary>
    public Guid? VersaoId { get; private init; }

    /// <summary>Escopo por processo seletivo. Opcional.</summary>
    public Guid? EscopoProcessoId { get; }

    /// <summary>Escopo por chamada. Opcional.</summary>
    public Guid? EscopoChamadaId { get; }

    /// <summary>Restrição da concessão a um tipo de resource específico. Opcional.</summary>
    public string? ResourceTipoRestricao { get; }

    /// <summary>Validade da concessão. Obrigatória para vínculo de grupo e concessão excepcional; opcional para a concessão de perfil.</summary>
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
        string? resourceTipoRestricao,
        DateTimeOffset? validoAte,
        UsuarioRef? concedidoPor,
        bool escopoInstitucional,
        Guid? perfilId)
    {
        PermissaoCodigo = permissaoCodigo;
        Fonte = fonte;
        GrantId = grantId;
        EscopoUnidadeId = escopoUnidadeId;
        EscopoProcessoId = escopoProcessoId;
        EscopoChamadaId = escopoChamadaId;
        ResourceTipoRestricao = resourceTipoRestricao;
        ValidoAte = validoAte;
        ConcedidoPor = concedidoPor;
        EscopoInstitucional = escopoInstitucional;
        PerfilId = perfilId;
    }

    /// <summary>
    /// Constrói uma <see cref="EffectiveGrant"/> validada. Rejeita código de
    /// permissão vazio e concessão resolvida por vínculo de grupo ou excepcional
    /// sem <paramref name="validoAte"/>. A concessão excepcional exige ao menos um
    /// escopo — unidade, processo, chamada ou tipo de resource (ADR-0084). A
    /// concessão de perfil (<see cref="FonteGrant.Concessao"/>) tem a unidade ou a
    /// instituição como escopo — nunca nenhuma das duas, que seria uma permissão
    /// global, nem as duas ao mesmo tempo.
    /// </summary>
    public static Result<EffectiveGrant> From(
        string? permissaoCodigo,
        FonteGrant fonte,
        Guid? grantId = null,
        Guid? escopoUnidadeId = null,
        Guid? escopoProcessoId = null,
        Guid? escopoChamadaId = null,
        string? resourceTipoRestricao = null,
        DateTimeOffset? validoAte = null,
        UsuarioRef? concedidoPor = null,
        bool escopoInstitucional = false,
        Guid? perfilId = null)
    {
        if (string.IsNullOrWhiteSpace(permissaoCodigo))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria,
                "Código da permissão é obrigatório."));
        }

        if (GuidVazioInformado(grantId))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido,
                "GrantId informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        if (GuidVazioInformado(escopoUnidadeId)
            || GuidVazioInformado(escopoProcessoId)
            || GuidVazioInformado(escopoChamadaId))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        if (!string.IsNullOrWhiteSpace(resourceTipoRestricao) && !ResourceTipos.SegueAConvencao(resourceTipoRestricao))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantResourceTipoRestricaoInvalido,
                "A restrição por tipo de resource deve estar em kebab-case, em minúsculas, sem acento (ex.: tipo-documento)."));
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
            && string.IsNullOrWhiteSpace(resourceTipoRestricao))
        {
            return Result<EffectiveGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.EffectiveGrantEscopoExcepcionalObrigatorio,
                "Concessão excepcional exige ao menos um escopo (unidade, processo, chamada ou tipo de resource)."));
        }

        if (fonte is FonteGrant.Concessao)
        {
            if (escopoUnidadeId is null && !escopoInstitucional)
            {
                return Result<EffectiveGrant>.Failure(new DomainError(
                    AuthorizationErrorCodes.EffectiveGrantEscopoObrigatorio,
                    "Concessão exige a unidade ou a instituição como escopo."));
            }

            if (escopoUnidadeId is not null && escopoInstitucional)
            {
                return Result<EffectiveGrant>.Failure(new DomainError(
                    AuthorizationErrorCodes.EffectiveGrantEscopoAmbiguo,
                    "O escopo é a unidade ou a instituição, não os dois."));
            }
        }

        return Result<EffectiveGrant>.Success(new EffectiveGrant(
            permissaoCodigo.Trim(),
            fonte,
            grantId,
            escopoUnidadeId,
            escopoProcessoId,
            escopoChamadaId,
            resourceTipoRestricao,
            validoAte,
            concedidoPor,
            escopoInstitucional,
            perfilId));
    }

    /// <summary>
    /// A concessão de perfil de uma permissão que a concessão gravada dá, com a versão do perfil a que está presa. Passa
    /// pela mesma validação de <see cref="From"/>, de modo que a permissão inválida falha sem efeito sobre as demais.
    /// </summary>
    public static Result<EffectiveGrant> DaConcessao(string? permissao, ConcessaoAtiva concessao)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        Result<EffectiveGrant> grant = From(
            permissao,
            FonteGrant.Concessao,
            grantId: concessao.ConcessaoId,
            escopoUnidadeId: concessao.UnidadeId,
            validoAte: concessao.ValidaAte,
            escopoInstitucional: concessao.Institucional,
            perfilId: concessao.PerfilId);

        return grant.IsSuccess
            ? Result<EffectiveGrant>.Success(grant.Value! with { VersaoId = concessao.VersaoId })
            : grant;
    }

    // Um Guid opcional é "informado mas vazio" quando vem preenchido com
    // Guid.Empty — nunca um identificador real (o projeto usa Guid v7).
    private static bool GuidVazioInformado(Guid? valor) => valor is { } g && g == Guid.Empty;
}
