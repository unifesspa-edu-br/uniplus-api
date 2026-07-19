namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do <see cref="Entities.FatoCandidato"/> — o catálogo
/// <c>rol_de_fatos_candidato</c> (UNI-REQ-0077), prefixados por <c>FatoCandidato.</c>.
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
    public const string OrigemObrigatoria = "FatoCandidato.OrigemObrigatoria";
    public const string OrigemInvalida = "FatoCandidato.OrigemInvalida";
    public const string CardinalidadeObrigatoria = "FatoCandidato.CardinalidadeObrigatoria";
    public const string CardinalidadeInvalida = "FatoCandidato.CardinalidadeInvalida";
    public const string ValoresDominioNaoPermitidosForaDeCategorico =
        "FatoCandidato.ValoresDominioNaoPermitidosForaDeCategorico";
    public const string ValoresDominioComItemEmBranco = "FatoCandidato.ValoresDominioComItemEmBranco";
    public const string ValoresDominioComDuplicata = "FatoCandidato.ValoresDominioComDuplicata";

    /// <summary>Fase em que o valor do fato fica conhecido (ADR-0116) ausente.</summary>
    public const string PontoResolucaoObrigatorio = "FatoCandidato.PontoResolucaoObrigatorio";

    /// <summary>Ponto de resolução fora do conjunto canônico das quatorze fases (<c>FaseCanonicaCatalogo</c>).</summary>
    public const string PontoResolucaoInvalido = "FatoCandidato.PontoResolucaoInvalido";

    /// <summary>Referência de onde/como o valor do fato é produzido (ADR-0116) ausente.</summary>
    public const string BindingObrigatorio = "FatoCandidato.BindingObrigatorio";

    /// <summary>Binding fora do formato fechado <c>"{PREFIXO}:{REFERENCIA}"</c>.</summary>
    public const string BindingFormatoInvalido = "FatoCandidato.BindingFormatoInvalido";

    /// <summary>Prefixo do binding incoerente com a <see cref="Enums.OrigemFato"/> declarada.</summary>
    public const string BindingPrefixoIncoerenteComOrigem = "FatoCandidato.BindingPrefixoIncoerenteComOrigem";

    public const string NaoEncontrado = "FatoCandidato.NaoEncontrado";
}
