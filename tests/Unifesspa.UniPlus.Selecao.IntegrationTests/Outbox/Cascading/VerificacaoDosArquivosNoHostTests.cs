namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;

/// <summary>
/// A verificação dos arquivos do candidato no pipeline real do Wolverine: o resultado chega pelo barramento e a
/// reconciliação em duas réplicas republica o pedido uma vez só.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class VerificacaoDosArquivosNoHostTests
{
    private readonly CascadingFixture _fixture;

    public VerificacaoDosArquivosNoHostTests(CascadingFixture fixture) => _fixture = fixture;

    private async Task<Guid> SemearEmVerificacaoAsync(DateTimeOffset publicadoEm)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;
        IInscricaoRepository inscricoes = sp.GetRequiredService<IInscricaoRepository>();
        IDocumentoDaInscricaoRepository documentos = sp.GetRequiredService<IDocumentoDaInscricaoRepository>();
        Inscricao inscricao = Inscricao.Iniciar(
            Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        await inscricoes.AdicionarAsync(inscricao);

        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", Aplicabilidade.Geral, false, null,
            [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, laudo, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", new RelogioFixo(publicadoEm)).IsSuccess.Should().BeTrue();
        await documentos.AdicionarApresentacaoAsync(apresentacao);
        await documentos.AdicionarArquivoAsync(arquivo);
        await sp.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();
        return arquivo.Id;
    }

    private async Task<ArquivoDaApresentacao> LerAsync(Guid id)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>()
            .ArquivosDaApresentacao.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    [Fact(DisplayName = "O resultado do verificador chega pelo barramento e o arquivo termina verificado")]
    public async Task Resultado_PeloBarramento_RegistraOVeredicto()
    {
        Guid id = await SemearEmVerificacaoAsync(DateTimeOffset.UtcNow);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(
            new ResultadoDaVerificacaoDeArquivo(id, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, new string('c', 64), 1024, FormatoDoArquivo.Pdf));

        ArquivoDaApresentacao arquivo = await LerAsync(id);
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Verificado);
        arquivo.HashSha256.Should().Be(new string('c', 64));
    }

    [Fact(DisplayName = "Duas réplicas na reconciliação: o pedido vencido é republicado uma vez só")]
    public async Task Reconciliacao_EmDuasReplicas_RepublicaUmaVez()
    {
        DateTimeOffset vencido = DateTimeOffset.UtcNow.AddHours(-2);
        Guid id = await SemearEmVerificacaoAsync(vencido);

        async Task<int> Replica()
        {
            await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
            Result<int> resultado = await scope.ServiceProvider.GetRequiredService<Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus>()
                .Send(new ReconciliarVerificacaoDosArquivosCommand());
            return resultado.Value;
        }

        int[] republicados = await Task.WhenAll(Replica(), Replica());

        republicados.Sum().Should().Be(1);
        ArquivoDaApresentacao arquivo = await LerAsync(id);
        arquivo.ProximaPublicacaoEm.Should().BeAfter(DateTimeOffset.UtcNow);
        (await Replica()).Should().Be(0, "a publicação nova reinicia o prazo");
    }
}
