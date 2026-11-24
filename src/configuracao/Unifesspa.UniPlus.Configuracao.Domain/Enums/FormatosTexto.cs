namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A exibição parcial do texto de um fato de domínio texto, conforme o <see cref="FormatoTexto"/>.</summary>
public static class FormatosTexto
{
    private const string Oculto = "***";

    /// <summary>
    /// A exibição parcial do texto no formato, pela máscara do tipo de valor do Kernel. O texto
    /// livre, que pode conter qualquer coisa, e o texto que não valida são inteiramente ocultados.
    /// </summary>
    public static string Mascarar(FormatoTexto formato, string? texto) => formato switch
    {
        FormatoTexto.Cpf => Mascara(Kernel.Domain.ValueObjects.Cpf.Criar(texto), static cpf => cpf.Mascarado),
        FormatoTexto.Email => Mascara(Kernel.Domain.ValueObjects.Email.Criar(texto), static email => email.Mascarado),
        FormatoTexto.Telefone => Mascara(Kernel.Domain.ValueObjects.Telefone.Criar(texto), static telefone => telefone.Mascarado),
        FormatoTexto.Cep => Mascara(Kernel.Domain.ValueObjects.Cep.Criar(texto), static cep => cep.Mascarado),
        FormatoTexto.NomePessoa => Mascara(Kernel.Domain.ValueObjects.NomePessoa.Criar(texto), static nome => nome.Mascarado),
        FormatoTexto.Livre => Oculto,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de texto fora do domínio fechado."),
    };

    private static string Mascara<T>(Result<T> valor, Func<T, string> mascarado) =>
        valor is { IsSuccess: true } ? mascarado(valor.Value!) : Oculto;
}
