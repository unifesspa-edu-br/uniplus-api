namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Messaging;

using Microsoft.EntityFrameworkCore;

using Wolverine.Attributes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// SPIKE #820 — handler da <see cref="RegistrarAtoNormativoRequisicao"/>: a mensagem
/// durável que um domínio enfileirou, na mesma transação em que publicou, para que o ato
/// correspondente seja registrado aqui.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive em Infrastructure, e não em Application, por exigência do middleware.</b> O
/// Wolverine só instala a transação (e o <c>SaveChangesAsync</c> automático) quando
/// enxerga o <c>DbContext</c> CONCRETO na assinatura — uma interface de unidade de
/// trabalho não serve. É o mesmo lugar em que
/// <c>ProcessoPublicadoToKafkaCascadeHandler</c> já mora, e o Discovery do host já
/// inclui os assemblies de Infrastructure.
/// </para>
/// <para>
/// <b>Não chama <c>SaveChanges</c>.</b> Quem salva é o middleware, junto com a marcação
/// do envelope no inbox — numa transação só. Salvar aqui dentro foi o que fez a primeira
/// tentativa do spike morrer com <i>"This NpgsqlTransaction has completed"</i>: o handler
/// tentava usar uma transação que o middleware já havia comitado.
/// </para>
/// <para>
/// <b>Idempotente por chave primária.</b> A fila é at-least-once. Como o id do ato vem na
/// mensagem, decidido pelo domínio que publicou, o segundo processamento reencontra o ato
/// e não faz nada — sem isso a reentrega criaria um ato gêmeo, e o gêmeo disputaria a
/// vaga de linhagem do objeto (ADR-0107) contra a linhagem do primeiro.
/// </para>
/// <para>
/// <b>Falha LANÇA.</b> Um <c>Result.Failure</c> devolvido daqui seria lido pelo Wolverine
/// como sucesso: a mensagem sairia da fila e o ato sumiria em silêncio. Só a exceção
/// aciona o retry e, esgotado, a dead letter — que é a rede de segurança desta
/// orquestração, e que o spike já viu funcionar.
/// </para>
/// </remarks>
public static class RegistrarAtoNormativoRequisicaoHandler
{
    [Transactional]
    public static async Task Handle(
        RegistrarAtoNormativoRequisicao requisicao,
        PublicacoesDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);

        bool jaRegistrado = await db.Set<AtoNormativo>()
            .AsNoTracking()
            .AnyAsync(a => a.Id == requisicao.AtoId, cancellationToken)
            .ConfigureAwait(false);
        if (jaRegistrado)
        {
            return;
        }

        TipoAtoPublicado? tipo = await db.Set<TipoAtoPublicado>()
            .AsNoTracking()
            .Where(t => t.Codigo == requisicao.TipoCodigo
                && t.VigenciaInicio <= requisicao.DataPublicacao
                && (t.VigenciaFim == null || t.VigenciaFim > requisicao.DataPublicacao))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            throw new RegistroDeAtoRecusadoException(
                requisicao.AtoId,
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"Não há versão vigente do tipo de ato '{requisicao.TipoCodigo}' em {requisicao.DataPublicacao:yyyy-MM-dd}.");
        }

        ReferenciaVersaoConfiguracao? versaoInvocada = null;
        if (requisicao.VersaoInvocadaId is { } versaoId && requisicao.VersaoInvocadaHash is { } versaoHash)
        {
            Result<ReferenciaVersaoConfiguracao> referencia = ReferenciaVersaoConfiguracao.Criar(versaoId, versaoHash);
            if (referencia.IsFailure)
            {
                throw new RegistroDeAtoRecusadoException(
                    requisicao.AtoId, referencia.Error!.Code, referencia.Error!.Message);
            }

            versaoInvocada = referencia.Value;
        }

        AtoNormativo ato = AtoNormativo.Registrar(
            requisicao.AtoId,
            requisicao.Orgao,
            requisicao.Serie,
            requisicao.Ano,
            requisicao.Numero,
            tipo.Codigo,
            tipo.CongelaConfiguracao,
            tipo.EfeitoIrreversivel,
            tipo.UnicoPorObjeto,
            requisicao.DataPublicacao,
            requisicao.DocumentoHash,
            requisicao.Assinante,
            timeProvider.GetUtcNow(),
            versaoInvocada,
            requisicao.AtoRetificadoId,
            requisicao.MotivoRetificacao,
            [.. requisicao.Vinculos.Select(v => (v.EntidadeTipo, v.EntidadeId))]);

        // A vaga de linhagem do objeto (ADR-0107): reservada AQUI, junto com o ato, e não
        // antes dele. É o que impede que uma publicação recusada deixe a vaga do certame
        // ocupada por um ato que nunca existiu — o defeito que condenou o desenho síncrono.
        foreach (VinculoAtoEntidade vinculo in ato.Vinculos)
        {
            if (!ato.UnicoPorObjeto)
            {
                continue;
            }

            await db.Set<LinhagemUnicaPorObjeto>()
                .AddAsync(LinhagemUnicaPorObjeto.Criar(ato, vinculo, ato.Id), cancellationToken)
                .ConfigureAwait(false);
        }

        await db.Set<AtoNormativo>().AddAsync(ato, cancellationToken).ConfigureAwait(false);

        // Sem SaveChanges: o middleware transacional do Wolverine comita isto junto com o
        // envelope do inbox. Ver o <remarks> da classe.
    }
}

/// <summary>
/// SPIKE #820 — recusa de negócio no registro de um ato vindo de mensagem durável.
/// Existe para que a falha ESCAPE do handler: só a exceção faz o Wolverine retentar e,
/// esgotada a política, mover o envelope para a dead letter, onde a falha é visível.
/// </summary>
public sealed class RegistroDeAtoRecusadoException : Exception
{
    public RegistroDeAtoRecusadoException(Guid atoId, string codigo, string mensagem)
        : base($"Registro do ato {atoId} recusado ({codigo}): {mensagem}")
    {
        AtoId = atoId;
        Codigo = codigo;
    }

    public RegistroDeAtoRecusadoException()
    {
    }

    public RegistroDeAtoRecusadoException(string message)
        : base(message)
    {
    }

    public RegistroDeAtoRecusadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid AtoId { get; }

    public string Codigo { get; } = string.Empty;
}
