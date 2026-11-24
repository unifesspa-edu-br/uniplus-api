namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class ContextoDosRotulosTests
{
    [Fact]
    public void Acrescentar_ValorPresente_EscreveRotuloDeclarado()
    {
        JsonObject objeto = [];

        ContextoDosRotulos.Acrescentar(objeto, "papel", (PapelDaBanca?)PapelDaBanca.AvaliaOMerito);

        objeto["contexto"]!["papelRotulo"]!.GetValue<string>().Should().Be(PapelDaBanca.AvaliaOMerito.Rotulo());
    }

    [Fact]
    public void Acrescentar_ValorAusente_NaoEscreveContexto()
    {
        JsonObject objeto = [];

        ContextoDosRotulos.Acrescentar<PapelDaBanca>(objeto, "papel", null);

        objeto.ContainsKey("contexto").Should().BeFalse();
    }

    [Fact]
    public void Serializar_ContextoComDoisRotulos_OrdenaChaves()
    {
        JsonObject objeto = [];
        ContextoDosRotulos.Acrescentar(objeto, "z", (PapelDaBanca?)PapelDaBanca.AvaliaOMerito);
        ContextoDosRotulos.Acrescentar(objeto, "a", (PapelDaBanca?)PapelDaBanca.AvaliaOMerito);

        JsonObject canonico = JsonNode.Parse(PerfilCanonicoV1.Instancia.Serializar(objeto))!.AsObject();

        canonico["contexto"]!.AsObject().Select(static par => par.Key).Should().Equal("aRotulo", "zRotulo");
    }

    [Fact]
    public void AcrescentarLista_MaisDeUmValor_EscreveRotulosNaMesmaOrdem()
    {
        JsonObject objeto = [];

        ContextoDosRotulos.AcrescentarLista(objeto, "papeis", new[] { PapelDaBanca.AvaliaOMerito, PapelDaBanca.AvaliaOMerito });

        objeto["contexto"]!["papeisRotulos"]!.AsArray().Select(static texto => texto!.GetValue<string>())
            .Should().Equal(PapelDaBanca.AvaliaOMerito.Rotulo(), PapelDaBanca.AvaliaOMerito.Rotulo());
    }

    [Fact]
    public void AcrescentarLista_ListaVazia_EscreveListaVaziaDeRotulos()
    {
        JsonObject objeto = [];

        ContextoDosRotulos.AcrescentarLista(objeto, "papeis", Array.Empty<PapelDaBanca>());

        objeto["contexto"]!["papeisRotulos"]!.AsArray().Should().BeEmpty();
    }
}
