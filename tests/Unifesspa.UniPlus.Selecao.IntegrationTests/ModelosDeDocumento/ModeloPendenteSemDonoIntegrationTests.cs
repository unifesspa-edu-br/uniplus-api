namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

public sealed class ModeloPendenteSemDonoIntegrationTests(ProcessoSeletivoDbFixture fixture, MinioContainerFixture minio)
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    [Fact]
    public async Task RemoverSePendenteVencido_SemDono_RemoveAoVencer()
    {
        FakeTimeProvider relogio = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(Guid.CreateVersion7(), relogio: relogio);
        ArquivoArmazenadoStorageService armazenamento = new ArmazenamentoDeTesteNoMinio(minio, "uniplus-modelos-sem-dono-test").ArquivosArmazenados();
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        contexto.ModelosDeDocumento.Add(modelo);
        await contexto.SaveChangesAsync();
        await GravarObjetosAsync(armazenamento, modelo);
        relogio.Advance(EnviosPendentesDeTeste.PrazoDeEnvio + RemocaoDeArquivosPendentesVencidos.Tolerancia + TimeSpan.FromMinutes(1));
        await RemocaoDePendentesDeTeste.ExecutarAsync(fixture, armazenamento, relogio);
        (await contexto.ModelosDeDocumento.AsNoTracking().AnyAsync(item => item.Id == modelo.Id)).Should().BeFalse();
        (await armazenamento.ObterInfoAsync(modelo.ObjectKey)).Should().BeNull();
        (await armazenamento.ObterInfoAsync(modelo.ChaveDaCopiaGravada)).Should().BeNull();
    }

    [Fact]
    public async Task RemoverDoProcesso_ComModeloPendente_DevolveAsDuasChaves()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(Guid.CreateVersion7());
        contexto.ModelosDeDocumento.Add(modelo);
        await contexto.SaveChangesAsync();

        IReadOnlyList<string> chaves = await new ModeloDeDocumentoRepository(contexto).RemoverDoProcessoAsync(modelo.ProcessoSeletivoId);
        await contexto.SaveChangesAsync();

        chaves.Should().Equal(modelo.ObjectKey, modelo.ChaveDaCopiaGravada);
        (await contexto.ModelosDeDocumento.AsNoTracking().AnyAsync(item => item.Id == modelo.Id)).Should().BeFalse();
    }
    private static async Task GravarObjetosAsync(ArquivoArmazenadoStorageService armazenamento, ModeloDeDocumento modelo)
    {
        foreach (string chave in new[] { modelo.ObjectKey, modelo.ChaveDaCopiaGravada })
        {
            await armazenamento.SalvarConteudoSeladoAsync(chave, "conteúdo"u8.ToArray(), modelo.ContentType);
            (await armazenamento.ObterInfoAsync(chave)).Should().NotBeNull();
        }
    }

}
