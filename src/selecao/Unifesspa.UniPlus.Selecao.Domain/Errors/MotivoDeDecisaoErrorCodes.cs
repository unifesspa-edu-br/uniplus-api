namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do cadastro único de motivos de decisão (UNI-REQ-0283) e da
/// escolha dos motivos pelo processo (UNI-REQ-0285). Declarados como constantes, e não
/// repetidos como literal no domínio e no registro de erros da API: um código escrito duas
/// vezes diverge na primeira correção de grafia, e o mapeamento órfão só apareceria como
/// <c>500</c> na requisição que o disparasse.
/// </summary>
public static class MotivoDeDecisaoErrorCodes
{
    public const string TipoObrigatorio = "MotivoDeDecisao.TipoObrigatorio";
    public const string CodigoObrigatorio = "MotivoDeDecisao.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "MotivoDeDecisao.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "MotivoDeDecisao.CodigoJaExiste";
    public const string DescricaoObrigatoria = "MotivoDeDecisao.DescricaoObrigatoria";
    public const string DescricaoTamanho = "MotivoDeDecisao.DescricaoTamanho";
    public const string DescricaoCaractereInvalido = "MotivoDeDecisao.DescricaoCaractereInvalido";
    public const string SentidoObrigatorio = "MotivoDeDecisao.SentidoObrigatorio";
    public const string SentidoNaoAdmitido = "MotivoDeDecisao.SentidoNaoAdmitido";
    public const string SubclassificacaoObrigatoria = "MotivoDeDecisao.SubclassificacaoObrigatoria";
    public const string SubclassificacaoInvalida = "MotivoDeDecisao.SubclassificacaoInvalida";
    public const string SubclassificacaoNaoAdmitida = "MotivoDeDecisao.SubclassificacaoNaoAdmitida";
    public const string JaAtivo = "MotivoDeDecisao.JaAtivo";
    public const string JaInativo = "MotivoDeDecisao.JaInativo";
    public const string NaoEncontrado = "MotivoDeDecisao.NaoEncontrado";
    public const string SituacaoAlteradaConcorrentemente = "MotivoDeDecisao.SituacaoAlteradaConcorrentemente";

    public const string MotivoInativo = "MotivoDeDecisao.MotivoInativo";
    public const string JaEscolhido = "MotivoDeDecisao.JaEscolhido";
    public const string NaoEscolhido = "MotivoDeDecisao.NaoEscolhido";
    public const string JustificativaObrigatoria = "MotivoDeDecisao.JustificativaObrigatoria";
    public const string JustificativaTamanho = "MotivoDeDecisao.JustificativaTamanho";
    public const string JustificativaCaractereInvalido = "MotivoDeDecisao.JustificativaCaractereInvalido";
    public const string AvaliacaoIniciada = "MotivoDeDecisao.AvaliacaoIniciada";
    public const string RetiradaAposPublicacao = "MotivoDeDecisao.RetiradaAposPublicacao";
    public const string MotivoDesativado = "MotivoDeDecisao.MotivoDesativado";
    public const string CoberturaInsuficiente = "MotivoDeDecisao.CoberturaInsuficiente";
}
