namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ChavesDasBancasNoDocumentoTests
{
    private static readonly LeituraDoDocumentoDaConfiguracao Leitura = new(new EsquemaDoDocumentoDaConfiguracao());

    [Theory]
    [InlineData("", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente)]
    [InlineData("\"chave\": \"banca-invalida\",", DocumentoDaConfiguracaoErrorCodes.ValorInvalido)]
    public void Ler_ChaveDaBancaAusenteOuMalformada_RecusaNoPonteiro(string chave, string codigo)
    {
        Result<DocumentoLido> resultado = Leitura.Ler($$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "cronograma": { "fases": [ { "chave": "fase", "tipoFaseCodigo": "INSCRICAO",
                "bancasRequeridas": [ { {{chave}} "tipoBancaCodigo": "BANCA_ANALISE" } ] } ] } }
            """);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/0/bancasRequeridas/0/chave", codigo));
    }

    [Fact]
    public void Ler_ChaveRepetidaEntreBancasDeFaseEEtapa_RecusaNoPonteiroDaPosterior()
    {
        Result<DocumentoLido> resultado = Leitura.Ler("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "cronograma": { "fases": [ { "chave": "fase", "tipoFaseCodigo": "INSCRICAO",
                "bancasRequeridas": [ { "chave": "banca_analise", "tipoBancaCodigo": "BANCA_ANALISE" } ] } ] },
              "etapas": [ { "chave": "etapa", "bancas": [ { "chave": "banca_analise", "tipoBancaCodigo": "BANCA_ANALISE" } ] } ] }
            """);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/0/bancas/0/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida));
    }
}
