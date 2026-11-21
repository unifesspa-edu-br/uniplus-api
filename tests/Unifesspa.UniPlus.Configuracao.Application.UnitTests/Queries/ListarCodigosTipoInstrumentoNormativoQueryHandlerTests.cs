namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class ListarCodigosTipoInstrumentoNormativoQueryHandlerTests
{
    [Fact(DisplayName = "O vocabulário devolve código, rótulo e descrição na ordem declarada no enum")]
    public void Handle_DevolveOsTiposNaOrdemDoEnumComRotuloEDescricao()
    {
        IReadOnlyList<CodigoComDescricao<TipoInstrumentoNormativo>> tipos =
            ListarCodigosTipoInstrumentoNormativoQueryHandler.Handle(new ListarCodigosTipoInstrumentoNormativoQuery());

        tipos.Select(static t => (t.Codigo, t.Rotulo)).Should().Equal(
            ("LEI", "Lei"),
            ("DECRETO", "Decreto"),
            ("PORTARIA", "Portaria"),
            ("RESOLUCAO", "Resolução"),
            ("INSTRUCAO_NORMATIVA", "Instrução Normativa"),
            ("PARECER", "Parecer"),
            ("EDITAL", "Edital"));
        tipos.Should().AllSatisfy(static t => t.Descricao.Should().NotBeNullOrWhiteSpace());
        tipos[0].Descricao.Should().Be("Ato normativo de maior hierarquia no ordenamento infraconstitucional, emanado do Poder Legislativo.");
    }
}
