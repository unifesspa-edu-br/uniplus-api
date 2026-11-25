namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

// A leitura falha com contexto se o valor gravado não respeita a regra do domínio.
public sealed class CodigoRecursoAcessibilidadeValueConverter : ValueConverter<CodigoRecursoAcessibilidade, string>
{
    public CodigoRecursoAcessibilidadeValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => ObjetoDeValorGravado.Reidratar(CodigoRecursoAcessibilidade.Criar(valor), nameof(CodigoRecursoAcessibilidade)))
    {
    }
}
