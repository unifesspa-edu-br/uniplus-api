namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class VocabulariosDaComprovacaoTests
{
    [Fact]
    public void ListarFormatosPermitidos_Consulta_DevolveCodigosNaOrdem()
    {
        IReadOnlyList<CodigoComRotulo<FormatoPermitido>> opcoes = ListarFormatosPermitidosQueryHandler.Handle(new());
        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("PDF", "JPEG", "PNG");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarCriteriosDeDistincaoDasApresentacoes_Consulta_DevolveCodigosNaOrdem()
    {
        IReadOnlyList<CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>> opcoes = ListarCriteriosDeDistincaoDasApresentacoesQueryHandler.Handle(new());
        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("COMPETENCIA_MENSAL", "EXERCICIO_ANUAL", "OCORRENCIA");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }

    [Fact]
    public void ListarConsequenciasDoDescumprimento_Consulta_DevolveCodigosNaOrdem()
    {
        IReadOnlyList<CodigoComRotulo<ConsequenciaDoDescumprimento>> opcoes = ListarConsequenciasDoDescumprimentoQueryHandler.Handle(new());
        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("ELIMINA_DO_PROCESSO", "RETIRA_A_MODALIDADE", "RETIRA_O_ACRESCIMO");
        opcoes.Should().OnlyContain(static opcao => !string.IsNullOrWhiteSpace(opcao.Rotulo));
    }
}
