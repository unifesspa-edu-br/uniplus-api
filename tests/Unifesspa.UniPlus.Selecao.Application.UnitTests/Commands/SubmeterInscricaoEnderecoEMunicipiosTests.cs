namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.SubmeterDeTeste;

/// <summary>
/// O envio e a conferência do endereço e dos municípios no Geo, inclusive com o Geo fora do ar e as recusas reunidas numa resposta só.
/// </summary>
public sealed class SubmeterInscricaoEnderecoEMunicipiosTests
{
    [Fact(DisplayName = "CEP que o Geo não encontra recusa o envio no campo do endereço, sem gravar")]
    public async Task Handle_CepNaoEncontrado_RecusaNoCampoDoEndereco()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "sem cobertura")));

        (Result<ReciboDaInscricaoDto> resultado, IUnitOfWork unitOfWork) = await SubmeterComEnderecoAsync(geo);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CEP geral oficial sem logradouro e bairro recusa o envio no campo do endereço")]
    public async Task Handle_CepGeralSemLogradouro_Recusa()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1501402", "Marabá", "PA", "cep-geral")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo);

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            [InscricaoErrorCodes.EnderecoLogradouroObrigatorio, InscricaoErrorCodes.EnderecoBairroObrigatorio]);
    }

    [Fact(DisplayName = "Geo fora do ar recusa o envio como indisponível, e não como CEP não encontrado")]
    public async Task Handle_GeoIndisponivel_RecusaComoIndisponivel()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "fora do ar")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo);

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Envio sem CEP e com UF divergente recebe a orientação do CEP, e não a divergência de município")]
    public async Task Handle_EnderecoSemCepComUfDivergente_RecusaPorFaltaDeCep()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente, "UF divergente")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo, cep: null);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep);
        resultado.Errors.Should().NotContain(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "O envio grava a naturalidade, o município de residência e a cidade do endereço com o nome do Geo, ignorando o nome do navegador")]
    public async Task Handle_MunicipiosConferidos_GravaONomeDoGeo()
    {
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BELEM-PA", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        string esperado = ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText();
        inscricao.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
        inscricao.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(esperado);
        inscricao.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cidade").GetProperty("nome").GetString().Should().Be("Belém");
        Emitido(inscricao).TodasAsRespostas()["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
    }

    [Fact(DisplayName = "O município subitem de grupo também fica gravado com o nome do Geo")]
    public async Task Handle_MunicipioEmGrupo_GravaONomeDoGeo()
    {
        GrupoColetado locais = GrupoMunicipal();

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], [locais], Texto(), OcorrenciaMunicipal(Belem), geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Grupos["LOCAIS"].Single().Respostas["LOCAL_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
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

    [Fact(DisplayName = "Com o Geo fora do ar, a recusa que não depende dele sai como 422 e não como indisponibilidade")]
    public async Task Handle_GeoForaDoArComRecusaIndependente_Recusa422()
    {
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = Belem, uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync([], null, respostas, geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep);
        resultado.Error?.Code.Should().NotBe(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Com o Geo fora do ar e nenhuma outra recusa, o envio inteiro é recusado como indisponível e nada é gravado")]
    public async Task Handle_GeoForaDoArSemOutraRecusa_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "O município de um campo que a avaliação esconde não é conferido no Geo nem recusado, e não fica gravado")]
    public async Task Handle_MunicipioDeCampoOculto_NaoEhConferido()
    {
        Dictionary<string, JsonElement> respostas = Texto(
            ("NACIONALIDADE", "ESTRANGEIRO"), ("PASSAPORTE_NUMERO", "FX1234567"), ("RNM_NUMERO", "V1234567"), ("NATURALIDADE_MUNICIPIO", "1599999"));
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<CepResolvido>.Success(new CepResolvido(Belem, "Belém", "PA", "logradouro")));

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], null, respostas, geo: geo, remover: ["RG_NUMERO", "RG_ORGAO_EMISSOR", "RG_UF", "RG_DATA_EMISSAO"]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Respostas.Should().NotContainKey("NATURALIDADE_MUNICIPIO");
        await geo.DidNotReceive().ConferirMunicipioAsync("1599999", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O endereço sem código de cidade, ou com o nome do navegador, fica com a cidade que o Geo resolveu pelo CEP")]
    public async Task Handle_EnderecosConferidos_GravamACidadeDoGeo()
    {
        FatoColetado comercial = FatoColetado.Criar(
            "ENDERECO_COMERCIAL", 6, "Endereço comercial", TipoRenderizacao.Endereco, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { nome = "SSA" } });
        respostas["ENDERECO_COMERCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BEL", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([comercial], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        foreach (string endereco in new[] { "ENDERECO_RESIDENCIAL", "ENDERECO_COMERCIAL" })
        {
            JsonElement cidade = inscricao.Respostas[endereco].GetProperty("cidade");
            (cidade.GetProperty("codigoIbge").GetString(), cidade.GetProperty("nome").GetString(), cidade.GetProperty("uf").GetString())
                .Should().Be((Belem, "Belém", "PA"), endereco);
        }

        inscricao.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }

    [Fact(DisplayName = "Com o Geo fora do ar, o CPF de outra pessoa é recusado como 422 e não vira indisponibilidade")]
    public async Task Handle_GeoForaDoArComCpfDeOutraPessoa_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("CPF", PersonasDeTeste.KevinCarvalho.Valor)), geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().Contain(e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
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

    [Fact(DisplayName = "Obrigatoriedade que depende da residência com o Geo fora do ar: o envio é recusado como indisponível, e não como campo obrigatório")]
    public async Task Handle_ObrigatoriedadeDependenteDaResidenciaComGeoForaDoAr_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [ObrigatorioSoParaQuemMoraEm("9999999")], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "Campo obrigatório em definitivo e vazio, com o Geo fora do ar, é recusa que conta: 422 com a recusa do campo")]
    public async Task Handle_ObrigatorioEmDefinitivoVazioComGeoForaDoAr_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [ObrigatorioSoParaQuemMoraEm("9999999")], null, Texto(), geo: GeoQueConhece().Indisponivel(), remover: ["NOME_MAE"]);

        resultado.Errors.Should().ContainSingle(e => e.Field == "NOME_MAE" && e.Error.Code == InscricaoErrorCodes.ItemObrigatorioAusente);
        resultado.Errors.Should().NotContain(e => e.Field == "OUTRA_REGIAO");
    }

    [Fact(DisplayName = "Campo obrigatório em definitivo e vazio conta como recusa mesmo quando uma restrição dele cita a residência que o Geo deixou indeterminada")]
    public async Task Handle_ObrigatorioVazioComRestricaoSobreAResidencia_ContaComoRecusa()
    {
        FatoColetado doEstado = FatoColetado.Criar(
            "LOCAL_NO_ESTADO", 8, "Local no estado", TipoRenderizacao.Municipio, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao,
            restricoes: [new MunicipiosDaUf("UF_RESIDENCIA")]).Value!;

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync([doEstado], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "LOCAL_NO_ESTADO" && e.Error.Code == InscricaoErrorCodes.ItemObrigatorioAusente);
    }
}
