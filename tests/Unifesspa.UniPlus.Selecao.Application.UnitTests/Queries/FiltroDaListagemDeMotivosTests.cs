namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class FiltroDaListagemDeMotivosTests
{
    [Fact(DisplayName = "Recusa tipo inválido")]
    public void Ler_TipoInvalido_RecusaTipo()
    {
        Result<FiltroDosMotivos> resultado = FiltroDaListagemDeMotivos.Ler("desconhecido", null, false);
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FiltroDaListagemDeMotivos.TipoInvalido);
    }

    [Fact(DisplayName = "Recusa subclassificação inválida")]
    public void Ler_SubclassificacaoInvalida_RecusaSubclassificacao()
    {
        Result<FiltroDosMotivos> resultado = FiltroDaListagemDeMotivos.Ler(null, "desconhecido", false);
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FiltroDaListagemDeMotivos.SubclassificacaoInvalida);
    }

    [Fact(DisplayName = "Acumula recusas na ordem dos filtros")]
    public void Ler_AmbosInvalidos_AcumulaRecusasEmOrdem()
    {
        Result<FiltroDosMotivos> resultado = FiltroDaListagemDeMotivos.Ler("invalido", "invalido", false);
        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(
            FiltroDaListagemDeMotivos.TipoInvalido, FiltroDaListagemDeMotivos.SubclassificacaoInvalida);
    }

    [Fact(DisplayName = "Usa o código publicado da subclassificação")]
    public void Ler_SubclassificacaoConhecida_UsaCodigoPublicado()
    {
        Result<FiltroDosMotivos> resultado = FiltroDaListagemDeMotivos.Ler(null, "CADASTRO_UNICO", false);
        resultado.Value!.Subclassificacao.Should().Be("CADASTRO_UNICO");
    }
}
