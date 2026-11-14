namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As respostas de cifra individual só são decifradas para o comprovante que o titular pediu: listar as emissões da inscrição, que
/// só precisa do instante de cada uma, não decifra nenhum comprovante.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DecifraDosSensiveisDoComprovanteTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly DateTimeOffset Primeiro = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly InscricaoDbFixture _fixture;

    public DecifraDosSensiveisDoComprovanteTests(InscricaoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Ler o comprovante pedido decifra só os sensíveis dele, e listar as emissões da inscrição não decifra nenhum")]
    public async Task DecifraSoOComprovantePedido()
    {
        Inscricao inscricao = Inscricao.Iniciar(
            Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        ComprovanteInscricao envio = Emitir(inscricao, Primeiro, "CIS", "HETEROSSEXUAL");
        ComprovanteInscricao correcao = Emitir(inscricao, Primeiro.AddMinutes(5), "TRANSGENERO", "BISSEXUAL");
        await GravarAsync(inscricao, envio, correcao);

        IUniPlusEncryptionService espiao = EspiaoDaCifra();
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        InscricaoRepository repositorio = new(leitura, espiao, _fixture.BlindIndex, TimeProvider.System);

        await repositorio.ObterEmissoesAsync([inscricao.Id]);
        await espiao.DidNotReceiveWithAnyArgs().DecryptAsync(default!, default!, default);

        ComprovanteNaPosicao? segundo = await repositorio.ObterComprovanteAsync(inscricao.Id, 2);

        await espiao.Received(1).DecryptAsync(
            SelecaoEncryptionKeys.SensiveisAesGcm, Arg.Is<byte[]>(b => b.SequenceEqual(correcao.SensiveisCifrados!)), Arg.Any<CancellationToken>());
        await espiao.DidNotReceive().DecryptAsync(
            Arg.Any<string>(), Arg.Is<byte[]>(b => b.SequenceEqual(envio.SensiveisCifrados!)), Arg.Any<CancellationToken>());
        segundo!.Comprovante.TodasAsRespostas()["IDENTIDADE_GENERO"].GetString().Should().Be("TRANSGENERO");
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
        InscricaoRepository repositorio = new(escrita, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await repositorio.AdicionarAsync(inscricao);
        foreach (ComprovanteInscricao comprovante in comprovantes)
        {
            await repositorio.AdicionarComprovanteAsync(comprovante);
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
}
