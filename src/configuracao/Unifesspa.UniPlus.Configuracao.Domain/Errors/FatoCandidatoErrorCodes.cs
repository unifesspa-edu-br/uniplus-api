namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do <see cref="Entities.FatoCandidato"/> — o catálogo
/// <c>fato_candidato</c> (UNI-REQ-0077), prefixados por <c>FatoCandidato.</c>.
/// Mapeados para status HTTP em <c>ConfiguracaoDomainErrorRegistration</c>:
/// <list type="bullet">
///   <item><description><see cref="NaoEncontrado"/> → 404 Not Found</description></item>
///   <item><description>demais → 422 Unprocessable Entity</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Não há <c>CodigoJaExiste</c> (409): o catálogo é seed-governado e append-only —
/// não há caminho de escrita em runtime que possa colidir. A unicidade é garantida
/// pelo índice único total e por teste sobre a fonte do seed. Os erros de validação
/// são alcançáveis apenas pela semeadura em desenvolvimento (defesa em profundidade
/// da factory), nunca por requisição de usuário.
/// </remarks>
public static class FatoCandidatoErrorCodes
{
    public const string CodigoObrigatorio = "FatoCandidato.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "FatoCandidato.CodigoFormatoInvalido";
    public const string NomeObrigatorio = "FatoCandidato.NomeObrigatorio";
    public const string NomeTamanho = "FatoCandidato.NomeTamanho";
    public const string DescricaoTamanho = "FatoCandidato.DescricaoTamanho";
    public const string DominioObrigatorio = "FatoCandidato.DominioObrigatorio";
    public const string DominioInvalido = "FatoCandidato.DominioInvalido";
    public const string NaturezaObrigatoria = "FatoCandidato.NaturezaObrigatoria";
    public const string NaturezaInvalida = "FatoCandidato.NaturezaInvalida";
    public const string CardinalidadeObrigatoria = "FatoCandidato.CardinalidadeObrigatoria";
    public const string CardinalidadeInvalida = "FatoCandidato.CardinalidadeInvalida";
    public const string ValoresDominioNaoPermitidosForaDeCategorico =
        "FatoCandidato.ValoresDominioNaoPermitidosForaDeCategorico";
    public const string ValoresDominioComItemEmBranco = "FatoCandidato.ValoresDominioComItemEmBranco";
    public const string ValoresDominioComDuplicata = "FatoCandidato.ValoresDominioComDuplicata";
    public const string NaoEncontrado = "FatoCandidato.NaoEncontrado";
}
