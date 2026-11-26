namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial do documento é copiada como foi escrita, sem o nome do município, que é só de leitura, e passa pela leitura do
/// domínio: cada causa aponta o campo do item pela posição em que o município veio no documento enviado.
/// </summary>
public sealed class LeituraDaProvaPresencialDoDocumentoTests
{
    private const int Limite = 100;
    private const string Marabá = "1504208";
    private const string Xinguara = "1508159";

    private static readonly MunicipiosConferidosNoGeo GeoConhecendoMarabá = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
    {
        [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
    });

    private static ProvaPresencialNoDocumento? Bloco(string documento) =>
        DeclaracoesDoDocumentoLidas.De(JsonNode.Parse(documento)!.AsObject()).ProvaPresencial;

    private static ProvaPresencialLida Lida(string documento, int limite = Limite) => ProvaPresencialLida.Ler(Bloco(documento), limite);

    private static (string? Ponteiro, string Codigo)[] Causas(ProvaPresencialLida lida, MunicipiosConferidosNoGeo geo) =>
        [.. lida.Causas(geo).Select(static causa => (causa.Field, causa.Error.Code))];

    [Fact(DisplayName = "O documento sem o bloco da prova presencial fica sem bloco, e a causa é o processo por declarar")]
    public void BlocoAusente_FicaSemBlocoEPorDeclarar()
    {
        Bloco("{}").Should().BeNull();
        Causas(Lida("{}"), GeoConhecendoMarabá).Should().Equal(("/provaPresencial", ProvaPresencialErrorCodes.PorDeclarar));
    }

