namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A exclusão do rascunho como o Wolverine a executa: o handler real sobre repositórios reais, numa transação ambiente
/// que retém o bloqueio exclusivo da linha até o fim, por uma pessoa que pode configurar em qualquer unidade.
/// </summary>
internal sealed class ExclusaoDoRascunhoDeTeste(UniPlusDbContext contexto)
{
    private static readonly IOptions<TravaDoProcessoOptions> EsperaPeloBloqueio =
        Options.Create(new TravaDoProcessoOptions { EsperaMaxima = TimeSpan.FromSeconds(10) });

    public async Task<(Result Resposta, IEnumerable<object> Mensagens)> ExcluirNaTransacaoAsync(Guid rascunhoId)
    {
        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync();
        (Result, IEnumerable<object>) resultado = await ExcluirAsync(rascunhoId);
        await transacao.CommitAsync();
        return resultado;
    }

    private Task<(Result Resposta, IEnumerable<object> Mensagens)> ExcluirAsync(Guid rascunhoId)
    {
        RascunhoDeProcessoSeletivoRepository rascunhos = new(contexto, EsperaPeloBloqueio);
        BloqueioDoRascunhoDeProcessoSeletivo bloqueio = new(rascunhos, ProcessoSeletivoRepositoryDeTeste.Criar(contexto), PessoaQuePodeConfigurar());

        return ExcluirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new ExcluirRascunhoDeProcessoSeletivoCommand(rascunhoId, PrecondicaoIfMatch.Curinga),
            bloqueio, rascunhos, new DocumentoAPublicarRepository(contexto), new ModeloDeDocumentoRepository(contexto), contexto, CancellationToken.None);
    }

    private static IVerificadorDeAcesso PessoaQuePodeConfigurar()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Permitido);
        return verificador;
    }
}
