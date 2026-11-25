namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Formato de um dado do candidato de domínio texto.
/// </summary>
[DescricaoDoContrato("O formato que o texto de um dado do candidato precisa ter.")]
public enum FormatoTexto
{
    /// <summary>Texto sem formato, que pode conter qualquer coisa.</summary>
    [CodigoDeContrato("LIVRE", "Texto livre", Descricao = "Texto sem formato, que pode conter qualquer coisa.")]
    Livre = 1,

    /// <summary>CPF com dígitos verificadores.</summary>
    [CodigoDeContrato("CPF", "CPF", Descricao = "Número de CPF com os dígitos verificadores corretos.")]
    Cpf = 2,

    /// <summary>Endereço de e-mail.</summary>
    [CodigoDeContrato("EMAIL", "E-mail", Descricao = "Endereço de e-mail.")]
    Email = 3,

    /// <summary>Telefone brasileiro com DDD.</summary>
    [CodigoDeContrato("TELEFONE", "Telefone", Descricao = "Telefone brasileiro com DDD.")]
    Telefone = 4,

    /// <summary>CEP de oito dígitos.</summary>
    [CodigoDeContrato("CEP", "CEP", Descricao = "CEP de oito dígitos.")]
    Cep = 5,

    /// <summary>Nome de pessoa, com nome e sobrenome.</summary>
    [CodigoDeContrato("NOME_PESSOA", "Nome de pessoa", Descricao = "Nome de pessoa, com nome e sobrenome.")]
    NomePessoa = 6,
}
