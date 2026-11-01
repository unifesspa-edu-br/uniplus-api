namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// A decisão de acesso tomada sem registro: o desfecho que a borda responde e, quando houve decisão, o registro que ela
/// geraria. Quem a recebe registra só a permitida, uma vez ou para vários resources, por
/// <see cref="Abstractions.IVerificadorDeAcesso"/>; a negativa não gera registro algum.
/// </summary>
public sealed record DecisaoDeAcesso
{
    /// <summary>O desfecho da decisão.</summary>
    public ResultadoDoAcesso Resultado { get; }

    /// <summary>O registro que a decisão geraria; nulo quando a identidade foi incompleta e não houve decisão.</summary>
    public RegistroDecisaoAcesso? Registro { get; }

    private DecisaoDeAcesso(ResultadoDoAcesso resultado, RegistroDecisaoAcesso? registro)
    {
        Resultado = resultado;
        Registro = registro;
    }

    /// <summary>Sem sujeito não houve decisão a tomar, nem registro a gerar.</summary>
    public static DecisaoDeAcesso IdentidadeIncompleta { get; } = new(ResultadoDoAcesso.IdentidadeIncompleta, null);

    /// <summary>A decisão tomada pelo ponto de decisão, com o registro que ela geraria.</summary>
    public static DecisaoDeAcesso Tomada(ResultadoDoAcesso resultado, RegistroDecisaoAcesso registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        return new DecisaoDeAcesso(resultado, registro);
    }
}
