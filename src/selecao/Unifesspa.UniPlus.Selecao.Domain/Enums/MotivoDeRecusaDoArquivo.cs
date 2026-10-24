namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Por que a verificação recusou o arquivo. Nenhum motivo nomeia a ameaça: o detalhe fica no log do verificador.
/// </summary>
public enum MotivoDeRecusaDoArquivo
{
    TamanhoExcedido = 1,
    FormatoNaoCorresponde = 2,
    PdfComSenha = 3,
    NaoPassouNaVerificacaoDeSeguranca = 4,
}
