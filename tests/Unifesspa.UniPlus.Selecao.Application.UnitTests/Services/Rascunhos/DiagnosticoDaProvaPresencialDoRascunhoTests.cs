namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial do documento é lida como declaração do domínio e conferida no Geo ao salvar: todos os avisos saem juntos, no
/// ponteiro do item pela posição enviada, sem repetir o que foi digitado, e o Geo só é consultado quando a leitura aceitou a declaração.
/// </summary>
public sealed class DiagnosticoDaProvaPresencialDoRascunhoTests
{
    private const string Maraba = "1504208";
    private const string Belem = "1501402";
    private const string DesconhecidoDoGeo = "1599999";

    private readonly CenarioDoRascunho _cenario = new CenarioDoRascunho().GeoConhece(Maraba, "Marabá").GeoConhece(Belem, "Belém");

    private Task<IReadOnlyList<FieldError>> DiagnosticarOBlocoAsync(string provaPresencial) =>
        _cenario.DiagnosticarOSalvamentoAsync(CenarioDoRascunho.DocumentoValido(provaPresencial: provaPresencial));

    private Task<IReadOnlyList<FieldError>> DiagnosticarOsMunicipiosAsync(params string[] codigosDosMunicipios) =>
        DiagnosticarOBlocoAsync(CenarioDoRascunho.ProvaPresencialNoPara(codigosDosMunicipios));

    private static (string? Ponteiro, string Codigo)[] Pares(IReadOnlyList<FieldError> causas) =>
        [.. causas.Select(static causa => (causa.Field, causa.Error.Code))];

