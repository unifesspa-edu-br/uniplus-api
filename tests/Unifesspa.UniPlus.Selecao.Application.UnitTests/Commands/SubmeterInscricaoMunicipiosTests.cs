namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// Os municípios respondidos são conferidos no geo, e o nome gravado é o que ele devolve.
/// </summary>
public sealed class SubmeterInscricaoMunicipiosTests
{
    private const string Salvador = "2927408";

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> OcorrenciaMunicipal(string codigo) =>
        new(StringComparer.Ordinal) { ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", codigo)))] };

    [Fact(DisplayName = "O envio grava a naturalidade, o município de residência e a cidade do endereço com o nome do Geo, ignorando o nome do navegador")]
    public async Task Handle_MunicipiosConferidos_GravaONomeDoGeo()
    {
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BELEM-PA", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        string esperado = ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText();
        inscricao.Formulario.Preenchimento.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
        inscricao.Formulario.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(esperado);
        inscricao.Formulario.Preenchimento.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cidade").GetProperty("nome").GetString().Should().Be("Belém");
        Emitido(inscricao).TodasAsRespostas()["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
    }

    [Fact(DisplayName = "O município subitem de grupo também fica gravado com o nome do Geo")]
    public async Task Handle_MunicipioEmGrupo_GravaONomeDoGeo()
    {
        GrupoColetado locais = GrupoMunicipal();

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], [locais], Texto(), OcorrenciaMunicipal(Belem), geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Formulario.Preenchimento.Grupos["LOCAIS"].Single().Respostas["LOCAL_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }

    [Fact(DisplayName = "O município de outra UF dentro de grupo é recusado no caminho do subitem, com uma recusa só")]
    public async Task Handle_MunicipioEmGrupoDeOutraUf_RecusaNoSubitem()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], [GrupoMunicipal()], Texto(), OcorrenciaMunicipal(Salvador), geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Where(e => e.Field == "LOCAIS.local-1.LOCAL_MUNICIPIO").Should().ContainSingle()
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "O município que o Geo não conhece é recusado no campo com município não encontrado")]
    public async Task Handle_MunicipioInexistente_RecusaNoCampo()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("NATURALIDADE_MUNICIPIO", "1599999")), geo: GeoQueConhece());

        resultado.Errors.Should().ContainSingle(e => e.Field == "NATURALIDADE_MUNICIPIO")
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado);
    }

    [Fact(DisplayName = "O município de outra UF é recusado no campo com município divergente, no lugar da recusa genérica da restrição")]
    public async Task Handle_MunicipioDeOutraUf_RecusaSoComADoGeo()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("NATURALIDADE_MUNICIPIO", Salvador)), geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Where(e => e.Field == "NATURALIDADE_MUNICIPIO").Should().ContainSingle()
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "As recusas do endereço, do município e do veredito saem juntas numa resposta só")]
    public async Task Handle_RecusasDeVariosPassos_SaemJuntas()
    {
        Dictionary<string, JsonElement> respostas = Texto(("NATURALIDADE_MUNICIPIO", Salvador), ("APELIDO", ""));
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = Belem, uf = "PA" } });
        respostas.Remove("NOME_MAE");

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, respostas, geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("ENDERECO_RESIDENCIAL", InscricaoErrorCodes.EnderecoSemCep),
            ("NATURALIDADE_MUNICIPIO", ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente),
            ("APELIDO", InscricaoErrorCodes.ItemObrigatorioAusente),
        ]);
    }

    [Fact(DisplayName = "O município de um campo que a avaliação esconde não é conferido no Geo nem recusado, e não fica gravado")]
    public async Task Handle_MunicipioDeCampoOculto_NaoEhConferido()
    {
        Dictionary<string, JsonElement> respostas = Texto(
            ("NACIONALIDADE", "ESTRANGEIRO"), ("PASSAPORTE_NUMERO", "FX1234567"), ("RNM_NUMERO", "V1234567"), ("NATURALIDADE_MUNICIPIO", "1599999"));
        IConferenciaEnderecoGeo geo = GeoQueConfirmaBelem();

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await new FormularioASubmeterDeTeste([], null)
        {
            Geo = geo,
            Ausentes = ["RG_NUMERO", "RG_ORGAO_EMISSOR", "RG_UF", "RG_DATA_EMISSAO"],
        }.SubmeterAsync(respostas);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("NATURALIDADE_MUNICIPIO");
        await geo.DidNotReceive().ConferirMunicipioAsync("1599999", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O endereço sem código de cidade, ou com o nome do navegador, fica com a cidade que o Geo resolveu pelo CEP")]
    public async Task Handle_EnderecosConferidos_GravamACidadeDoGeo()
    {
        FatoColetado comercial = EnderecoComercial();
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { nome = "SSA" } });
        respostas["ENDERECO_COMERCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BEL", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([comercial], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ConferirCidade(inscricao, "ENDERECO_RESIDENCIAL");
        ConferirCidade(inscricao, "ENDERECO_COMERCIAL");

        inscricao.Formulario.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }

    [Fact(DisplayName = "As recusas do agregado, como o CPF de outra pessoa, saem junto das demais na resposta única")]
    public async Task Handle_CpfDeOutraPessoaComOutraRecusa_SaemJuntas()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("CPF", PersonasDeTeste.KevinCarvalho.Valor), ("NATURALIDADE_MUNICIPIO", Salvador)),
            geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("CPF", InscricaoErrorCodes.CpfDiferenteDoDono),
            ("NATURALIDADE_MUNICIPIO", ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente),
        ]);
    }

    [Fact(DisplayName = "Grupo com ocorrências de id repetido é recusado na forma, sem chegar à avaliação nem à conferência dos municípios")]
    public async Task Handle_GrupoComIdRepetido_RecusaSemExcecao()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], [GrupoMunicipal()], Texto(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", Belem))), new("local-1", Texto(("LOCAL_MUNICIPIO", Belem)))],
            },
            geo: GeoQueConhece());

        resultado.Errors.Should().Contain(e => e.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Corrigir a inscrição sem reenviar a naturalidade confere o município gravado de novo e regrava o nome")]
    public async Task Handle_SegundoEnvioSemANaturalidade_RegravaONome()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo());
        (await cenario.SubmeterAsync(geo: GeoQueConhece()))
            .IsSuccess.Should().BeTrue();

        InscricaoRequest correcao = CorrecaoSemNaturalidade(cenario);
        FakeConferenciaEnderecoGeo geo = GeoQueConhece();

        Result<ReciboDaInscricaoDto> corrigida = await cenario.SubmeterAsync(correcao, geo: geo);

        corrigida.IsSuccess.Should().BeTrue(corrigida.Error?.Message);
        cenario.Inscricao.Formulario.Preenchimento.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }
    private static IConferenciaEnderecoGeo GeoQueConfirmaBelem()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<CepResolvido>.Success(new CepResolvido(Belem, "Belém", "PA", "logradouro")));
        return geo;
    }

    private static FatoColetado EnderecoComercial() =>
        FatoColetadoDeTeste.Coletando("ENDERECO_COMERCIAL", 6).ComRotulo("Endereço comercial").DoTipo(TipoRenderizacao.Endereco).Construir();

    private static void ConferirCidade(Inscricao inscricao, string endereco)
    {
        JsonElement cidade = inscricao.Formulario.Preenchimento.Respostas[endereco].GetProperty("cidade");
        (cidade.GetProperty("codigoIbge").GetString(), cidade.GetProperty("nome").GetString(), cidade.GetProperty("uf").GetString())
            .Should().Be((Belem, "Belém", "PA"), endereco);
    }

    private static InscricaoRequest CorrecaoSemNaturalidade(CenarioDoEnvioDeTeste cenario)
    {
        InscricaoRequest comNovoApelido = RequestComApelidoETermoAceito(cenario.Cpf, cenario.Termo, cenario.Configuracao, apelido: "Nandinha");
        return comNovoApelido with
        {
            Respostas = comNovoApelido.Respostas!.Where(static r => r.Key != "NATURALIDADE_MUNICIPIO").ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
        };
    }

}
