namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do catálogo de motivos de decisão de isenção
/// (UNI-REQ-0120 a UNI-REQ-0122). Declarados como constantes, e não repetidos
/// como literal no domínio e no registro de erros da API: um código escrito
/// duas vezes diverge na primeira correção de grafia, e o mapeamento órfão só
/// apareceria como <c>500</c> na requisição que o disparasse.
/// </summary>
public static class MotivoDecisaoIsencaoErrorCodes
{
    public const string CodigoObrigatorio = "MotivoDecisaoIsencao.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "MotivoDecisaoIsencao.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "MotivoDecisaoIsencao.CodigoJaExiste";
    public const string DescricaoObrigatoria = "MotivoDecisaoIsencao.DescricaoObrigatoria";
    public const string DescricaoTamanho = "MotivoDecisaoIsencao.DescricaoTamanho";
    public const string DescricaoCaractereInvalido = "MotivoDecisaoIsencao.DescricaoCaractereInvalido";
    public const string FundamentoObrigatorio = "MotivoDecisaoIsencao.FundamentoObrigatorio";
    public const string ResultadoPermitidoObrigatorio = "MotivoDecisaoIsencao.ResultadoPermitidoObrigatorio";
    public const string JaAtivo = "MotivoDecisaoIsencao.JaAtivo";
    public const string JaInativo = "MotivoDecisaoIsencao.JaInativo";
    public const string NaoEncontrado = "MotivoDecisaoIsencao.NaoEncontrado";
}
