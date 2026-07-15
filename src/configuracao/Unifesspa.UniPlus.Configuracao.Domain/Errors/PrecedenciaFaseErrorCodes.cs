namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da <see cref="Entities.PrecedenciaFase"/>
/// (UNI-REQ-0064), prefixados por <c>PrecedenciaFase.</c>. Mapeados para status
/// HTTP em <c>ConfiguracaoDomainErrorRegistration</c>:
/// <list type="bullet">
///   <item><description><see cref="NaoEncontrada"/> → 404 Not Found</description></item>
///   <item><description>demais → 422 Unprocessable Entity</description></item>
/// </list>
/// </summary>
public static class PrecedenciaFaseErrorCodes
{
    public const string AntecessoraCodigoObrigatorio = "PrecedenciaFase.AntecessoraCodigoObrigatorio";
    public const string AntecessoraCodigoFormatoInvalido = "PrecedenciaFase.AntecessoraCodigoFormatoInvalido";
    public const string AntecessoraForaDoConjuntoCanonico = "PrecedenciaFase.AntecessoraForaDoConjuntoCanonico";
    public const string SucessoraCodigoObrigatorio = "PrecedenciaFase.SucessoraCodigoObrigatorio";
    public const string SucessoraCodigoFormatoInvalido = "PrecedenciaFase.SucessoraCodigoFormatoInvalido";
    public const string SucessoraForaDoConjuntoCanonico = "PrecedenciaFase.SucessoraForaDoConjuntoCanonico";

    /// <summary>Antecessora e sucessora com o mesmo código (aresta de um vértice para si mesmo).</summary>
    public const string SelfLoop = "PrecedenciaFase.SelfLoop";

    /// <summary>Já existe uma aresta viva com o mesmo par (antecessora, sucessora).</summary>
    public const string ArestaDuplicada = "PrecedenciaFase.ArestaDuplicada";

    /// <summary>A aresta, somada ao grafo vigente, fecharia um ciclo.</summary>
    public const string CicloDetectado = "PrecedenciaFase.CicloDetectado";

    public const string NaoEncontrada = "PrecedenciaFase.NaoEncontrada";
}
