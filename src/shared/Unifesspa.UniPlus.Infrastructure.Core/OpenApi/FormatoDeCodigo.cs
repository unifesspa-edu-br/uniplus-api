namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>
/// O formato com que a resposta expõe um enum de contrato: <see cref="CodigoComRotulo{TEnum}"/> ou, quando o campo precisa
/// da explicação do valor, <see cref="CodigoComDescricao{TEnum}"/>.
/// </summary>
internal sealed record FormatoDeCodigo(Type EnumDeContrato, bool ComDescricao)
{
    /// <summary>O componente do formato: o nome do enum, ou o nome do enum seguido de <c>ComDescricao</c>.</summary>
    public string NomeDoComponente => ComDescricao ? $"{EnumDeContrato.Name}ComDescricao" : EnumDeContrato.Name;

    /// <summary>O formato que o tipo é, ou nada se o tipo não é um formato de código.</summary>
    public static FormatoDeCodigo? De(Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        if (!tipo.IsGenericType)
        {
            return null;
        }

        Type definicao = tipo.GetGenericTypeDefinition();

        return definicao == typeof(CodigoComRotulo<>) || definicao == typeof(CodigoComDescricao<>)
            ? new FormatoDeCodigo(tipo.GetGenericArguments()[0], definicao == typeof(CodigoComDescricao<>))
            : null;
    }
}
