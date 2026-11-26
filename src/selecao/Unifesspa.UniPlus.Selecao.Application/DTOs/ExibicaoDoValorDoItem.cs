namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a tela mostra o valor de um item de resposta.</summary>
public enum ExibicaoDoValorDoItem
{
    [CodigoDeContrato("PADRAO", "Padrão", Descricao = "O valor aparece conforme sua forma.")]
    Padrao,
    [CodigoDeContrato("CPF", "CPF", Descricao = "O CPF aparece mascarado.")]
    Cpf,
    [CodigoDeContrato("IDENTIFICADOR", "Identificador", Descricao = "O identificador aparece mascarado.")]
    Identificador,
    [CodigoDeContrato("MOEDA", "Valor em reais", Descricao = "O valor aparece em reais, como R$ 1.234,56.")]
    Moeda,
    [CodigoDeContrato("SITUACAO", "Situação", Descricao = "A tela mostra o rótulo numa etiqueta, sem mudar a forma do valor.")]
    Situacao,
}
