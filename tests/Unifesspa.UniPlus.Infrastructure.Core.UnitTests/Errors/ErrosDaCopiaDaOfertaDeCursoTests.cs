namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

public sealed class ErrosDaCopiaDaOfertaDeCursoTests
{
    [Fact]
    public void GetMappings_CodigoDaOfertaInvalido_RegistraContratoPublico()
    {
        DomainErrorMapping mapeamento = new KernelDomainErrorRegistration().GetMappings()
            .Single(par => par.Key == CopiaDaOfertaDeCursoErrorCodes.CodigoFormatoInvalido).Value;
        mapeamento.Code.Should().Be("uniplus.copia_da_oferta_de_curso.codigo_formato_invalido");
        mapeamento.Status.Should().Be(422);
        mapeamento.Title.Should().Be("Formato do código da oferta de curso inválido");
    }
}
