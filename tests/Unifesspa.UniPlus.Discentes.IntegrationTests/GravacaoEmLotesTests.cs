namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Discentes.IntegrationTests.VinculoDeTeste;

/// <summary>
/// Prova, contra um Postgres real, o que a sincronização promete sobre gravação em lotes.
/// </summary>
/// <remarks>
/// A promessa central é que um lote confirmado permanece confirmado mesmo que a execução
/// falhe depois. Sem esta prova contra banco de verdade, a promessa vale apenas enquanto
/// ninguém envolver a sincronização inteira numa transação só.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo público para descoberta.")]
[Trait("Category", "Integration")]
public sealed class GravacaoEmLotesTests : IClassFixture<VinculoDiscenteDbFixture>
{
    private readonly VinculoDiscenteDbFixture _fixture;

    public GravacaoEmLotesTests(VinculoDiscenteDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// O lote seguinte falha ao confirmar: dois vínculos com o mesmo identificador de origem
    /// violam a chave natural da réplica. O que interessa aqui não é a causa da falha, e sim
    /// que ela não alcança o que já foi confirmado antes.
    /// </summary>
    [Fact]
    public async Task Lote_confirmado_permanece_apos_falha_em_lote_posterior()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_001), Sincronizavel(9_002)]);

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            await GravarEConfirmarAsync(contexto, [Sincronizavel(9_003), Sincronizavel(9_003)]));

        contexto.ChangeTracker.Clear();

        int gravados = await ContarAsync(9_001, 9_002);

        gravados.Should().Be(2, "o lote já confirmado não pode ser desfeito por falha posterior");
    }

    /// <summary>
    /// O primeiro vínculo já está na réplica com o mesmo conteúdo, então não é recifrado. O
    /// segundo é novo, e cifrar o CPF dele falha.
    /// </summary>
    [Fact]
    public async Task Falha_ao_cifrar_preserva_o_que_ja_fora_reconhecido_como_igual()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_501)]);
        contexto.ChangeTracker.Clear();

        VinculoDiscenteRepository repositorio = new(contexto, new CifraQueRecusaCifrar(_fixture.Encryption));

        FalhaAoPrepararLoteException falha = await Assert.ThrowsAsync<FalhaAoPrepararLoteException>(
            async () => await repositorio.GravarLoteAsync([Sincronizavel(9_501), Sincronizavel(9_502)]));

        falha.Parcial.Inalterados.Should().Be(
            1,
            "o vínculo reconhecido como igual continua correto na réplica; contá-lo como não "
            + "gravado faria o registro da execução subestimar o que ela alcançou");
    }

    [Fact]
    public async Task Vinculo_ausente_do_lote_permanece_intacto()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_101), Sincronizavel(9_102)]);
        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_101, nome: "NOME CORRIGIDO")]);

        VinculoDiscenteRecord? ausenteDoSegundoLote = await LerAsync(9_102);

        ausenteDoSegundoLote.Should().NotBeNull(
            "ausência de um vínculo no lote não é ordem para removê-lo");
    }

    [Fact]
    public async Task Conteudo_igual_nao_reescreve_a_linha()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_201)]);

        ResultadoDaGravacao segunda = await new VinculoDiscenteRepository(contexto, _fixture.Encryption)
            .GravarLoteAsync([Sincronizavel(9_201)]);

        segunda.Inalterados.Should().Be(1);
        segunda.Escritos.Should().Be(0, "resumo igual dispensa reescrever e recifrar o CPF");
    }

    [Fact]
    public async Task Conteudo_diferente_atualiza_a_linha_existente()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_301)]);
        contexto.ChangeTracker.Clear();

        ResultadoDaGravacao segunda = await GravarEConfirmarAsync(
            contexto, [Sincronizavel(9_301, nome: "OUTRO NOME", conteudo: "conteudo-diferente")]);

        segunda.Atualizados.Should().Be(1);
        (await LerAsync(9_301))!.Nome.Should().Be("OUTRO NOME");
    }

    /// <summary>
    /// Sem invalidar, a sincronização seguinte compararia o resumo antigo com o da origem,
    /// concluiria que nada mudou, e a alteração feita aqui permaneceria indefinidamente —
    /// divergindo da origem sem nada apontar a divergência.
    /// </summary>
    [Fact]
    public async Task Atualizacao_fora_da_sincronizacao_invalida_o_resumo()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await GravarEConfirmarAsync(contexto, [Sincronizavel(9_401)]);
        contexto.ChangeTracker.Clear();

        VinculoDiscenteRepository repositorio = new(contexto, _fixture.Encryption);
        VinculoDiscente doBanco = (await repositorio.ObterPorIdSigaaAsync(IdNoSigaa(9_401)))!;
        await repositorio.AtualizarAsync(doBanco);
        await contexto.SaveChangesAsync();

        (await LerAsync(9_401))!.ResumoDoConteudo.Should().BeEmpty(
            "resumo que não descreve mais o que está guardado não pode dispensar a próxima escrita");
    }

    /// <summary>
    /// Cancelamento não é falha de lote: se a cifra for cancelada no meio da preparação, o
    /// cancelamento sobe como está, e não vira <see cref="FalhaAoPrepararLoteException"/>.
    /// </summary>
    [Fact]
    public async Task Cancelamento_ao_cifrar_sobe_sem_virar_falha_de_lote()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        VinculoDiscenteRepository repositorio = new(contexto, new CifraCancelada(_fixture.Encryption));

        Func<Task> gravacao = () => repositorio.GravarLoteAsync([Sincronizavel(9_601)]);

        await gravacao.Should().ThrowAsync<OperationCanceledException>();
    }

    private async Task<ResultadoDaGravacao> GravarEConfirmarAsync(
        UniPlusDbContext contexto,
        IReadOnlyList<VinculoSincronizavel> lote)
    {
        ResultadoDaGravacao resultado = await new VinculoDiscenteRepository(contexto, _fixture.Encryption).GravarLoteAsync(lote);
        await contexto.SaveChangesAsync();
        return resultado;
    }

    private async Task<VinculoDiscenteRecord?> LerAsync(long idDeOrigem)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();

        return await leitura.VinculosDiscentes.SingleOrDefaultAsync(v => v.IdDiscenteSigaa == idDeOrigem);
    }

    private async Task<int> ContarAsync(params long[] idsDeOrigem)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();

        return await leitura.VinculosDiscentes.CountAsync(v => idsDeOrigem.Contains(v.IdDiscenteSigaa));
    }

    private sealed class CifraCancelada : IUniPlusEncryptionService
    {
        private readonly IUniPlusEncryptionService _real;

        public CifraCancelada(IUniPlusEncryptionService real) => _real = real;

        public Task<byte[]> EncryptAsync(
            string keyName, byte[] plaintext, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();

        public Task<byte[]> DecryptAsync(
            string keyName, byte[] ciphertext, CancellationToken cancellationToken = default) =>
            _real.DecryptAsync(keyName, ciphertext, cancellationToken);
    }

    /// <summary>
    /// Decifra normalmente, mas recusa cifrar. Deixa o reconhecimento do que já está igual
    /// acontecer — ele só lê — e falha no primeiro vínculo que precisaria ser escrito.
    /// </summary>
    private sealed class CifraQueRecusaCifrar : IUniPlusEncryptionService
    {
        private readonly IUniPlusEncryptionService _real;

        public CifraQueRecusaCifrar(IUniPlusEncryptionService real) => _real = real;

        public Task<byte[]> EncryptAsync(
            string keyName, byte[] plaintext, CancellationToken cancellationToken = default) =>
            throw new EncryptionFailureException("Falha simulada ao cifrar o CPF.");

        public Task<byte[]> DecryptAsync(
            string keyName, byte[] ciphertext, CancellationToken cancellationToken = default) =>
            _real.DecryptAsync(keyName, ciphertext, cancellationToken);
    }

}
