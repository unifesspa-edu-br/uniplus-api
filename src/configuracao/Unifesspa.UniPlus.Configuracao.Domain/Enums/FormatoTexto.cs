namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Formato de um <see cref="Entities.FatoCandidato"/> de domínio texto (ADR-0136). A validação e a
/// máscara de cada formato vivem num tipo de valor do Kernel; <see cref="FormatosTexto"/> despacha
/// para ele.
/// </summary>
public enum FormatoTexto
{
    /// <summary>Sentinela — formato não informado; rejeitado no fato de domínio texto.</summary>
    Nenhum = 0,

    /// <summary>Texto sem formato, que pode conter qualquer coisa.</summary>
    Livre,

    /// <summary>CPF com dígitos verificadores.</summary>
    Cpf,

    /// <summary>Endereço de e-mail.</summary>
    Email,

    /// <summary>Telefone brasileiro com DDD.</summary>
    Telefone,

    /// <summary>CEP de oito dígitos.</summary>
    Cep,

    /// <summary>Nome de pessoa, com nome e sobrenome.</summary>
    NomePessoa,
}
