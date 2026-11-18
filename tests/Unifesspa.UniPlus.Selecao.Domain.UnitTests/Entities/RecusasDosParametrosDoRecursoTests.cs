namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class RecusasDosParametrosDoRecursoTests
{
    [Fact]
    public void Criar_RecusasIndependentes_AcumulaNasDuasEntidades()
    {
        ParametrosRegraPrazoRecurso parametros = new(0, UnidadePrazo.Nenhuma, 0, null, false, 1, null);
        ReferenciaRegra regra = ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64));
        string[] sufixos = ["PrazoNaoPositivo", "SuspensividadeNaoPositiva", "SuspensividadeSuperiorSemInstanciaSuperior"];

        RegraRecursoFase.Criar(regra, parametros, Guid.Empty).Errors.Select(static erro => erro.Error.Code)
            .Should().Equal(sufixos.Select(static sufixo => $"RegraRecursoFase.{sufixo}"));
        RecursoDaEtapa.Criar(AncoraDoRecurso.AtoPublicado, regra, parametros, Guid.Empty).Errors.Select(static erro => erro.Error.Code)
            .Should().Equal(sufixos.Select(static sufixo => $"RecursoDaEtapa.{sufixo}"));
    }
}
