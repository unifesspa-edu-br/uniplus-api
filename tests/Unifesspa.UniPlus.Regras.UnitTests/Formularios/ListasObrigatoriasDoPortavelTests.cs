namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>
/// As quatro listas do formulário portável (páginas, termos, derivações e agregados) são obrigatórias: a lista ausente
/// ou nula é recusada como "obrigatório", acumulada com as demais, e só a lista vazia declara "nenhum".
/// </summary>
public sealed class ListasObrigatoriasDoPortavelTests
{
    private static readonly JsonSerializerOptions OpcoesDoFio = new(JsonSerializerDefaults.Web);

    [Theory(DisplayName = "Lista ausente ou nula é recusada como obrigatória, nomeando só ela")]
    [InlineData("paginas")]
    [InlineData("termos")]
    [InlineData("derivacoes")]
    [InlineData("agregados")]
    public void ParaDefinicao_SemUmaLista_RecusaSoEssaLista(string lista)
    {
        Result<DefinicaoFormulario> definicao = SemALista(Formulario(Item("RENDA")), lista).ParaDefinicao();

        FieldError recusa = definicao.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(lista);
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        recusa.Error.Message.Should().StartWith("Informe ").And.Contain("envie []");
    }

    [Fact(DisplayName = "Corpo sem nenhuma das listas acumula as quatro recusas, na ordem do contrato")]
    public void ParaDefinicao_CorpoVazio_AcumulaAsQuatroListas()
    {
        FormularioPortavel formulario = JsonSerializer.Deserialize<FormularioPortavel>("{}", OpcoesDoFio)!;

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal("paginas", "termos", "derivacoes", "agregados");
        definicao.Errors.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "Lista ausente acumula com o elemento nulo de outra lista, cada um no seu campo")]
    public void ParaDefinicao_ListaAusenteEElementoNulo_AcumulaNaOrdemDoDocumento()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA")) with { Termos = [null!], Agregados = null };

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal("termos[0]", "agregados");
    }

    [Fact(DisplayName = "Listas vazias declaram nenhum elemento e não são recusadas pela forma")]
    public void ParaDefinicao_ListasVazias_NaoSaoRecusadasPelaForma()
    {
        FormularioPortavel formulario = new([], [], [], []);

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().NotContain(["paginas", "termos", "derivacoes", "agregados"]);
    }

    private static FormularioPortavel SemALista(FormularioPortavel formulario, string lista) => lista switch
    {
        "paginas" => formulario with { Paginas = null },
        "termos" => formulario with { Termos = null },
        "derivacoes" => formulario with { Derivacoes = null },
        "agregados" => formulario with { Agregados = null },
        _ => throw new ArgumentOutOfRangeException(nameof(lista), lista, "Lista desconhecida."),
    };
}
