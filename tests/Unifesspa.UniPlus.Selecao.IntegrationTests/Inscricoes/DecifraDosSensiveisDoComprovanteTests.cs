namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// As respostas de cifra individual só são decifradas para o comprovante que o titular pediu: listar as emissões da inscrição, que
/// só precisa do instante de cada uma, não decifra nenhum comprovante. O envelope cifrado de cada comprovante é ligado a ele: o
/// copiado de outro comprovante e as colunas apagadas são detectados na leitura.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DecifraDosSensiveisDoComprovanteTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly DateTimeOffset Primeiro = InstanteDoRelogioComMicrossegundos();

    private const string CopiaOEnvelopeDoComprovanteAnterior = """
        UPDATE selecao.comprovantes_inscricao c
        SET sensiveis_cifrados = o.sensiveis_cifrados
        FROM selecao.comprovantes_inscricao o
        WHERE c.inscricao_id = @id AND o.inscricao_id = @id AND o.numero_inscricao = c.numero_inscricao AND o.instante < c.instante
        """;

    private readonly InscricaoDbFixture _fixture;
    private readonly CenarioDaInscricaoGravada _cenario;

    public DecifraDosSensiveisDoComprovanteTests(InscricaoDbFixture fixture)
    {
        _fixture = fixture;
        _cenario = new CenarioDaInscricaoGravada(fixture);
    }

    [Fact(DisplayName = "Ler o comprovante pedido decifra só os sensíveis dele, e listar as emissões da inscrição não decifra nenhum")]
    public async Task DecifraSoOComprovantePedido()
    {
        (Inscricao inscricao, ComprovanteInscricao envio, ComprovanteInscricao correcao) = await GravarEnvioECorrecaoAsync();
        IUniPlusEncryptionService espiao = EspiaoDaCifra();
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        ComprovanteDaInscricaoRepository repositorio = RepositoriosDaInscricaoDeTeste.Comprovantes(leitura, _fixture, espiao);

        await repositorio.ObterEmissoesAsync([inscricao.Id]);
        await espiao.DidNotReceiveWithAnyArgs().DecryptAsync(default!, default!, default);

        ComprovanteNaPosicao? segundo = await repositorio.ObterComprovanteAsync(inscricao.Id, 2);

        await ConferirQueSoACorrecaoFoiDecifradaAsync(espiao, envio, correcao);
        segundo!.Comprovante.TodasAsRespostas()["IDENTIDADE_GENERO"].GetString().Should().Be("TRANSGENERO");
        segundo.Comprovante.IntegridadeConfere().Should().BeTrue("o instante com microssegundos volta do banco com o que o hash cobriu");
    }

    [Fact(DisplayName = "O envelope dos dados sensíveis copiado de outro comprovante é detectado na leitura")]
    public async Task DadoSensivel_EnvelopeTrocadoEntreComprovantes_ELidoComoAdulterado()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        DateTimeOffset instante = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        ComprovanteInscricao envio = Enviar(original, "000556", ComIdentidadeDeGenero("PESSOA_CIS", instante)).Value!;
        ComprovanteInscricao correcao = Enviar(original, "000556", ComIdentidadeDeGenero("PESSOA_NAO_BINARIA", instante.AddMinutes(5))).Value!;
        await _cenario.GravarAsync(original, envio, correcao);

        (await _cenario.ComprovantesGravadosAsync(original.Id)).Select(static c => c.RespostasSensiveis["IDENTIDADE_GENERO"].GetString())
            .Should().Equal("PESSOA_CIS", "PESSOA_NAO_BINARIA");
        (await _cenario.ExecutarAsync(CopiaOEnvelopeDoComprovanteAnterior, original.Id)).Should().Be(1);

        Func<Task> ler = () => _cenario.ComprovantesGravadosAsync(original.Id);

        (await ler.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("não pertencem");
    }

    [Theory(DisplayName = "Apagar as colunas dos dados sensíveis do comprovante é detectado: uma só falha a leitura, as duas fazem a integridade acusar")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DadoSensivel_ColunasApagadas_SaoDetectadas(bool apagaOCifrado, bool apagaOHmac)
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ComprovanteInscricao envio = Enviar(original, "000557", new ConteudoDoEnvio { Respostas = Respostas(("IDENTIDADE_GENERO", "PESSOA_CIS")) }).Value!;
        await _cenario.GravarAsync(original, envio);
        await _cenario.ExecutarAsync(ApagarAsColunas(apagaOCifrado, apagaOHmac), original.Id);

        Func<Task> ler = () => _cenario.ComprovantesGravadosAsync(original.Id);
        if (apagaOCifrado && apagaOHmac)
        {
            (await _cenario.ComprovantesGravadosAsync(original.Id)).Should().ContainSingle().Which.IntegridadeConfere().Should().BeFalse("o hash cobre o código do fato sensível que sumiu");
        }
        else
        {
            await ler.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    private async Task<(Inscricao Inscricao, ComprovanteInscricao Envio, ComprovanteInscricao Correcao)> GravarEnvioECorrecaoAsync()
    {
        Inscricao inscricao = Inscricao.Iniciar(
            Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;
        ComprovanteInscricao envio = Emitir(inscricao, Primeiro, "CIS", "HETEROSSEXUAL");
        ComprovanteInscricao correcao = Emitir(inscricao, Primeiro.AddMinutes(5), "TRANSGENERO", "BISSEXUAL");
        await GravarAsync(inscricao, envio, correcao);

        return (inscricao, envio, correcao);
    }

    private static async Task ConferirQueSoACorrecaoFoiDecifradaAsync(
        IUniPlusEncryptionService espiao, ComprovanteInscricao envio, ComprovanteInscricao correcao)
    {
        await espiao.Received(1).DecryptAsync(
            SelecaoEncryptionKeys.SensiveisAesGcm, Arg.Is<byte[]>(b => b.SequenceEqual(correcao.SensiveisCifrados!)), Arg.Any<CancellationToken>());
        await espiao.DidNotReceive().DecryptAsync(
            Arg.Any<string>(), Arg.Is<byte[]>(b => b.SequenceEqual(envio.SensiveisCifrados!)), Arg.Any<CancellationToken>());
    }

    private static DateTimeOffset InstanteDoRelogioComMicrossegundos()
    {
        DateTimeOffset agora = RelogioEmMicrossegundos.DoSistema.GetUtcNow();

        return agora.Microsecond == 0 ? agora.AddMicroseconds(1) : agora;
    }

    private static ComprovanteInscricao Emitir(Inscricao inscricao, DateTimeOffset instante, string identidadeDeGenero, string orientacaoSexual) =>
        ComprovanteInscricao.Emitir(
            inscricao.Id, "000901", instante, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], [],
            modalidades: null,
            respostasSensiveis: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement(identidadeDeGenero),
                ["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement(orientacaoSexual),
            });

    private async Task GravarAsync(Inscricao inscricao, params ComprovanteInscricao[] comprovantes)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext();
        InscricaoRepository repositorio = RepositoriosDaInscricaoDeTeste.Inscricoes(escrita, _fixture);
        await repositorio.AdicionarAsync(inscricao);
        foreach (ComprovanteInscricao comprovante in comprovantes)
        {
            await RepositoriosDaInscricaoDeTeste.Comprovantes(escrita, _fixture).AdicionarComprovanteAsync(comprovante);
        }

        await escrita.SaveChangesAsync();
    }

    private IUniPlusEncryptionService EspiaoDaCifra()
    {
        IUniPlusEncryptionService espiao = Substitute.For<IUniPlusEncryptionService>();
        espiao.DecryptAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(chamada => _fixture.Encryption.DecryptAsync(chamada.ArgAt<string>(0), chamada.ArgAt<byte[]>(1), chamada.ArgAt<CancellationToken>(2)));
        return espiao;
    }

    private static ConteudoDoEnvio ComIdentidadeDeGenero(string identidadeDeGenero, DateTimeOffset instante) =>
        new() { Respostas = Respostas(("IDENTIDADE_GENERO", identidadeDeGenero)), Instante = instante };

    private static string ApagarAsColunas(bool apagaOCifrado, bool apagaOHmac) =>
        $"UPDATE selecao.comprovantes_inscricao SET {(apagaOCifrado ? "sensiveis_cifrados = NULL" : "sensiveis_cifrados = sensiveis_cifrados")}, "
        + $"{(apagaOHmac ? "sensiveis_hmac = NULL" : "sensiveis_hmac = sensiveis_hmac")} WHERE inscricao_id = @id";
}
