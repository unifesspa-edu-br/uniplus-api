namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia Cpf ↔ string (11 dígitos) no banco. A revalidação na leitura é
// deliberada: dado corrompido (alteração manual no banco) falha alto via
// InvalidOperationException com contexto, em vez de produzir Cpf inválido
// ou NRE tardia no primeiro uso da propriedade.
public sealed class CpfValueConverter : ValueConverter<Cpf, string>
{
    public CpfValueConverter()
        : base(
            cpf => cpf.Valor,
            valor => ValueObjectMaterialization.Reidratar(Cpf.Criar(valor), nameof(Cpf)))
    {
    }
}
