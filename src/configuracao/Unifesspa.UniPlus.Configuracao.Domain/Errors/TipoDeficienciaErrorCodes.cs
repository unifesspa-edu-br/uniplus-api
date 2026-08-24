namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

// Mapeamentos esperados em ConfiguracaoDomainErrorRegistration (UNI-REQ-0012):
//   CodigoJaExiste        → 409 Conflict
//   NomeJaExiste          → 409 Conflict
//   CodigoObrigatorio     → 422 UnprocessableEntity
//   CodigoFormatoInvalido → 422 UnprocessableEntity
//   NomeObrigatorio       → 422 UnprocessableEntity
//   NomeTamanho           → 422 UnprocessableEntity
//   DescricaoObrigatoria  → 422 UnprocessableEntity
//   DescricaoTamanho      → 422 UnprocessableEntity
//   NaoEncontrado         → 404 NotFound
public static class TipoDeficienciaErrorCodes
{
    /// <summary>Código ausente (UNI-REQ-0061): a identidade congelada nos fatos de atendimento é código + origem.</summary>
    public const string CodigoObrigatorio = "TipoDeficiencia.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "TipoDeficiencia.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "TipoDeficiencia.CodigoJaExiste";

    public const string NomeObrigatorio = "TipoDeficiencia.NomeObrigatorio";
    public const string NomeTamanho = "TipoDeficiencia.NomeTamanho";
    public const string NomeJaExiste = "TipoDeficiencia.NomeJaExiste";

    /// <summary>Descrição ausente (ADR-0116): serve à descrição por valor do fato <c>TIPO_DEFICIENCIA</c>.</summary>
    public const string DescricaoObrigatoria = "TipoDeficiencia.DescricaoObrigatoria";
    public const string DescricaoTamanho = "TipoDeficiencia.DescricaoTamanho";
    public const string NaoEncontrado = "TipoDeficiencia.NaoEncontrado";
}
