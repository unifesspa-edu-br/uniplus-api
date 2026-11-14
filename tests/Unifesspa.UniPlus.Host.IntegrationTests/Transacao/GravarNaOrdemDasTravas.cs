namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Comando de teste que registra um ato em Publicações travando antes o recurso compartilhado e o processo.</summary>
public sealed record GravarNaOrdemDasTravas(Guid ProcessoId, string Codigo, string TipoCodigo) : ICommand<Result>;

/// <summary>
/// Handler de teste de uma transação que grava em mais de um módulo na ordem de travas da ADR-0170: recurso compartilhado
/// (uma linha do catálogo da Configuração), depois o processo, depois o ato, que é o registro pela porta de escrita de
/// Publicações. O recurso é travado por SQL do próprio teste, porque nenhuma operação do produto o trava hoje.
/// </summary>
public static class GravarNaOrdemDasTravasHandler
{
    public const string TravaDoRecursoCompartilhado =
        "SELECT 1 FROM configuracao.rol_de_fatos_candidato ORDER BY id LIMIT 1 FOR UPDATE";

    public static async Task<Result> Handle(
        GravarNaOrdemDasTravas comando,
        UniPlusDbContext contexto,
        IProcessoSeletivoRepository processos,
        IRegistroDeAtoNormativo registro,
        IUnitOfWork unidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(unidade);

        await contexto.Database.ExecuteSqlRawAsync(TravaDoRecursoCompartilhado, cancellationToken);
        await processos.TravarAsync(comando.ProcessoId, cancellationToken);

        return await RegistroEmTransacaoDeTeste.RegistrarESalvarAsync(registro, unidade, AtoDoComando(comando), cancellationToken);
    }

    private static AtoNormativoPublicado AtoDoComando(GravarNaOrdemDasTravas comando) =>
        RegistrarAtoPelaPortaHandler.AtoDe(new RegistrarAtoPelaPorta(
            comando.Codigo, Guid.CreateVersion7(), comando.ProcessoId, comando.TipoCodigo, UnicoPorObjeto: true, DesfechoDaGravacao.Sucesso));
}
