namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

public sealed class CodigoDoFormularioValueConverter : ValueConverter<CodigoDoFormulario, string>
{
    public CodigoDoFormularioValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => ObjetoDeValorGravado.Reidratar(CodigoDoFormulario.Criar(valor), nameof(CodigoDoFormulario)))
    {
    }
}
