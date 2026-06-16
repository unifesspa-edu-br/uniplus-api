namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessão de dupla aprovação (four-eyes) para uma operação sensível,
/// consumida pela decisão de autorização (ADR-0078). Dois aprovadores
/// <b>distintos</b> autorizam uma operação sobre um recurso específico
/// (<see cref="RecursoHash"/>), com uma janela de validade curta — no máximo
/// <b>1 hora</b> após a concessão. O identificador é um GUID v7
/// (<see cref="Guid.CreateVersion7()"/>, ADR-0032), ordenável temporalmente.
/// </summary>
/// <remarks>
/// A persistência, o algoritmo do <see cref="RecursoHash"/> e o consumo atômico
/// de uso único são responsabilidade de stories irmãs (#609, #612). Este tipo é
/// apenas o contrato em memória, com as invariantes de construção.
/// </remarks>
public sealed record DualApprovalGrant
{
    /// <summary>Janela máxima de validade após a concessão.</summary>
    public static readonly TimeSpan JanelaMaxima = TimeSpan.FromHours(1);

    /// <summary>Identificador GUID v7 da concessão.</summary>
    public Guid Id { get; }

    /// <summary>Primeiro aprovador.</summary>
    public UsuarioRef AprovadorPrimario { get; }

    /// <summary>Segundo aprovador — distinto do primário.</summary>
    public UsuarioRef AprovadorSecundario { get; }

    /// <summary>Hash opaco do recurso alvo (algoritmo definido em story irmã).</summary>
    public string RecursoHash { get; }

    /// <summary>Código da permissão concedida.</summary>
    public string PermissaoCodigo { get; }

    /// <summary>Instante da concessão.</summary>
    public DateTimeOffset ConcedidoEm { get; }

    /// <summary>Validade da concessão — no máximo 1h após <see cref="ConcedidoEm"/>.</summary>
    public DateTimeOffset ValidoAte { get; }

    /// <summary>Marcador de uso único (o consumo atômico fica em story irmã).</summary>
    public bool Usado { get; }

    /// <summary>Instante do uso, quando consumida. Opcional.</summary>
    public DateTimeOffset? UsadoEm { get; }

    /// <summary>Quem consumiu a concessão. Opcional.</summary>
    public UsuarioRef? UsadoPor { get; }

    private DualApprovalGrant(
        Guid id,
        UsuarioRef aprovadorPrimario,
        UsuarioRef aprovadorSecundario,
        string recursoHash,
        string permissaoCodigo,
        DateTimeOffset concedidoEm,
        DateTimeOffset validoAte,
        bool usado,
        DateTimeOffset? usadoEm,
        UsuarioRef? usadoPor)
    {
        Id = id;
        AprovadorPrimario = aprovadorPrimario;
        AprovadorSecundario = aprovadorSecundario;
        RecursoHash = recursoHash;
        PermissaoCodigo = permissaoCodigo;
        ConcedidoEm = concedidoEm;
        ValidoAte = validoAte;
        Usado = usado;
        UsadoEm = usadoEm;
        UsadoPor = usadoPor;
    }

    /// <summary>
    /// Constrói um <see cref="DualApprovalGrant"/> validado, com identificador
    /// GUID v7. Rejeita aprovador secundário igual ao primário (mesma
    /// identidade OIDC), validade não posterior à concessão e validade acima do
    /// limite de 1 hora.
    /// </summary>
    public static Result<DualApprovalGrant> From(
        UsuarioRef aprovadorPrimario,
        UsuarioRef aprovadorSecundario,
        string? recursoHash,
        string? permissaoCodigo,
        DateTimeOffset concedidoEm,
        DateTimeOffset validoAte,
        bool usado = false,
        DateTimeOffset? usadoEm = null,
        UsuarioRef? usadoPor = null)
    {
        ArgumentNullException.ThrowIfNull(aprovadorPrimario);
        ArgumentNullException.ThrowIfNull(aprovadorSecundario);

        if (string.IsNullOrWhiteSpace(permissaoCodigo))
        {
            return Result<DualApprovalGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.DualApprovalPermissaoObrigatoria,
                "Código da permissão é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(recursoHash))
        {
            return Result<DualApprovalGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.DualApprovalRecursoHashObrigatorio,
                "Hash do recurso é obrigatório."));
        }

        if (MesmaIdentidade(aprovadorPrimario, aprovadorSecundario))
        {
            return Result<DualApprovalGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.DualApprovalAprovadoresIguais,
                "Aprovador secundário deve ser distinto do primário."));
        }

        if (validoAte <= concedidoEm)
        {
            return Result<DualApprovalGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.DualApprovalValidadeNaoPosterior,
                "Validade deve ser posterior à concessão."));
        }

        if (validoAte - concedidoEm > JanelaMaxima)
        {
            return Result<DualApprovalGrant>.Failure(new DomainError(
                AuthorizationErrorCodes.DualApprovalValidadeAcimaDoLimite,
                "Validade não pode exceder 1 hora após a concessão."));
        }

        return Result<DualApprovalGrant>.Success(new DualApprovalGrant(
            Guid.CreateVersion7(),
            aprovadorPrimario,
            aprovadorSecundario,
            recursoHash.Trim(),
            permissaoCodigo.Trim(),
            concedidoEm,
            validoAte,
            usado,
            usadoEm,
            usadoPor));
    }

    // Dois aprovadores são a mesma pessoa quando compartilham emissor + subject
    // do token (par único de identidade OIDC) — não o UsuarioId interno, que
    // pode estar ausente em um dos lados.
    private static bool MesmaIdentidade(UsuarioRef a, UsuarioRef b) =>
        string.Equals(a.Emissor, b.Emissor, StringComparison.Ordinal)
        && string.Equals(a.Subject, b.Subject, StringComparison.Ordinal);
}
