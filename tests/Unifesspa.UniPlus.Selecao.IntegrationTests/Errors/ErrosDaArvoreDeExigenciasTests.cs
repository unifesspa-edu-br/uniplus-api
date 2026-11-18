namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ErrosDaArvoreDeExigenciasTests
{
    [Fact]
    public void Mapeamento_QuantidadeMinimaEmGrupoE_ExplicaARecusa()
    {
        IDomainErrorMapper mapper = MapperDoModuloDeTeste.Criar();

        bool encontrado = mapper.TryGetMapping("NoExigencia.QuantidadeMinimaProibidaEmGrupoE", out DomainErrorMapping? erro);

        encontrado.Should().BeTrue();
        erro!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        erro.Title.Should().Be("Quantidade mínima de documentos não permitida em grupo E");
    }
}
