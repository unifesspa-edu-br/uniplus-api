namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>A leitura inteira (limites, normalização e esquema) sobre o esquema embutido e os exemplos publicados.</summary>
public sealed class LeituraDoDocumentoComEsquemaRealTests
{
    private const string Cabecalho = "\"formato\": \"uniplus.selecao.configuracao\", \"versaoFormato\": 1";

    private static readonly LeituraDoDocumentoDaConfiguracao Leitura = new(new EsquemaDoDocumentoDaConfiguracao());

    private static Result<DocumentoLido> Ler(string resto) => Leitura.Ler($"{{ {Cabecalho}, {resto} }}");

    [Theory(DisplayName = "O exemplo publicado passa pela leitura inteira, sem os campos só de leitura")]
    [MemberData(nameof(ExemplosDoContrato.Arquivos), MemberType = typeof(ExemplosDoContrato))]
    public void Exemplo_PassaSemOsCamposSomenteLeitura(string arquivo)
    {
        Result<DocumentoLido> resultado = Leitura.Ler(ExemplosDoContrato.Ler(arquivo));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Documento.ContainsKey("id").Should().BeFalse();
        resultado.Value.Documento.ContainsKey("publicadoEm").Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura projeta o nome e o código da unidade do documento")]
    public void Leitura_ProjetaNomeEUnidade()
    {
        Result<DocumentoLido> resultado = Leitura.Ler(ExemplosDoContrato.Ler("inscricao-pelo-sistema.json"));

        resultado.Value!.Nome.Should().Be("Processo Seletivo de Exemplo 2027");
        resultado.Value.UnidadeAdministradoraCodigo.Should().Be("CEPS");
    }

    [Fact(DisplayName = "A parte que o esquema exige com chave e veio sem ela é recusada no ponteiro da chave que falta, sem receber chave")]
    public void ParteSemChave_ERecusadaNoPonteiroDaChave()
    {
        Result<DocumentoLido> resultado = Ler("""
            "etapas": [
              { "chave": "prova", "itensDeAvaliacao": [ { "chave": "questao" }, {} ], "bancas": [ {} ] },
              { "nome": "Segunda etapa" }
            ]
            """);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/0/itensDeAvaliacao/1/chave", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente),
            ("/etapas/0/bancas/0/chave", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente),
            ("/etapas/1/chave", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente));
    }

    [Fact(DisplayName = "Reordenar as partes com as suas chaves conserva a chave de cada parte, e o item repete a chave em etapas diferentes")]
    public void Reordenar_ConservaAChaveDeCadaParte()
    {
        Result<DocumentoLido> original = Ler($"\"etapas\": [ {Etapa("prova", "Prova")}, {Etapa("redacao", "Redação")} ]");
        Result<DocumentoLido> reordenado = Ler($"\"etapas\": [ {Etapa("redacao", "Redação")}, {Etapa("prova", "Prova")} ]");

        original.IsSuccess.Should().BeTrue();
        reordenado.IsSuccess.Should().BeTrue();
        ChaveENome(original).Should().Equal(("prova", "Prova"), ("redacao", "Redação"));
        ChaveENome(reordenado).Should().Equal(("redacao", "Redação"), ("prova", "Prova"));
    }

    [Fact(DisplayName = "A parte devolvida sem chave depois de reordenar recusa o documento inteiro: nenhuma chave é religada à parte")]
    public void ParteSemChaveAoReordenar_RecusaODocumento()
    {
        Result<DocumentoLido> resultado = Ler("""
            "etapas": [
              { "nome": "Redação" },
              { "chave": "prova", "nome": "Prova" }
            ]
            """);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("/etapas/0/chave");
    }

    [Fact(DisplayName = "A chave repetida entre etapas é recusada na etapa posterior, e o item repetido em etapas diferentes não é")]
    public void ChaveRepetida_RecusadaSoNoMesmoEscopo()
    {
        Result<DocumentoLido> resultado = Ler("""
            "etapas": [
              { "chave": "prova", "itensDeAvaliacao": [ { "chave": "questao" } ] },
              { "chave": "prova", "itensDeAvaliacao": [ { "chave": "questao" } ] }
            ]
            """);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/1/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida));
    }

    [Fact(DisplayName = "A chave repetida em três etapas é recusada na segunda e na terceira, na posição da lista enviada")]
    public void ChaveRepetidaEmTresEtapas_RecusadaNasPosteriores()
    {
        Result<DocumentoLido> resultado = Ler("""
            "etapas": [ { "chave": "prova" }, { "chave": "redacao" }, { "chave": "prova" }, { "chave": "prova" } ]
            """);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/2/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida),
            ("/etapas/3/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida));
    }

    [Fact(DisplayName = "As causas de forma saem juntas, na ordem do documento, sem repetir o texto digitado")]
    public void CausasDeForma_SaemJuntasNaOrdemDoDocumentoSemOTextoDigitado()
    {
        Result<DocumentoLido> resultado = Leitura.Ler("""
            { "formato": "SEGREDO-1", "versaoFormato": 1, "etapas": [ { "nome": 3, "invento": 1, "chave": "x" }, { "chave": "x" } ],
              "opcoesDeCurso": { "numero": 9 } }
            """);

        resultado.Errors.Select(static causa => causa.Field).Should().Equal(
            "/formato", "/etapas/0/nome", "/etapas/0/invento", "/etapas/1/chave", "/opcoesDeCurso/numero");
        resultado.Errors.Select(static causa => causa.Error.Message).Should().NotContain(static mensagem => mensagem.Contains("SEGREDO", StringComparison.Ordinal));
    }

    private static string Etapa(string chave, string nome) =>
        $$"""{ "chave": "{{chave}}", "nome": "{{nome}}", "itensDeAvaliacao": [ { "chave": "questao" } ] }""";

    private static IEnumerable<(string? Chave, string? Nome)> ChaveENome(Result<DocumentoLido> lido) =>
        lido.Value!.Documento["etapas"]!.AsArray().Select(static etapa => (etapa!["chave"]?.GetValue<string>(), etapa["nome"]?.GetValue<string>()));
}
