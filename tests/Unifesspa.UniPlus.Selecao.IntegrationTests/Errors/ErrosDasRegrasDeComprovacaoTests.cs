namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ErrosDasRegrasDeComprovacaoTests
{
    [Fact]
    public void Mapeamento_QuantidadeMinimaEmGrupoDeTodosOsItens_ExplicaARecusa()
    {
        IDomainErrorMapper mapper = MapperDoModuloDeTeste.Criar();

        bool encontrado = mapper.TryGetMapping("RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos", out DomainErrorMapping? erro);

        encontrado.Should().BeTrue();
        erro!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        erro.Title.Should().Be("O grupo que exige todos os itens não declara quantidade mínima");
    }
}
