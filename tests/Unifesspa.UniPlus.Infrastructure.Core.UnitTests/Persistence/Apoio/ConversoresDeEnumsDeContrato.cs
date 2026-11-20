namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

internal sealed class ConversorDoNomeEmCaixaAlta : ValueConverter<FormatoDeAula, string>
{
    public ConversorDoNomeEmCaixaAlta()
        : base(
            formato => formato.ToString().ToUpperInvariant(),
            texto => Enum.Parse<FormatoDeAula>(texto, true))
    {
    }
}

internal sealed class ConversorDaListaPelosNomes : ValueConverter<List<FormatoDeAula>, string>
{
    public ConversorDaListaPelosNomes()
        : base(
            formatos => string.Join(',', formatos),
            texto => texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(nome => Enum.Parse<FormatoDeAula>(nome)).ToList())
    {
    }
}

internal sealed class ConversorDasAnotacoesEmJson : ValueConverter<List<Anotacao>, string>
{
    public ConversorDasAnotacoesEmJson()
        : base(
            anotacoes => JsonSerializer.Serialize(anotacoes, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<List<Anotacao>>(json, (JsonSerializerOptions?)null)!)
    {
    }
}
