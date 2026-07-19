namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

// Mapeamentos esperados em ConfiguracaoDomainErrorRegistration (UNI-REQ-0012):
//   NomeJaExiste         → 409 Conflict
//   NomeObrigatorio      → 422 UnprocessableEntity
//   NomeTamanho          → 422 UnprocessableEntity
//   DescricaoObrigatoria → 422 UnprocessableEntity
//   DescricaoTamanho     → 422 UnprocessableEntity
//   NaoEncontrado        → 404 NotFound
public static class TipoDeficienciaErrorCodes
{
    public const string NomeObrigatorio = "TipoDeficiencia.NomeObrigatorio";
    public const string NomeTamanho = "TipoDeficiencia.NomeTamanho";
    public const string NomeJaExiste = "TipoDeficiencia.NomeJaExiste";

    /// <summary>Descrição ausente (ADR-0116): serve à descrição por valor do fato <c>TIPO_DEFICIENCIA</c>.</summary>
    public const string DescricaoObrigatoria = "TipoDeficiencia.DescricaoObrigatoria";
    public const string DescricaoTamanho = "TipoDeficiencia.DescricaoTamanho";
    public const string NaoEncontrado = "TipoDeficiencia.NaoEncontrado";
}
