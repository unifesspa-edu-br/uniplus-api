namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Decide, para cada ponto do modelo que guarda um enum com código de contrato, se o conversor do código é instalado,
/// aceito ou recusado. Um conversor por enum, dividido por todos os pontos do mesmo enum.
/// </summary>
internal sealed class ConversaoDosEnumsDeContrato
{
    private readonly Dictionary<Type, ValueConverter> _conversoresPorEnum = [];

    public void Configurar(IMutableModel modelo)
    {
        foreach (IPontoDeConversao ponto in PontosDeConversaoDoModelo.Em(modelo))
        {
            Configurar(ponto);
        }
    }

    private void Configurar(IPontoDeConversao ponto)
    {
        Type tipo = SemNullable(ponto.TipoDeclarado);

        if (EnumsDeContrato.EhEnumQueDeclaraCodigo(tipo))
        {
            ConfigurarEnum(ponto, tipo);
        }
        else if (EnumDaColecao(tipo) is { } enumDaColecao)
        {
            throw ColecaoSemTipoDeElemento(ponto, enumDaColecao);
        }
    }

    private void ConfigurarEnum(IPontoDeConversao ponto, Type tipoDoEnum)
    {
        if (SemConversao(ponto))
        {
            ponto.DefinirConversor(ConversorDe(tipoDoEnum));
        }
        else if (!UsaOConversorDoCodigo(ponto, tipoDoEnum))
        {
            throw OutraConversao(ponto, tipoDoEnum);
        }
    }

    private ValueConverter ConversorDe(Type tipoDoEnum)
    {
        if (!_conversoresPorEnum.TryGetValue(tipoDoEnum, out ValueConverter? conversor))
        {
            conversor = Activator.CreateInstance(TipoDoConversor(tipoDoEnum)) as ValueConverter
                ?? throw new InvalidOperationException($"Não foi possível criar o conversor do código de contrato de {tipoDoEnum.Name}.");
            _conversoresPorEnum.Add(tipoDoEnum, conversor);
        }

        return conversor;
    }

    private static Type SemNullable(Type tipo) => Nullable.GetUnderlyingType(tipo) ?? tipo;

    private static Type TipoDoConversor(Type tipoDoEnum) =>
        typeof(CodigoDeContratoValueConverter<>).MakeGenericType(tipoDoEnum);

    private static bool SemConversao(IPontoDeConversao ponto) =>
        ponto.Conversor is null && ponto.TipoNoProvedor is null;

    private static bool UsaOConversorDoCodigo(IPontoDeConversao ponto, Type tipoDoEnum) =>
        ponto.Conversor?.GetType() == TipoDoConversor(tipoDoEnum);

    private static Type? EnumDaColecao(Type tipo) =>
        tipo.GetInterfaces().Append(tipo)
            .Where(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(static i => SemNullable(i.GenericTypeArguments[0]))
            .FirstOrDefault(EnumsDeContrato.EhEnumQueDeclaraCodigo);

    private static InvalidOperationException OutraConversao(IPontoDeConversao ponto, Type tipoDoEnum) =>
        new(
            $"{ponto.Descricao} é do enum '{tipoDoEnum.Name}', que declara código de contrato, mas já tem {ConversaoConfigurada(ponto)}. " +
            "O código de contrato já grava o enum: remova essa conversão (`HasConversion` na configuração ou `HaveConversion` nas convenções do modelo).");

    private static string ConversaoConfigurada(IPontoDeConversao ponto) =>
        ponto.Conversor is { } conversor
            ? $"o conversor '{conversor.GetType().Name}'"
            : $"a conversão para '{ponto.TipoNoProvedor?.Name}'";

    private static InvalidOperationException ColecaoSemTipoDeElemento(IPontoDeConversao ponto, Type tipoDoEnum) =>
        new(
            $"{ponto.Descricao} é uma coleção do enum '{tipoDoEnum.Name}', que declara código de contrato, mas não está mapeada como coleção primitiva. " +
            "Mapeie-a com `PrimitiveCollection` e sem conversor na coleção inteira: o código de contrato é instalado no elemento.");
}
