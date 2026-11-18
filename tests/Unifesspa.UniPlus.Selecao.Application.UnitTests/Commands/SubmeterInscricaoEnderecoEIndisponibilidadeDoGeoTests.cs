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
using Unifesspa.UniPlus.Regras.ValueObjects;
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
/// O endereço respondido é conferido no geo, e a indisponibilidade dele recusa só o que depende da residência.
/// </summary>
public sealed class SubmeterInscricaoEnderecoEIndisponibilidadeDoGeoTests
{
    private static async Task<(Result<ReciboDaInscricaoDto> Resultado, IUnitOfWork UnitOfWork)> SubmeterComEnderecoAsync(IConferenciaEnderecoGeo geo, string? cep = "66010000")
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        (IInscricaoRepository inscricaoRepository, IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registroCodecs) = PortasPara(existente, versao, configuracao);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        InscricaoRequest request = PedidoComEndereco(cpf, configuracao, termo, cep);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geo);
        return (resultado, unitOfWork);
    }

    private static InscricaoRequest PedidoComEndereco(Cpf cpf, ConfiguracaoPublicada configuracao, VersaoTermoEscolhida termo, string? cep)
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep, cidade = new { codigoIbge = "1501402", uf = "PA" } });
        return new(OfertaDo(configuracao), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

    }

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

    [Fact(DisplayName = "Com o Geo fora do ar, o CPF de outra pessoa é recusado como 422 e não vira indisponibilidade")]
    public async Task Handle_GeoForaDoArComCpfDeOutraPessoa_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("CPF", PersonasDeTeste.KevinCarvalho.Valor)), geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().Contain(e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
    }

    private static FatoColetado ObrigatorioSoParaQuemMoraEm(string municipio) => FatoColetado.Criar(
        "OUTRA_REGIAO", 7, "Outra região", TipoRenderizacao.Texto,
        Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(municipio)).Value!)]).Value!),
        null, classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

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
        (Result<ReciboDaInscricaoDto> resultado, _) = await new FormularioASubmeterDeTeste([ObrigatorioSoParaQuemMoraEm("9999999")], null)
        {
            Geo = GeoQueConhece().Indisponivel(),
            Ausentes = ["NOME_MAE"],
        }.SubmeterAsync(Texto());

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
