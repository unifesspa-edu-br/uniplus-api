namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

public sealed class NumeroDaChamadaValueConverter : ValueConverter<NumeroDaChamada, int>
{
    public NumeroDaChamadaValueConverter()
        : base(
            chamada => chamada.Valor,
            valor => ObjetoDeValorGravado.Reidratar(NumeroDaChamada.Criar(valor), nameof(NumeroDaChamada)))
    {
    }
}
