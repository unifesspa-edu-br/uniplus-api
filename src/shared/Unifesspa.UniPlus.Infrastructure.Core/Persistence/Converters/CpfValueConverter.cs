namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia Cpf ↔ string (11 dígitos) no banco. A materialização chama
// Cpf.Criar com o valor armazenado — invariantes do VO foram garantidos
// no momento da escrita, mas a revalidação na leitura é deliberada:
// dado corrompido (alteração manual no banco) falha alto em vez de
// produzir Cpf inválido silenciosamente.
public sealed class CpfValueConverter : ValueConverter<Cpf, string>
{
    public CpfValueConverter()
        : base(
            cpf => cpf.Valor,
            valor => Cpf.Criar(valor).Value!)
    {
    }
}
