namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ProjecaoDaExportacaoDeRecursosTests
{
    [Theory]
    [InlineData("cronograma", """{"tiposDeAto":[{"codigo":"RESULTADO","nome":"Resultado"}],"cronogramaFases":{"fases":[{"chave":"fase","codigo":"INSCRICAO","bancasRequeridas":[{"id":"11111111-1111-1111-1111-111111111111","chave":"banca","codigo":"BANCA","candidatosAtendidos":[{"codigo":"BBB","nome":"Segundo"},{"codigo":"AAA","nome":"Primeiro"}],"regraDeDeliberacao":{"modo":"atoIndividual","integrantes":1,"suplentes":0}}],"publicacoesPrevistas":[{"id":"produto","atoCodigo":"RESULTADO","papel":"PRELIMINAR"}],"regraRecurso":{"regra":{"codigo":"PRAZO","versao":"v1","hash":"hash"},"publicacaoDeReferenciaId":"produto","parametros":{"prazoValor":"2.0000","prazoUnidade":"diasUteis","admiteInstanciaSuperior":false,"efeitoSuspensivo":"semEfeitoSuspensivo","guardaDaVaga":"naoGuarda"},"procedimentos":[]}}]}}""", """{"fases":[{"chave":"fase","tipoFaseCodigo":"INSCRICAO","publicacoesPrevistas":[{"atoCodigo":"RESULTADO","atoNome":"Resultado","papel":"PRELIMINAR"}],"bancasRequeridas":[{"id":"11111111-1111-1111-1111-111111111111","chave":"banca","tipoBancaCodigo":"BANCA","categoriasDocumentoCodigos":["BBB","AAA"],"categoriasDocumentoNomes":["Segundo","Primeiro"],"regraDeDeliberacao":{"modo":"atoIndividual","integrantes":1,"suplentes":0}}],"regraRecurso":{"regraCodigo":"PRAZO","regraVersao":"v1","prazoValor":2.0000,"prazoUnidade":"diasUteis","publicacaoDeReferenciaAtoCodigo":"RESULTADO","admiteInstanciaSuperior":false,"efeitoSuspensivo":"semEfeitoSuspensivo","guardaDaVaga":"naoGuarda","procedimentosDeRecurso":[]}}]}""")]
    [InlineData("etapas", """{"tiposDeAto":[{"codigo":"RESULTADO","nome":"Resultado"}],"etapas":[{"chave":"etapa","carater":"Ambas","itensDeAvaliacao":[{"id":"11111111-1111-1111-1111-111111111111","chave":"item"}],"bancas":[{"chave":"banca","codigo":"BANCA","candidatosAtendidos":[{"codigo":"AAA","nome":"Primeiro"}]}],"publicacoesPrevistas":[{"id":"produto","atoCodigo":"RESULTADO","papel":"PRELIMINAR"}],"recursos":[{"eventoInicialDoPrazo":"AtoPublicado","regra":{"codigo":"PRAZO","versao":"v1"},"publicacaoDeReferenciaId":"produto","parametros":{"prazoValor":"1.0000"},"procedimentos":[{"tipoDeDecisao":"NOTA_DA_ETAPA","alvo":{"tipo":"item_avaliacao","valor":"11111111-1111-1111-1111-111111111111"},"regra":{"codigo":"PROCEDIMENTO-RECURSO-RECORRECAO","versao":"v1","hash":"hash"}}]}]}]}""", """[{"chave":"etapa","carater":"ambas","itensDeAvaliacao":[{"id":"11111111-1111-1111-1111-111111111111","chave":"item"}],"publicacoesPrevistas":[{"atoCodigo":"RESULTADO","atoNome":"Resultado","papel":"PRELIMINAR"}],"bancas":[{"chave":"banca","tipoBancaCodigo":"BANCA"}],"recursos":[{"eventoInicialDoPrazo":"atoPublicado","regraCodigo":"PRAZO","regraVersao":"v1","prazoValor":1.0000,"publicacaoDeReferenciaAtoCodigo":"RESULTADO","procedimentosDeRecurso":[{"tipoDeDecisao":"NOTA_DA_ETAPA","alvo":{"itemDeAvaliacaoChave":"item"},"regra":{"codigo":"PROCEDIMENTO-RECURSO-RECORRECAO","versao":"v1"}}]}]}]""")]
    public void Projetar_RecursosEPublicacoes_UsaOsDadosDoCongelado(string parte, string congelado, string esperado)
    {
        JsonObject documento = new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao())
            .Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        documento[parte]!.ToJsonString().Should().Be(JsonNode.Parse(esperado)!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"cronogramaFases":{"fases":[{"regraRecurso":{"publicacaoDeReferenciaId":"ausente"}}]}}""")]
    [InlineData("""{"etapas":[{"publicacoesPrevistas":[{"atoCodigo":"AUSENTE"}]}]}""")]
    [InlineData("""{"etapas":[{"recursos":[{"procedimentos":[{"alvo":{"tipo":"item_avaliacao","valor":"ausente"}}]}]}]}""")]
    public void Projetar_ReferenciaNaoEncontrada_LancaExcecao(string congelado)
    {
        Action projetar = () => new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao())
            .Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        projetar.Should().Throw<InvalidOperationException>();
    }
}
