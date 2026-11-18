namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RascunhoDeProcessoSeletivoPrecondicaoTests
{
    private static readonly Guid Unidade = Guid.CreateVersion7();

    private static RascunhoDeProcessoSeletivo Novo() =>
        RascunhoDeProcessoSeletivo.Criar(Unidade, DocumentoDoRascunho.Criar("""{"a":1}""", "Nome").Value!).Value!;

    [Fact(DisplayName = "A exigência da precondição recusa a requisição sem If-Match com a precondição requerida")]
    public void ExigirPrecondicao_Ausente_Requerida()
    {
        Result resultado = RascunhoDeProcessoSeletivo.ExigirPrecondicao(PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
    }

    [Fact(DisplayName = "A exigência da precondição aceita qualquer If-Match presente, sem conferir a revisão")]
    public void ExigirPrecondicao_Presente_Aceita()
    {
        RascunhoDeProcessoSeletivo.ExigirPrecondicao(PrecondicaoIfMatch.DeTags(["\"9\""])).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Conferir a precondição sem If-Match devolve a precondição requerida")]
    public void ConferirPrecondicao_Ausente_Requerida()
    {
        Result resultado = Novo().ConferirPrecondicao(PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
    }

    [Fact(DisplayName = "Conferir a precondição de outra revisão devolve a precondição falha, sem mudar o rascunho")]
    public void ConferirPrecondicao_OutraRevisao_Falhou()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.ConferirPrecondicao(PrecondicaoIfMatch.DeTags(["\"2\""]));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        rascunho.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Conferir a precondição da revisão atual aceita, e o curinga também")]
    public void ConferirPrecondicao_RevisaoAtual_Aceita()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        rascunho.ConferirPrecondicao(PrecondicaoIfMatch.DeTags([rascunho.ETag])).IsSuccess.Should().BeTrue();
        rascunho.ConferirPrecondicao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "A recusa de unidade ausente é a mesma do agregado, exposta para quem confere o documento antes")]
    public void UnidadeAdministradoraAusente_TemOCodigoDoAgregado()
    {
        RascunhoDeProcessoSeletivo.UnidadeAdministradoraAusente.Code
            .Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
    }
}
