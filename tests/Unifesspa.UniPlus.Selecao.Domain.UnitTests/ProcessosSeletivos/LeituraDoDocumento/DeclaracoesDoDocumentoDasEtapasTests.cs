namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class DeclaracoesDoDocumentoDasEtapasTests
{
    [Fact(DisplayName = "O documento sem o bloco das etapas e sem o da classificação tem a lista vazia e a classificação nula")]
    public void Documento_SemEtapasNemClassificacao_TemListaVaziaEClassificacaoNula()
    {
        DeclaracoesDoDocumento documento = Documento();

        documento.Etapas.Should().BeEmpty();
        documento.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "A classificação sem regras de eliminação nem critérios de desempate tem as duas listas vazias")]
    public void Classificacao_SemListas_TemListasVazias()
    {
        ClassificacaoNoDocumento classificacao = new(null, null, null, null, null, null, null);

        classificacao.RegrasEliminacao.Should().BeEmpty();
        classificacao.CriteriosDeDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa sem publicações, recursos nem tipos de decisão tem listas vazias, sem promessa de parecer e com os tipos de decisão ausentes")]
    public void Etapa_SemPublicacoesNemRecursos_TemListasVazias()
    {
        EtapaNoDocumento etapa = new(
            0, "etapa_um", new(null, null, null, null, null, null), new(null, null, null, null, [], []), new(null, null), []);

        etapa.PublicacoesPrevistas.Should().BeEmpty();
        etapa.Recursos.Should().BeEmpty();
        etapa.EmiteParecerIndividual.Should().BeFalse();
        etapa.TiposDeDecisao.Should().BeNull();
    }
}
