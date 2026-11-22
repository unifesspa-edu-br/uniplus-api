namespace Unifesspa.UniPlus.Authorization.UnitTests.Projecao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ItemClassificadoSerializacaoTests
{
    [Fact]
    public void ItemDerivado_Serializacao_OmiteProtecao()
    {
        Item item = new("valor");
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(item));

        json.RootElement.TryGetProperty(nameof(Item.Conteudo), out _).Should().BeTrue();
        json.RootElement.TryGetProperty(nameof(Item.Natureza), out _).Should().BeFalse();
        json.RootElement.TryGetProperty(nameof(Item.Classificacao), out _).Should().BeFalse();
        json.RootElement.TryGetProperty(nameof(Item.BaseLegal), out _).Should().BeFalse();
    }

    private sealed record Item(string Conteudo) : ItemClassificado(default, default, "fundamento");
}