    private async Task GeoNaoFoiConsultado() =>
        await _cenario.Geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);

    [Fact(DisplayName = "O processo que não declarou a prova presencial recebe só o aviso de por declarar, sem consultar o Geo")]
    public async Task SemOBloco_ApontaPorDeclarar()
    {
        IReadOnlyList<FieldError> causas = await _cenario.DiagnosticarOSalvamentoAsync(
            """{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "PSR 2028", "unidadeAdministradoraCodigo": "CEPS" } }""");

        Pares(causas).Should().Equal(("/provaPresencial", ProvaPresencialErrorCodes.PorDeclarar));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "A prova presencial sem município recebe o aviso de falta de município no campo dos municípios")]
    public async Task ComProvaESemMunicipio_ApontaSemMunicipio()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync();

        Pares(causas).Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.SemMunicipio));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "O processo sem prova presencial e sem município não recebe aviso")]
    public async Task SemProvaESemMunicipio_NaoTemAviso()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOBlocoAsync(CenarioDoRascunho.SemProvaPresencial);

        causas.Should().BeEmpty();
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "O município repetido é apontado na segunda ocorrência, e a declaração recusada na leitura não vai ao Geo")]
    public async Task MunicipioRepetido_ApontaASegundaOcorrencia()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(Maraba, Maraba);

        Pares(causas).Should().Equal(("/provaPresencial/municipios/1/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "A UF que não é a do prefixo do código é apontada no campo da UF do município")]
    public async Task UfQueNaoEDoPrefixo_ApontaAUf()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOBlocoAsync(
            """ "provaPresencial": { "temProvaPresencial": true, "municipios": [ { "codigoIbge": "1504208", "uf": "PB" } ] } """);

        Pares(causas).Should().Equal(("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "O código sem a forma de município é apontado sem repetir o texto digitado")]
    public async Task CodigoSemForma_ApontaSemRepetirOTexto()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync("15A4208");

        Pares(causas).Should().Equal(("/provaPresencial/municipios/0/codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido));
        causas.Single().Error.Message.Should().NotContain("15A4208");
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "O município que o Geo não conhece recebe um aviso cada, na posição declarada, e os conhecidos não")]
    public async Task MunicipioDesconhecidoDoGeo_UmAvisoPorMunicipio()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(DesconhecidoDoGeo, Maraba, "1599998");

        Pares(causas).Should().Equal(
            ("/provaPresencial/municipios/0/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo),
            ("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
        await _cenario.Geo.Received(3).ConferirMunicipioAsync(Arg.Any<string>(), "PA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista acima do limite configurado é apontada no campo dos municípios, e nenhum município vai ao Geo")]
    public async Task AcimaDoLimite_ApontaOLimiteSemConsultarOGeo()
    {
        _cenario.LimiteDeMunicipios = 2;

        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(Maraba, Belem, DesconhecidoDoGeo);

        Pares(causas).Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "Com a lista acima do limite, o aviso da lista vem antes dos avisos de cada município, na posição do documento")]
    public async Task AcimaDoLimiteComMunicipioRepetido_AvisoDaListaVemAntes()
    {
        _cenario.LimiteDeMunicipios = 2;

        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(Maraba, Belem, Maraba);

        Pares(causas).Should().Equal(
            ("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite),
            ("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "A lista que cabe no limite configurado é conferida no Geo e não tem aviso do limite")]
    public async Task NoLimite_ConfereNoGeo()
    {
        _cenario.LimiteDeMunicipios = 2;

        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(Maraba, Belem);

        causas.Should().BeEmpty();
        await _cenario.Geo.Received(2).ConferirMunicipioAsync(Arg.Any<string>(), "PA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com o Geo fora do ar o aviso é um só, no campo dos municípios, e a consulta pára no primeiro município")]
    public async Task GeoForaDoAr_UmSoAviso()
    {
        _cenario.GeoForaDoAr();

        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(Maraba, Belem);

        Pares(causas).Should().Equal(("/provaPresencial/municipios", ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel));
        await _cenario.Geo.Received(1).ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com o Geo fora do ar, o processo por declarar recebe só o aviso de por declarar")]
    public async Task GeoForaDoAr_ProcessoPorDeclarar_NaoTemAvisoDoGeo()
    {
        _cenario.GeoForaDoAr();

        IReadOnlyList<FieldError> causas = await _cenario.DiagnosticarOSalvamentoAsync(
            """{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "unidadeAdministradoraCodigo": "CEPS" } }""");

        Pares(causas).Should().Equal(("/provaPresencial", ProvaPresencialErrorCodes.PorDeclarar));
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "A declaração recusada na leitura traz só as causas da leitura: não é processo por declarar")]
    public async Task DeclaracaoRecusada_NaoEPorDeclarar()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOsMunicipiosAsync(DesconhecidoDoGeo, "15A4208");

        causas.Select(static causa => causa.Error.Code).Should().Equal(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
        await GeoNaoFoiConsultado();
    }

    [Fact(DisplayName = "O processo sem prova presencial que declara município é apontado pela leitura, sem ser por declarar")]
    public async Task SemProvaComMunicipio_ApontaPelaLeitura()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOBlocoAsync(
            """ "provaPresencial": { "temProvaPresencial": false, "municipios": [ { "codigoIbge": "1504208", "uf": "PA" } ] } """);

        Pares(causas).Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial));
    }

    [Fact(DisplayName = "Todas as causas de forma saem juntas, cada município pela posição enviada")]
    public async Task CausasDeForma_SaemJuntasNaPosicaoEnviada()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarOBlocoAsync("""
            "provaPresencial": { "temProvaPresencial": true, "municipios": [
              { "codigoIbge": "1504208", "uf": "PB" },
              { "codigoIbge": "1501402", "uf": "PA" },
              { "codigoIbge": "1501402", "uf": "PA" } ] }
            """);

        Pares(causas).Should().Equal(
            ("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente),
            ("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido));
    }

    [Fact(DisplayName = "Os avisos da prova presencial se juntam aos dos outros blocos na ordem do documento")]
    public async Task AvisosDaProvaPresencial_SeJuntamAosDosOutrosBlocos()
    {
        IReadOnlyList<FieldError> causas = await _cenario.DiagnosticarOSalvamentoAsync("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "unidadeAdministradoraCodigo": "CEPS" },
              "provaPresencial": { "temProvaPresencial": true, "municipios": [] },
              "manifestacaoDeInteresse": "VALOR-DIGITADO" }
            """);

        causas.Select(static causa => causa.Field).Should().Equal("/provaPresencial/municipios", "/manifestacaoDeInteresse");
    }
}
