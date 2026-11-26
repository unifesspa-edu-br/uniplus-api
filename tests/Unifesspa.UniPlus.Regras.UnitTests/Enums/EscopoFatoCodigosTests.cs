namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class EscopoFatoCodigosTests
{
    [Fact]
    public void Codigos_ValoresDeclarados_ResolvemNosDoisSentidos()
    {
        (EscopoFato Valor, string Codigo)[] pares =
        [
            (EscopoFato.Candidato, "CANDIDATO"),
            (EscopoFato.MembroGrupo, "MEMBRO_GRUPO"),
        ];
        CatalogoDe<EscopoFato>.Valores().Should().Equal(pares.Select(static par => par.Valor));
        foreach ((EscopoFato valor, string codigo) in pares)
        {
            valor.Codigo().Should().Be(codigo);
            CatalogoDe<EscopoFato>.TentarObter(codigo, out EscopoFato obtido).Should().BeTrue();
            obtido.Should().Be(valor);
        }
    }
}
