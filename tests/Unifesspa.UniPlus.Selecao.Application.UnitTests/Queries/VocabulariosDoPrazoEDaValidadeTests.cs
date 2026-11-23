namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

public sealed class VocabulariosDoPrazoEDaValidadeTests
{
    [Fact]
    public void ListarEscolhasDoPrazoDeEmissao_Consulta_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<EscolhaDoPrazoDeEmissao>> opcoes = ListarEscolhasDoPrazoDeEmissaoQueryHandler.Handle(new());

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("SEM_PRAZO", "COM_PRAZO");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarEscolhasDaValidade_Consulta_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<EscolhaDaValidade>> opcoes = ListarEscolhasDaValidadeQueryHandler.Handle(new());

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("NAO_EXIGE_DOCUMENTO_VALIDO", "EXIGE_DOCUMENTO_VALIDO");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarUnidadesDoPrazoDeEmissao_Consulta_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<UnidadeDoPrazoDeEmissao>> opcoes = ListarUnidadesDoPrazoDeEmissaoQueryHandler.Handle(new());

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("DIAS", "MESES", "ANOS");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarTiposDaDataDeReferencia_Consulta_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<TipoDaDataDeReferencia>> opcoes = ListarTiposDaDataDeReferenciaQueryHandler.Handle(new());

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("FIM_DAS_INSCRICOES", "INICIO_DA_FASE", "FIM_DA_FASE", "DATA_ESPECIFICA", "DATA_DA_APRESENTACAO");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarEfeitosNaAnaliseDoDocumento_Consulta_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<EfeitoNaAnaliseDoDocumento>> opcoes = ListarEfeitosNaAnaliseDoDocumentoQueryHandler.Handle(new());

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("RECUSAR_DOCUMENTO", "ACEITAR_COM_AVISO");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

}
