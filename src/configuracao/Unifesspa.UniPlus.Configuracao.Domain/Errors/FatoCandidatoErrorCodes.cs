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
    public const string FonteValoresObrigatoria = "FatoCandidato.FonteValoresObrigatoria";
    public const string FonteValoresForaDeCategorico = "FatoCandidato.FonteValoresForaDeCategorico";
    public const string EscopoObrigatorio = "FatoCandidato.EscopoObrigatorio";
    public const string FormatoObrigatorio = "FatoCandidato.FormatoObrigatorio";
    public const string FormatoForaDeTexto = "FatoCandidato.FormatoForaDeTexto";

    /// <summary>Resposta vazia a um fato de texto livre.</summary>
    public const string TextoLivreVazio = "FatoCandidato.TextoLivreVazio";

    /// <summary>Texto, data e endereço exigem classificação pessoal ou sensível.</summary>
    public const string ClassificacaoAbaixoDoMinimoDoDominio = "FatoCandidato.ClassificacaoAbaixoDoMinimoDoDominio";
    public const string ClassificacaoProtecaoObrigatoria = "FatoCandidato.ClassificacaoProtecaoObrigatoria";
    public const string FinalidadeTratamentoObrigatoria = "FatoCandidato.FinalidadeTratamentoObrigatoria";
    public const string FinalidadeTratamentoTamanho = "FatoCandidato.FinalidadeTratamentoTamanho";
    public const string HipoteseLegalObrigatoria = "FatoCandidato.HipoteseLegalObrigatoria";

    /// <summary>Hipótese do art. 7º em fato sensível, ou do art. 11 em fato não sensível.</summary>
    public const string HipoteseLegalIncompativelComClassificacao = "FatoCandidato.HipoteseLegalIncompativelComClassificacao";

    /// <summary>Fato de sistema só tem nome e descrição editáveis.</summary>
    public const string FatoDeSistemaSoEditaNomeEDescricao = "FatoCandidato.FatoDeSistemaSoEditaNomeEDescricao";

    /// <summary>Fato do administrador com vínculo de atributo do candidato ou de integração.</summary>
    public const string VinculoExclusivoDeFatoDeSistema = "FatoCandidato.VinculoExclusivoDeFatoDeSistema";

    public const string JaAtivo = "FatoCandidato.JaAtivo";
    public const string JaDesativado = "FatoCandidato.JaDesativado";

    /// <summary>Fase em que o valor do fato fica conhecido (ADR-0116) ausente.</summary>
    public const string PontoResolucaoObrigatorio = "FatoCandidato.PontoResolucaoObrigatorio";

    /// <summary>Ponto de resolução fora do conjunto canônico de fases (<c>FaseCanonicaCatalogo</c>).</summary>
    public const string PontoResolucaoInvalido = "FatoCandidato.PontoResolucaoInvalido";

    /// <summary>Referência de onde/como o valor do fato é produzido (ADR-0116) ausente.</summary>
    public const string BindingObrigatorio = "FatoCandidato.BindingObrigatorio";

    /// <summary>Binding fora do formato fechado <c>"{PREFIXO}:{REFERENCIA}"</c>.</summary>
    public const string BindingFormatoInvalido = "FatoCandidato.BindingFormatoInvalido";

    /// <summary>Prefixo do binding incoerente com a <see cref="Enums.OrigemFato"/> declarada.</summary>
    public const string BindingPrefixoIncoerenteComOrigem = "FatoCandidato.BindingPrefixoIncoerenteComOrigem";

    /// <summary>Binding <c>REGRA_DERIVACAO:</c> cuja referência não é o código do próprio fato.</summary>
    public const string BindingReferenciaRegraIncoerente = "FatoCandidato.BindingReferenciaRegraIncoerente";

    public const string NaoEncontrado = "FatoCandidato.NaoEncontrado";
}
