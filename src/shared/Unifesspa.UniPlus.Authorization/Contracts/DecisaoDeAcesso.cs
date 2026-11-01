namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// A decisão de acesso avaliada sem registro: ou foi permitida, ou foi negada, ou não houve decisão porque o token não
/// identifica quem pede. Quem a recebe compõe com ela e grava o desfecho por
/// <see cref="Abstractions.IVerificadorDeAcesso.RegistrarDesfechoAsync"/>, que aplica a política de registro.
/// </summary>
public abstract record DecisaoDeAcesso
{
    private protected DecisaoDeAcesso()
    {
    }

    /// <summary>O desfecho da decisão.</summary>
    public abstract ResultadoDoAcesso Resultado { get; }

    /// <summary>
    /// Monta a decisão tomada a partir do registro que ela gerou: permitida quando o registro é permitido, e negada
    /// (negada ou impedida) nos demais casos.
    /// </summary>
    public static DecisaoDeAcesso Tomada(PermissionRequirement requisito, ResourceContext resource, RegistroDecisaoAcesso registro)
    {
        ArgumentNullException.ThrowIfNull(requisito);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(registro);

        return registro.Permitido
            ? new DecisaoPermitida(requisito, resource, registro)
            : new DecisaoNegada(requisito, resource, registro);
    }
}

/// <summary>A decisão permitida, com o registro que ela gera.</summary>
/// <param name="Requisito">A permissão decidida.</param>
/// <param name="Resource">O resource sobre o qual se decidiu.</param>
/// <param name="Registro">O registro da decisão.</param>
public sealed record DecisaoPermitida(PermissionRequirement Requisito, ResourceContext Resource, RegistroDecisaoAcesso Registro)
    : DecisaoDeAcesso
{
    /// <inheritdoc />
    public override ResultadoDoAcesso Resultado => ResultadoDoAcesso.Permitido;
}

/// <summary>A decisão negada, com o registro que ela gera.</summary>
/// <param name="Requisito">A permissão decidida.</param>
/// <param name="Resource">O resource sobre o qual se decidiu.</param>
/// <param name="Registro">O registro da decisão.</param>
public sealed record DecisaoNegada(PermissionRequirement Requisito, ResourceContext Resource, RegistroDecisaoAcesso Registro)
    : DecisaoDeAcesso
{
    /// <summary><see cref="ResultadoDoAcesso.Negado"/> ou <see cref="ResultadoDoAcesso.Impedido"/>.</summary>
    public override ResultadoDoAcesso Resultado => Registro.Resultado;
}

/// <summary>
/// O token não identifica quem pede: não houve decisão a tomar, nem registro a gerar. A borda responde <c>401</c>.
/// </summary>
public sealed record SemSujeito : DecisaoDeAcesso
{
    private SemSujeito()
    {
    }

    /// <summary>A única instância: o caso não carrega dado.</summary>
    public static SemSujeito Instancia { get; } = new();

    /// <inheritdoc />
    public override ResultadoDoAcesso Resultado => ResultadoDoAcesso.IdentidadeIncompleta;
}
