namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Respostas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CodigoComDescricaoTests
{
    private static readonly JsonSerializerOptions OpcoesDaResposta = new(JsonSerializerDefaults.Web);

    public enum Fase
    {
        [CodigoDeContrato("INSCRICAO", "Inscrição", Descricao = "A pessoa ainda pode se inscrever.")]
        Inscricao = 1,

        [CodigoDeContrato("RESULTADO", "Resultado")]
        Resultado = 2,
    }

    [Fact(DisplayName = "O valor com descrição declarada devolve o código, o rótulo e a descrição do membro")]
    public void De_MembroComDescricao_DevolveCodigoRotuloEDescricao()
    {
        CodigoComDescricao<Fase> codigo = CodigoComDescricao<Fase>.De(Fase.Inscricao);

        codigo.Codigo.Should().Be("INSCRICAO");
        codigo.Rotulo.Should().Be("Inscrição");
        codigo.Descricao.Should().Be("A pessoa ainda pode se inscrever.");
    }

    [Fact(DisplayName = "O valor cujo membro não declarou descrição lança, citando o membro e o enum, em vez de devolver descrição nula")]
    public void De_MembroSemDescricao_LancaCitandoOMembroEOEnum()
    {
        Func<CodigoComDescricao<Fase>> semDescricao = () => CodigoComDescricao<Fase>.De(Fase.Resultado);

        semDescricao.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Resultado").And.Contain("Fase");
    }

    [Fact(DisplayName = "O valor que não é membro do enum, como num cast de inteiro, lança")]
    public void De_ValorForaDoEnum_Lanca()
    {
        Func<CodigoComDescricao<Fase>> foraDoEnum = () => CodigoComDescricao<Fase>.De((Fase)99);

        foraDoEnum.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O código com descrição sai na resposta com os três campos")]
    public void Serializar_SaiComCodigoRotuloEDescricao()
    {
        JsonElement json = JsonSerializer.SerializeToElement(CodigoComDescricao<Fase>.De(Fase.Inscricao), OpcoesDaResposta);

        json.EnumerateObject().Select(static p => (p.Name, Valor: p.Value.GetString())).Should().Equal(
            ("codigo", "INSCRICAO"),
            ("rotulo", "Inscrição"),
            ("descricao", "A pessoa ainda pode se inscrever."));
    }
}
