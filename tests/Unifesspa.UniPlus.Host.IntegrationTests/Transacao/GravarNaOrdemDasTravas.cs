namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Comando de teste que grava em Publicações travando antes o recurso compartilhado e o processo.</summary>
public sealed record GravarNaOrdemDasTravas(Guid ProcessoId, string Codigo) : ICommand<Result>;

/// <summary>
/// Handler de teste de uma transação que grava em mais de um módulo na ordem de travas da ADR-0170: recurso compartilhado
/// (uma linha do catálogo da Configuração), depois o processo, depois o ato, que é a gravação em Publicações. O recurso é
/// travado por SQL do próprio teste, porque nenhuma operação do produto o trava hoje.
/// </summary>
public static class GravarNaOrdemDasTravasHandler
{
    public const string TravaDoRecursoCompartilhado =
        "SELECT 1 FROM configuracao.rol_de_fatos_candidato ORDER BY id LIMIT 1 FOR UPDATE";

    public static async Task<Result> Handle(
        GravarNaOrdemDasTravas comando,
        UniPlusDbContext contexto,
        IProcessoSeletivoRepository processos,
        ITipoAtoPublicadoRepository tipos,
        IUnitOfWork unidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(unidade);

        await contexto.Database.ExecuteSqlRawAsync(TravaDoRecursoCompartilhado, cancellationToken);
        await processos.TravarAsync(comando.ProcessoId, cancellationToken);
        await tipos.AdicionarAsync(GravarEmDoisModulosHandler.NovoTipo(comando.Codigo), cancellationToken);
        await unidade.SalvarAlteracoesAsync(cancellationToken);

        return Result.Success();
    }
}
