namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>Um arquivo enviado pelo candidato, gravado ainda pendente, com o contexto e o repositório em que foi gravado.</summary>
internal sealed class ArquivoPendenteDaInscricao(
    SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) : IDisposable
{
    public SelecaoDbContext Context { get; } = context;

    public DocumentoDaInscricaoRepository Repositorio { get; } = repositorio;

    public ArquivoDaApresentacao Arquivo { get; } = arquivo;

    /// <summary>A confirmação do envio, que deixa o arquivo em verificação.</summary>
    public async Task ColocarEmVerificacaoAsync()
    {
        Arquivo.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        Repositorio.Atualizar(Arquivo);
        await Context.SaveChangesAsync();
    }

    public void Dispose() => Context.Dispose();
}
