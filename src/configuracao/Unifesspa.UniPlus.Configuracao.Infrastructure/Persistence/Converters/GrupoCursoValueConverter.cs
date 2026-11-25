namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

// A leitura falha com contexto se o valor gravado não respeita a regra do domínio.
public sealed class GrupoCursoValueConverter : ValueConverter<GrupoCurso, string>
{
    public GrupoCursoValueConverter()
        : base(
            grupo => grupo.Codigo,
            valor => ObjetoDeValorGravado.Reidratar(GrupoCurso.Criar(valor), nameof(GrupoCurso)))
    {
    }

    /// <summary>
    /// Os códigos dos quatro grupos como lista SQL (<c>'A', 'B', …</c>), para o CHECK das
    /// colunas que este converter grava — curso e Pesos por Área usam a mesma lista.
    /// </summary>
    internal static string CodigosEmSql() =>
        SqlDosCodigosDeContrato.Lista(GrupoCurso.Todos.Select(static grupo => grupo.Codigo));
}
