namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A declaração de prova presencial do processo (UNI-REQ-0407): se o processo tem prova presencial e,
/// só então, os municípios de aplicação, cada um pelo código do IBGE com a UF coerente com o prefixo, sem
/// repetição e até o limite técnico. Município de qualquer UF, com ou sem campus, é aceito.
/// </summary>
public sealed class DeclaracaoDaProvaPresencialTests
{
    private const int Limite = 100;
    private const string Marabá = "1504208";
    private const string Xinguara = "1508159";

    private static MunicipioDeAplicacaoInformado Municipio(string? codigoIbge, string? uf) => new(codigoIbge, uf);

    [Fact(DisplayName = "Com prova presencial, os municípios de qualquer UF são aceitos, com ou sem campus")]
    public void ComProva_MunicipiosDeQualquerUf_Aceita()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio(Marabá, "PA"), Municipio("3550308", "SP"), Municipio("5300108", "DF")], Limite);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TemProvaPresencial.Should().BeTrue();
        resultado.Value.Municipios.Select(static municipio => municipio.CodigoIbge).Should().Equal(Marabá, "3550308", "5300108");
    }

    [Fact(DisplayName = "O rascunho com prova presencial e sem município ainda é uma declaração: a publicação é que exige o município")]
    public void ComProva_SemMunicipio_E_Declaracao()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: true, [], Limite);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Municipios.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem prova presencial, a declaração não tem município")]
    public void SemProva_SemMunicipio_Aceita()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: false, [], Limite);

        resultado.Value!.TemProvaPresencial.Should().BeFalse();
        resultado.Value.Municipios.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem prova presencial, declarar município é recusado e a declaração não existe")]
    public void SemProva_ComMunicipio_Recusa()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: false, [Municipio(Marabá, "PA")], Limite);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios",
            Error = new { Code = ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial },
        });
    }

    [Theory(DisplayName = "Código do IBGE ausente ou fora da forma de município é recusado na posição do item")]
    [InlineData(null, CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("150420", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    [InlineData("15042088", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    [InlineData("15O4208", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    [InlineData("9904208", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    public void Codigo_ForaDaForma_Recusa(string? codigo, string esperado)
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio(Marabá, "PA"), Municipio(codigo, "PA")], Limite);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios[1].codigoIbge",
            Error = new { Code = esperado },
        });
    }

    [Theory(DisplayName = "UF ausente ou incoerente com o prefixo do código é recusada na posição do item")]
    [InlineData(null, CidadeReferenciaErrorCodes.UfObrigatoria)]
    [InlineData("", CidadeReferenciaErrorCodes.UfObrigatoria)]
    [InlineData("SP", CidadeReferenciaErrorCodes.UfIncoerente)]
    [InlineData("pa", CidadeReferenciaErrorCodes.UfIncoerente)]
    public void Uf_AusenteOuIncoerente_Recusa(string? uf, string esperado)
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio(Marabá, uf)], Limite);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios[0].uf",
            Error = new { Code = esperado },
        });
    }

    [Fact(DisplayName = "O município repetido é recusado na posição da repetição, pelo código")]
    public void Repetido_RecusaNaRepeticao()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio(Marabá, "PA"), Municipio(Xinguara, "PA"), Municipio(Marabá, "PA")], Limite);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios[2].codigoIbge",
            Error = new { Code = ProvaPresencialErrorCodes.MunicipioRepetido },
        });
    }

    [Fact(DisplayName = "O limite técnico de municípios é aceito e o seguinte é recusado")]
    public void Limite_AceitaOLimiteERecusaOSeguinte()
    {
        MunicipioDeAplicacaoInformado[] noLimite = [.. Enumerable.Range(0, 3).Select(static indice => Municipio($"15{indice:00000}", "PA"))];

        DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: true, noLimite, limiteDeMunicipios: 3).IsSuccess.Should().BeTrue();
        DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: true, noLimite, limiteDeMunicipios: 2).Errors.Should()
            .ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "provaPresencial.municipios",
                Error = new { Code = ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite },
            });
    }

    [Fact(DisplayName = "As recusas independentes se acumulam: código, UF, repetição e limite")]
    public void Recusas_Acumulam()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio("150420", "PA"), Municipio(Marabá, "SP"), Municipio(Marabá, "PA")], limiteDeMunicipios: 2);

        resultado.Errors.Select(static erro => (Campo: erro.Field, Codigo: erro.Error.Code)).Should().BeEquivalentTo(
        [
            ("provaPresencial.municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite),
            ("provaPresencial.municipios[0].codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido),
            ("provaPresencial.municipios[1].uf", CidadeReferenciaErrorCodes.UfIncoerente),
            ("provaPresencial.municipios[2].codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido),
        ]);
    }

    [Fact(DisplayName = "Nenhuma mensagem de recusa reflete o código nem a UF recebidos")]
    public void Mensagens_NaoRefletemOValorRecebido()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [Municipio("15O42", "ZZ"), Municipio(Marabá, "SP"), Municipio(Marabá, "SP")], limiteDeMunicipios: 2);

        resultado.Errors.Select(static erro => erro.Error.Message).Should().NotContain(
            mensagem => mensagem.Contains("15O42", StringComparison.Ordinal) || mensagem.Contains(Marabá, StringComparison.Ordinal)
                || mensagem.Contains("ZZ", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O município nulo na lista é recusado na posição dele, sem interromper as demais recusas")]
    public void MunicipioNulo_RecusaNaPosicaoEAcumula()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(
            temProvaPresencial: true, [null!, Municipio(Marabá, "SP")], Limite);

        resultado.Errors.Select(static erro => (Campo: erro.Field, Codigo: erro.Error.Code)).Should().BeEquivalentTo(
        [
            ("provaPresencial.municipios[0].codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio),
            ("provaPresencial.municipios[0].uf", CidadeReferenciaErrorCodes.UfObrigatoria),
            ("provaPresencial.municipios[1].uf", CidadeReferenciaErrorCodes.UfIncoerente),
        ]);
    }

    [Fact(DisplayName = "Duas declarações são iguais quando têm a mesma prova presencial e os mesmos municípios, na mesma ordem")]
    public void Igualdade_PelaProvaEPelosMunicipiosEmOrdem()
    {
        DeclaracaoDaProvaPresencial Declarar(bool prova, params MunicipioDeAplicacaoInformado[] municipios) =>
            DeclaracaoDaProvaPresencial.Declarar(prova, municipios, Limite).Value!;

        Declarar(true, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA")).Should().Be(Declarar(true, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA")));
        Declarar(true, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA")).Should().NotBe(Declarar(true, Municipio(Xinguara, "PA"), Municipio(Marabá, "PA")));
        Declarar(true).Should().NotBe(Declarar(false));
    }

    [Fact(DisplayName = "A igualdade das declarações é simétrica, tem hash coerente, e recusa nulo")]
    public void Igualdade_SimetriaHashNuloECopia()
    {
        DeclaracaoDaProvaPresencial primeira = DeclaracaoDaProvaPresencial.Declarar(true, [Municipio(Marabá, "PA")], Limite).Value!;
        DeclaracaoDaProvaPresencial segunda = DeclaracaoDaProvaPresencial.Declarar(true, [Municipio(Marabá, "PA")], Limite).Value!;
        DeclaracaoDaProvaPresencial outraUf = DeclaracaoDaProvaPresencial.Declarar(true, [Municipio("3550308", "SP")], Limite).Value!;

        primeira.Should().Be(segunda);
        segunda.Should().Be(primeira);
        primeira.GetHashCode().Should().Be(segunda.GetHashCode());
        primeira.Should().NotBe(outraUf);
        primeira.Equals(null).Should().BeFalse();
    }
}
