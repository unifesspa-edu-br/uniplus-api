namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O endereço da inscrição só é aceito com CEP encontrado no Geo; o CEP geral da cidade exige logradouro
/// e bairro do candidato. O nível vem do Geo, nunca do navegador (issue #1948).
/// </summary>
public sealed class ConferenciaDoEnderecoDaInscricaoTests
{
    private const string Campo = "ENDERECO_RESIDENCIAL";
    private const string Paragominas = "1505502";

    private static readonly RespostasAdmitidas Admitidas = new(
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
        {
            [Campo] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
        },
        new HashSet<string>(StringComparer.Ordinal) { Campo },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        new HashSet<Guid>(),
        new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal));

    private static Dictionary<string, JsonElement> Endereco(
        string? logradouro = "Rua Brigadeiro Eduardo Gomes",
        string? bairro = "Laércio Cabeline",
        string cep = "68627476",
        string codigoIbge = Paragominas,
        bool? confirmado = null,
        string origemDoNavegador = "geo-api") => new(StringComparer.Ordinal)
        {
            [Campo] = JsonSerializer.SerializeToElement(new
            {
                cep,
                logradouro,
                bairro,
                cidade = new { codigoIbge, nome = "Paragominas", uf = "PA" },
                nivelResolucao = "logradouro",
                origem = origemDoNavegador,
                cepConfirmado = confirmado,
            }),
        };

    private static Task<Result<IReadOnlyList<FieldError>>> ConferirAsync(
        Dictionary<string, JsonElement> respostas, FakeConferenciaEnderecoGeo geo, bool aoEnviar) =>
        ConferenciaDoEnderecoDaInscricao.ConferirAsync(respostas, Admitidas, geo, aoEnviar);

    [Theory(DisplayName = "CEP que o Geo não encontra é recusado com a orientação dos Correios e do CEP geral, ao salvar e ao enviar")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CepNaoEncontrado_Recusa(bool aoEnviar)
    {
        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), new FakeConferenciaEnderecoGeo(), aoEnviar);

        resultado.Value!.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == Campo && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado
                && e.Error.Message == "CEP não encontrado. Confira o seu CEP na busca dos Correios; se o seu bairro não tem CEP próprio, use o CEP geral da sua cidade e descreva o endereço real no complemento.");
    }

    [Theory(DisplayName = "CEP que resolve a outra cidade que a declarada é recusado, ao salvar e ao enviar")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CepDeOutraCidade_Recusa(bool aoEnviar)
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", "1501402", "PA");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), geo, aoEnviar);

        resultado.Value!.Should().ContainSingle(e => e.Field == Campo && e.Error.Code == InscricaoErrorCodes.EnderecoCepDeOutraCidade);
    }

    [Fact(DisplayName = "CEP geral oficial (cep-geral) com logradouro e bairro digitados é aceito")]
    public async Task CepGeral_ComLogradouroEBairro_Aceita()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "cep-geral");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), geo, aoEnviar: true);

        resultado.Value!.Should().BeEmpty();
    }

    [Theory(DisplayName = "CEP geral oficial sem logradouro ou sem bairro é recusado ao enviar")]
    [InlineData(null, "Laércio Cabeline", InscricaoErrorCodes.EnderecoLogradouroObrigatorio)]
    [InlineData("  ", "Laércio Cabeline", InscricaoErrorCodes.EnderecoLogradouroObrigatorio)]
    [InlineData("Rua Brigadeiro Eduardo Gomes", null, InscricaoErrorCodes.EnderecoBairroObrigatorio)]
    public async Task CepGeral_SemLogradouroOuBairro_RecusaAoEnviar(string? logradouro, string? bairro, string codigo)
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "cep-geral");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(logradouro, bairro), geo, aoEnviar: true);

        resultado.Value!.Should().ContainSingle(e => e.Field == Campo && e.Error.Code == codigo);
    }

    [Theory(DisplayName = "CEP de origem não exata (faixa ou desconhecida), sem a confirmação do candidato, é recusado com a mensagem da confirmação")]
    [InlineData("faixa-cidade")]
    [InlineData("faixa-bairro")]
    [InlineData("faixa-distrito")]
    [InlineData("origem-que-a-api-nao-conhece")]
    public async Task CepPorFaixa_SemConfirmacao_Recusa(string origem)
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", origem);

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), geo, aoEnviar: true);

        resultado.Value!.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == Campo && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoConfirmado
                && e.Error.Message == "O CEP informado não corresponde a uma rua cadastrada nos Correios. Confira o número; se o seu endereço usa o CEP geral da cidade ou do bairro, confirme no formulário.");
    }

    [Fact(DisplayName = "CEP resolvido só por faixa com a confirmação explícita, o logradouro e o bairro é aceito")]
    public async Task CepPorFaixa_Confirmado_Aceita()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-bairro");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(confirmado: true), geo, aoEnviar: true);

        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "CEP de faixa sem confirmação e sem logradouro recusa só a confirmação: logradouro e bairro vêm depois")]
    public async Task CepPorFaixa_SemConfirmacaoESemLogradouro_RecusaSoAConfirmacao()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-cidade");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(logradouro: null, bairro: null), geo, aoEnviar: true);

        resultado.Value!.Should().ContainSingle().Which.Error.Code.Should().Be(InscricaoErrorCodes.EnderecoCepNaoConfirmado);
    }

    [Fact(DisplayName = "CEP resolvido só por faixa e confirmado, mas sem logradouro nem bairro, é recusado")]
    public async Task CepPorFaixa_ConfirmadoSemLogradouroEBairro_Recusa()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-cidade");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(logradouro: null, bairro: null, confirmado: true), geo, aoEnviar: true);

        resultado.Value!.Select(e => e.Error.Code).Should().BeEquivalentTo(
            [InscricaoErrorCodes.EnderecoLogradouroObrigatorio, InscricaoErrorCodes.EnderecoBairroObrigatorio]);
    }

    [Fact(DisplayName = "CEP resolvido só por faixa com confirmação falsa é recusado como sem confirmação")]
    public async Task CepPorFaixa_ConfirmacaoFalsa_Recusa()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-cidade");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(confirmado: false), geo, aoEnviar: true);

        resultado.Value!.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoConfirmado);
    }

    [Theory(DisplayName = "CEP exato (logradouro ou grande usuário) aceita logradouro e bairro diferentes dos do Geo, ou em branco")]
    [InlineData("logradouro")]
    [InlineData("grande-usuario")]
    public async Task CepExato_AceitaLogradouroEBairroEditados(string origem)
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", origem);

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(logradouro: "Outra Rua", bairro: null), geo, aoEnviar: true);

        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "O CEP de grande usuário, que chega sem bairro, não exige bairro nem confirmação")]
    public async Task GrandeUsuario_SemBairro_Aceita()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "grande-usuario");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(bairro: null), geo, aoEnviar: true);

        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "No rascunho, o CEP de faixa sem confirmação nem logradouro ainda é aceito: o candidato está preenchendo")]
    public async Task CepPorFaixa_AceitaNoRascunho()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-cidade");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(logradouro: null, bairro: null), geo, aoEnviar: false);

        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "A origem que o navegador declara não decide: vale a que o Geo devolve")]
    public async Task OrigemDoNavegador_EhIgnorada()
    {
        FakeConferenciaEnderecoGeo geo = new FakeConferenciaEnderecoGeo().ComCep("68627476", Paragominas, "PA", "faixa-cidade");

        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(origemDoNavegador: "logradouro"), geo, aoEnviar: true);

        resultado.Value!.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoConfirmado);
    }

    [Fact(DisplayName = "Geo fora do ar ao enviar é falha fechada de indisponibilidade, nunca CEP não encontrado")]
    public async Task GeoIndisponivel_AoEnviar_FalhaFechada()
    {
        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), new FakeConferenciaEnderecoGeo().Indisponivel(), aoEnviar: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Geo fora do ar ao salvar adia a conferência: o rascunho não é recusado")]
    public async Task GeoIndisponivel_AoSalvar_AdiaAConferencia()
    {
        Result<IReadOnlyList<FieldError>> resultado = await ConferirAsync(Endereco(), new FakeConferenciaEnderecoGeo().Indisponivel(), aoEnviar: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Should().BeEmpty();
    }
}
