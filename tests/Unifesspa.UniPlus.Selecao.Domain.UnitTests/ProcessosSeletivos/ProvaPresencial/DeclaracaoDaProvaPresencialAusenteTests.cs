namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

public sealed class DeclaracaoDaProvaPresencialAusenteTests
{
    private static (string? Campo, string Codigo)[] Recusas(Result<DeclaracaoDaProvaPresencial> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "Declaração e municípios ausentes são recusados juntos como obrigatórios")]
    public void Declarar_TudoAusente_AcumulaOsDoisObrigatorios()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(null, null, 10);

        Recusas(resultado).Should().BeEquivalentTo(
        [
            ("provaPresencial.temProvaPresencial", CodigosDeValidacao.Obrigatorio),
            ("provaPresencial.municipios", CodigosDeValidacao.Obrigatorio),
        ]);
    }

    [Fact(DisplayName = "Com prova declarada, a lista de municípios ausente é a única recusa")]
    public void Declarar_ComProvaESemLista_RecusaSoALista()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(true, null, 10);

        Recusas(resultado).Should().Equal(("provaPresencial.municipios", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Lista vazia sem prova declarada é a declaração de nenhum município e passa")]
    public void Declarar_SemProvaEListaVazia_Passa()
    {
        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(false, [], 10);

        resultado.IsSuccess.Should().BeTrue();
    }
}
