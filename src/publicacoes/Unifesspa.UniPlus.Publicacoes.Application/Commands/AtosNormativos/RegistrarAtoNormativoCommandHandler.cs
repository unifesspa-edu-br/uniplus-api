namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="RegistrarAtoNormativoCommand"/> (convention-based
/// Wolverine): resolve o tipo vigente na data de publicação, copia por valor os
/// atributos de consequência, registra o ato append-only e computa o aviso de
/// número duplicado (AC4).
/// </summary>
public static class RegistrarAtoNormativoCommandHandler
{
    public static async Task<Result<RegistrarAtoNormativoResult>> Handle(
        RegistrarAtoNormativoCommand command,
        ITipoAtoPublicadoRepository tiposRepository,
        IAtoNormativoRepository atosRepository,
        IPublicacoesUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tiposRepository);
        ArgumentNullException.ThrowIfNull(atosRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // AC6: sem versão do tipo vigente na data, não há de onde copiar os
        // atributos de consequência — recusa nomeada.
        TipoAtoPublicado? tipo = await tiposRepository
            .ObterVigenteAsync(command.TipoCodigo, command.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<RegistrarAtoNormativoResult>.Failure(new DomainError(
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"Não há versão vigente do tipo de ato '{command.TipoCodigo}' na data de publicação {command.DataPublicacao:yyyy-MM-dd}."));
        }

        // AC7: o par {id, hash} é recebido por valor, completo ou ausente.
        Result<ReferenciaVersaoConfiguracao>? versaoResult = ResolverVersaoInvocada(command);
        if (versaoResult is { IsFailure: true })
        {
            return Result<RegistrarAtoNormativoResult>.Failure(versaoResult.Error!);
        }

        AtoNormativo ato = AtoNormativo.Registrar(
            command.Orgao,
            command.Serie,
            command.Ano,
            command.Numero,
            command.TipoCodigo,
            tipo.CongelaConfiguracao,
            tipo.EfeitoIrreversivel,
            command.DataPublicacao,
            command.DocumentoHash,
            command.Assinante,
            timeProvider.GetUtcNow(),
            versaoResult?.Value);

        // AC4: colisão de numeração é aviso, jamais recusa. No registro o próprio
        // ato ainda não existe, então todos os que compartilham a numeração são
        // conflitantes. O aviso do POST é best-effort: como o número não tem
        // unicidade, dois registros concorrentes da mesma numeração podem ambos
        // observar zero conflitos e responder sem aviso — a consulta de detalhe
        // recomputa e enxerga ambos. Serializar aqui exigiria advisory lock para
        // um aviso que, por decisão de negócio, nunca bloqueia o registro.
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(atosRepository, ato, excluirId: null, cancellationToken)
            .ConfigureAwait(false);

        await atosRepository.AdicionarAsync(ato, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<RegistrarAtoNormativoResult>.Success(
            new RegistrarAtoNormativoResult(ato.Id, ato.RegistradoEm, avisos));
    }

    private static Result<ReferenciaVersaoConfiguracao>? ResolverVersaoInvocada(
        RegistrarAtoNormativoCommand command)
    {
        bool temId = command.VersaoInvocadaId.HasValue;
        bool temHash = command.VersaoInvocadaHash is not null;

        if (!temId && !temHash)
        {
            return null;
        }

        if (temId != temHash)
        {
            return Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta,
                AtoNormativoRegras.VersaoInvocadaIncompleta));
        }

        Result<ReferenciaVersaoConfiguracao> versao = ReferenciaVersaoConfiguracao.Criar(
            command.VersaoInvocadaId!.Value, command.VersaoInvocadaHash!);

        // O validator já rejeita id vazio e hash malformado; se ainda assim a
        // factory recusar (defesa em profundidade), traduz para um código mapeado
        // em vez de vazar o erro do value object como "erro não mapeado".
        return versao.IsFailure
            ? Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                AtoNormativoErrorCodes.VersaoInvocadaIncompleta, versao.Error!.Message))
            : versao;
    }
}
