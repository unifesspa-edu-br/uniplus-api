namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

// Mapeamentos esperados em ConfiguracaoDomainErrorRegistration (UNI-REQ-0012):
//   NomeJaExiste      → 409 Conflict
//   NomeObrigatorio   → 422 UnprocessableEntity
//   NomeTamanho       → 422 UnprocessableEntity
//   DescricaoTamanho  → 422 UnprocessableEntity
//   NaoEncontrado     → 404 NotFound
public static class TipoDeficienciaErrorCodes
{
    public const string NomeObrigatorio = "TipoDeficiencia.NomeObrigatorio";
    public const string NomeTamanho = "TipoDeficiencia.NomeTamanho";
    public const string NomeJaExiste = "TipoDeficiencia.NomeJaExiste";
    public const string DescricaoTamanho = "TipoDeficiencia.DescricaoTamanho";
    public const string NaoEncontrado = "TipoDeficiencia.NaoEncontrado";
}
