namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A identidade dos itens do checklist estrutural é contrato: um cliente liga a navegação
/// do editor ao par código-dimensão para não depender de comparar frases em português. Um
/// código repetido, ausente, ou uma dimensão fora do conjunto fechado quebram esse contrato
/// sem quebrar compilação — por isso são asserção, e não convenção.
/// </summary>
public sealed class IdentidadeDosItensDeConformidadeTests
{
    private static IReadOnlyList<ItemConformidade> Checklist() =>
        ProcessoConformeFactory.Criar().AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);

    private static IReadOnlyList<string> DimensoesDeclaradas() =>
    [
        .. typeof(DimensaoConformidade)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(static f => (string)f.GetRawConstantValue()!),
    ];

    [Fact(DisplayName = "Nenhum código de item se repete no checklist — o código é o que distingue invariantes dentro da mesma dimensão")]
    public void CodigosSaoUnicos()
    {
        IReadOnlyList<string> codigos = [.. Checklist().Select(static i => i.Codigo)];

        codigos.Should().OnlyHaveUniqueItems(
            "dois itens com o mesmo código são indistinguíveis para quem recebe a lista, e a "
            + "navegação do cliente levaria à seção de um deles sempre");
    }

    [Fact(DisplayName = "Todo item tem código e mensagem preenchidos — nenhum entra na resposta identificado só pelo texto")]
    public void TodosOsItensTemCodigoEMensagem()
    {
        Checklist().Should().AllSatisfy(item =>
        {
            item.Codigo.Should().NotBeNullOrWhiteSpace();
            item.Mensagem.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact(DisplayName = "Toda dimensão usada pertence ao conjunto fechado declarado — não há dimensão inventada no ponto de uso")]
    public void DimensoesPertencemAoConjuntoFechado()
    {
        IReadOnlyList<string> declaradas = DimensoesDeclaradas();

        Checklist().Select(static i => i.Dimensao).Distinct().Should().BeSubsetOf(declaradas);
    }

    [Fact(DisplayName = "Toda dimensão declarada é usada por algum item — uma seção sem item é vocabulário que envelheceu")]
    public void DimensoesDeclaradasSaoTodasUsadas()
    {
        IReadOnlyList<string> usadas = [.. Checklist().Select(static i => i.Dimensao).Distinct()];

        DimensoesDeclaradas().Should().BeSubsetOf(usadas);
    }

    [Theory(DisplayName = "Código e dimensão são tokens estáveis em snake_case — sem acento, espaço ou maiúscula que a URL ou o cliente precisem escapar")]
    [InlineData(true)]
    [InlineData(false)]
    public void CodigoEDimensaoSaoTokensEstaveis(bool avaliarCodigo)
    {
        IEnumerable<string> tokens = avaliarCodigo
            ? Checklist().Select(static i => i.Codigo)
            : Checklist().Select(static i => i.Dimensao);

        tokens.Should().AllSatisfy(token =>
            Regex.IsMatch(token, "^[a-z][a-z0-9_]*$").Should().BeTrue($"'{token}' precisa ser um token estável"));
    }

    [Fact(DisplayName = "A mensagem pode mudar sem tocar em código nem dimensão — é o que permite revisar a redação sem quebrar cliente")]
    public void MensagemEIndependenteDaIdentidade()
    {
        ItemConformidade original = Checklist()[0];

        // `with` é a prova direta: a mensagem é um campo à parte da identidade, e reescrevê-la
        // produz um item que continua sendo o mesmo item para quem navega por código.
        ItemConformidade reescrito = original with { Mensagem = "Qualquer outra redação" };

        reescrito.Codigo.Should().Be(original.Codigo);
        reescrito.Dimensao.Should().Be(original.Dimensao);
        reescrito.Ok.Should().Be(original.Ok);
        reescrito.Should().NotBe(original, "a mensagem faz parte do valor, mesmo não fazendo parte da identidade");
    }
}