    [Fact(DisplayName = "O processo sem prova presencial e sem município, com a lista ausente ou vazia, não tem causa")]
    public void SemProvaESemMunicipio_NaoTemCausa()
    {
        Causas(Lida("""{ "provaPresencial": { "temProvaPresencial": false } }"""), MunicipiosConferidosNoGeo.Nenhum).Should().BeEmpty();
        Causas(Lida("""{ "provaPresencial": { "temProvaPresencial": false, "municipios": [] } }"""), MunicipiosConferidosNoGeo.Nenhum).Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo com prova presencial e sem município aponta a lista de municípios")]
    public void ComProvaESemMunicipio_ApontaALista()
    {
        Bloco("""{ "provaPresencial": { "temProvaPresencial": true } }""")!.Municipios.Should().BeEmpty();
        Causas(Lida("""{ "provaPresencial": { "temProvaPresencial": true, "municipios": [] } }"""), MunicipiosConferidosNoGeo.Nenhum)
            .Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.SemMunicipio));
    }

    [Fact(DisplayName = "O bloco copia a prova presencial e cada município como escritos, na ordem enviada, e ignora o nome do município")]
    public void Bloco_CopiaOsMunicipiosComoEscritosSemONome()
    {
        ProvaPresencialNoDocumento bloco = Bloco("""
            { "provaPresencial": { "temProvaPresencial": true, "municipios": [
              { "codigoIbge": "1508159", "uf": "PA", "nome": "Nome que o Geo não confirmou" },
              { "codigoIbge": "15042", "uf": "pa" } ] } }
            """)!;

        bloco.TemProvaPresencial.Should().BeTrue();
        bloco.Municipios.Should().Equal(new MunicipioDeProvaInformado(Xinguara, "PA"), new MunicipioDeProvaInformado("15042", "pa"));
    }

    [Fact(DisplayName = "O bloco sem o campo de prova presencial copia o valor de partida, que é sem prova")]
    public void BlocoSemOCampoDaProva_CopiaSemProva() =>
        Bloco("""{ "provaPresencial": { } }""")!.TemProvaPresencial.Should().BeFalse();

    [Fact(DisplayName = "O município com código e UF coerentes que o Geo conhece não tem causa")]
    public void MunicipioConhecidoNoGeo_NaoTemCausa() =>
        Causas(
            Lida("""{ "provaPresencial": { "temProvaPresencial": true, "municipios": [ { "codigoIbge": "1504208", "uf": "PA" } ] } }"""),
            GeoConhecendoMarabá).Should().BeEmpty();

    [Fact(DisplayName = "O município que o Geo não conhece aponta o código dele pela posição enviada")]
    public void MunicipioQueOGeoNaoConhece_ApontaOCodigo() =>
        Causas(
            Lida("""
                { "provaPresencial": { "temProvaPresencial": true, "municipios": [
                  { "codigoIbge": "1504208", "uf": "PA" }, { "codigoIbge": "1508159", "uf": "PA" } ] } }
                """),
            GeoConhecendoMarabá).Should().Equal(("/provaPresencial/municipios/1/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));

    [Fact(DisplayName = "O município repetido aponta o código na posição da repetição, e o Geo não é consultado")]
    public void MunicipioRepetido_ApontaARepeticao() =>
        Causas(
            Lida("""
                { "provaPresencial": { "temProvaPresencial": true, "municipios": [
                  { "codigoIbge": "1504208", "uf": "PA" }, { "codigoIbge": "1504208", "uf": "PA" } ] } }
                """),
            MunicipiosConferidosNoGeo.Nenhum).Should().Equal(("/provaPresencial/municipios/1/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido));

    [Fact(DisplayName = "A UF que não é a do prefixo do código aponta o campo da UF do município")]
    public void UfIncoerente_ApontaOCampoDaUf() =>
        Causas(
            Lida("""{ "provaPresencial": { "temProvaPresencial": true, "municipios": [ { "codigoIbge": "1504208", "uf": "SP" } ] } }"""),
            GeoConhecendoMarabá).Should().Equal(("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente));

    [Fact(DisplayName = "O código fora da forma e o município sem código nem UF apontam cada campo, e nada vai ao Geo")]
    public void CodigoForaDaFormaEMunicipioSemCampos_ApontamCadaCampo() =>
        Causas(
            Lida("""
                { "provaPresencial": { "temProvaPresencial": true, "municipios": [
                  { "codigoIbge": "15042", "uf": "PA" }, { } ] } }
                """),
            GeoConhecendoMarabá).Should().Equal(
                ("/provaPresencial/municipios/0/codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido),
                ("/provaPresencial/municipios/1/codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio),
                ("/provaPresencial/municipios/1/uf", CidadeReferenciaErrorCodes.UfObrigatoria));

    [Fact(DisplayName = "A lista acima do limite configurado aponta a lista por último, depois das causas de cada município")]
    public void ListaAcimaDoLimite_ApontaALista() =>
        Causas(
            Lida(
                """
                { "provaPresencial": { "temProvaPresencial": true, "municipios": [
                  { "codigoIbge": "1504208", "uf": "SP" }, { "codigoIbge": "1508159", "uf": "PA" }, { "codigoIbge": "1500107", "uf": "PA" } ] } }
                """,
                limite: 2),
            MunicipiosConferidosNoGeo.Nenhum).Should().Equal(
                ("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente),
                ("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite));

    [Fact(DisplayName = "O processo sem prova presencial que declara município é recusado na lista")]
    public void SemProvaComMunicipio_RecusaALista() =>
        Causas(
            Lida("""{ "provaPresencial": { "temProvaPresencial": false, "municipios": [ { "codigoIbge": "1504208", "uf": "PA" } ] } }"""),
            GeoConhecendoMarabá).Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial));

    [Fact(DisplayName = "Nenhuma causa repete o texto que a pessoa digitou no código nem na UF")]
    public void Causas_NaoRepetemOTextoDigitado()
    {
        ProvaPresencialLida lida = Lida("""
            { "provaPresencial": { "temProvaPresencial": true, "municipios": [
              { "codigoIbge": "TEXTO-DIGITADO", "uf": "UF-DIGITADA" }, { "codigoIbge": "1504208", "uf": "UF-DIGITADA" } ] } }
            """);

        FieldError[] causas = [.. lida.Causas(GeoConhecendoMarabá)];

        causas.Should().HaveCount(2);
        causas.Select(static causa => causa.Error.Message).Should().OnlyContain(static mensagem =>
            !mensagem.Contains("TEXTO-DIGITADO", StringComparison.Ordinal) && !mensagem.Contains("UF-DIGITADA", StringComparison.Ordinal));
    }
}
