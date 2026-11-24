namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O texto que o comentário da coluna traz para listar os códigos que o enum declara, na ordem do enum.</summary>
internal static class ComentarioDosCodigosDeContrato
{
    internal static string Codigos<TEnum>()
        where TEnum : struct, Enum =>
        $"Códigos: {string.Join(", ", CatalogoDe<TEnum>.Todos().Select(static declarado => declarado.Codigo))}.";
}
