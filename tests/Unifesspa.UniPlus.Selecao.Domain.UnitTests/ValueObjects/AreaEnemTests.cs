namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class AreaEnemTests
{
    [Theory(DisplayName = "Criar aceita as cinco áreas canônicas do ENEM")]
    [InlineData(AreaEnem.Redacao)]
    [InlineData(AreaEnem.CienciasDaNatureza)]
    [InlineData(AreaEnem.CienciasHumanas)]
    [InlineData(AreaEnem.LinguagensECodigos)]
    [InlineData(AreaEnem.Matematica)]
    public void Criar_AreaCanonica_Aceita(string valor)
    {
        Result<AreaEnem> resultado = AreaEnem.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Fact(DisplayName = "O conjunto canônico é exatamente o das cinco constantes declaradas")]
    public void Valores_CoincideComAsConstantes()
    {
        // Lidas por reflexão: uma área nova declarada como constante e esquecida
        // fora do conjunto ficaria inalcançável pelo Criar, e uma lista escrita à
        // mão aqui repetiria o mesmo esquecimento.
        IEnumerable<string> constantes = typeof(AreaEnem)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static campo => campo.IsLiteral && campo.FieldType == typeof(string))
            .Select(static campo => (string)campo.GetRawConstantValue()!);

        AreaEnem.Valores.Should().BeEquivalentTo(constantes);
        AreaEnem.Valores.Should().HaveCount(5);
    }

    [Fact(DisplayName = "O conjunto canônico não se deixa alterar nem por cast")]
    public void Valores_NaoAceitaAlteracao()
    {
        ISet<string> comoConjunto = (ISet<string>)AreaEnem.Valores;

        Action acrescentar = () => comoConjunto.Add("Física");
        Action remover = () => comoConjunto.Remove(AreaEnem.Redacao);

        acrescentar.Should().Throw<NotSupportedException>();
        remover.Should().Throw<NotSupportedException>();
        AreaEnem.Criar("Física").IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar normaliza espaços nas bordas (Trim)")]
    public void Criar_ComEspacos_Normaliza()
    {
        Result<AreaEnem> resultado = AreaEnem.Criar("  Matemática  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(AreaEnem.Matematica);
    }

    [Theory(DisplayName = "Criar aceita o acento em forma decomposta e devolve a grafia composta (NFC)")]
    [InlineData("Matemática", AreaEnem.Matematica)]
    [InlineData("Redação", AreaEnem.Redacao)]
    [InlineData(" Linguagens e Códigos ", AreaEnem.LinguagensECodigos)]
    public void Criar_AcentoDecomposto_RecompoeEmNfc(string valor, string esperado)
    {
        Result<AreaEnem> resultado = AreaEnem.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(esperado);
        AreaEnem.EhValido(valor).Should().BeTrue();
    }

    [Fact(DisplayName = "Criar recusa texto com surrogate isolado com falha de domínio, sem exceção")]
    public void Criar_SurrogateIsolado_FalhaSemExcecao()
    {
        string comSurrogateIsolado = "Matem\uD800tica";

        Result<AreaEnem> resultado = AreaEnem.Criar(comSurrogateIsolado);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaEnemErrorCodes.ForaDoDominio);
        AreaEnem.EhValido(comSurrogateIsolado).Should().BeFalse();
    }

    [Theory(DisplayName = "Criar recusa área fora do domínio com falha de domínio, sem exceção")]
    [InlineData("Física")]
    [InlineData("matemática")]
    [InlineData("Matematica")]
    [InlineData("Linguagens, Códigos e suas Tecnologias")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_AreaForaDoDominio_Falha(string? valor)
    {
        Result<AreaEnem> resultado = AreaEnem.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaEnemErrorCodes.ForaDoDominio);
    }

    [Fact(DisplayName = "A falha lista os valores aceitos")]
    public void Criar_ForaDoDominio_MensagemListaOsValores()
    {
        Result<AreaEnem> resultado = AreaEnem.Criar("Física");

        foreach (string area in AreaEnem.Valores)
        {
            resultado.Error!.Message.Should().Contain(area);
        }
    }

    [Theory(DisplayName = "EhValido reflete a pertinência ao domínio fechado")]
    [InlineData(AreaEnem.Redacao, true)]
    [InlineData(" Ciências Humanas ", true)]
    [InlineData("Física", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EhValido_RefletePertinencia(string? valor, bool esperado)
    {
        AreaEnem.EhValido(valor).Should().Be(esperado);
    }

    [Fact(DisplayName = "ToString devolve o valor canônico")]
    public void ToString_DevolveValor()
    {
        AreaEnem.Criar(AreaEnem.Redacao).Value!.ToString().Should().Be(AreaEnem.Redacao);
    }
}
