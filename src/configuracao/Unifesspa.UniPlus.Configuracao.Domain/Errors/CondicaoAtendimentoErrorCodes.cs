namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

// Mapeamentos esperados para HTTP (a serem registrados em
// ConfiguracaoDomainErrorRegistration — fora desta entrega):
//   CodigoJaExiste                  → 409 Conflict
//   RemocaoBloqueadaCodigoProtegido → 409 Conflict
//   NaoEncontrada                   → 404 Not Found
//   CodigoProtegidoNaoEditavel      → 422 Unprocessable Entity
//   CodigoObrigatorio               → 422 Unprocessable Entity
//   CodigoFormatoInvalido           → 422 Unprocessable Entity
//   NomeObrigatorio                 → 422 Unprocessable Entity
//   NomeTamanho                     → 422 Unprocessable Entity
//   DescricaoTamanho                → 422 Unprocessable Entity
public static class CondicaoAtendimentoErrorCodes
{
    public const string CodigoObrigatorio = "CondicaoAtendimento.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "CondicaoAtendimento.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "CondicaoAtendimento.CodigoJaExiste";
    public const string CodigoProtegidoNaoEditavel = "CondicaoAtendimento.CodigoProtegidoNaoEditavel";
    public const string NomeObrigatorio = "CondicaoAtendimento.NomeObrigatorio";
    public const string NomeTamanho = "CondicaoAtendimento.NomeTamanho";
    public const string DescricaoTamanho = "CondicaoAtendimento.DescricaoTamanho";
    public const string NaoEncontrada = "CondicaoAtendimento.NaoEncontrada";
    public const string RemocaoBloqueadaCodigoProtegido = "CondicaoAtendimento.RemocaoBloqueadaCodigoProtegido";
}
