namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Por que a verificação recusou o arquivo. Nenhum motivo nomeia a ameaça: o detalhe fica no log do verificador.
/// </summary>
public enum MotivoDeRecusaDoArquivo
{
    [CodigoDeContrato("TAMANHO_EXCEDIDO", "Tamanho excedido", Descricao = "O arquivo excede o tamanho permitido.")]
    TamanhoExcedido = 1,
    [CodigoDeContrato("FORMATO_NAO_CORRESPONDE", "Formato incompatível", Descricao = "O conteúdo não corresponde ao formato informado.")]
    FormatoNaoCorresponde = 2,
    [CodigoDeContrato("PDF_COM_SENHA", "PDF com senha", Descricao = "O arquivo PDF está protegido por senha.")]
    PdfComSenha = 3,
    [CodigoDeContrato("NAO_PASSOU_NA_VERIFICACAO_DE_SEGURANCA", "Não passou na verificação de segurança", Descricao = "O arquivo não passou na verificação de segurança.")]
    NaoPassouNaVerificacaoDeSeguranca = 4,
}
