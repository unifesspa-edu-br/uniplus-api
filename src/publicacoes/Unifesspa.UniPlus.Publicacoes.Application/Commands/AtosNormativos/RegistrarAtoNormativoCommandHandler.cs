namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using System.Globalization;

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

        // ADR-0103: quando o registro emenda outro ato, as invariantes da cadeia que
        // dependem do ato retificado (existência, classe de congelamento coincidente,
        // linearidade) são verificadas aqui, com o retificado em mãos.
        DomainError? retificacaoErro = await ValidarRetificacaoAsync(
            command, tipo, atosRepository, cancellationToken).ConfigureAwait(false);
        if (retificacaoErro is not null)
        {
            return Result<RegistrarAtoNormativoResult>.Failure(retificacaoErro);
        }

        AtoNormativo ato = AtoNormativo.Registrar(
            command.Orgao,
            command.Serie,
            command.Ano,
            command.Numero,
            command.TipoCodigo,
            tipo.CongelaConfiguracao,
            tipo.EfeitoIrreversivel,
            tipo.UnicoPorObjeto,
            command.DataPublicacao,
            command.DocumentoHash,
            command.Assinante,
            timeProvider.GetUtcNow(),
            versaoResult?.Value,
            command.AtoRetificadoId,
            command.MotivoRetificacao);

        // AC4: colisão de numeração é aviso, jamais recusa. No registro o próprio ato
        // ainda não existe, então todos os que compartilham a numeração são
        // conflitantes — menos a linhagem que este ato passa a integrar: uma
        // republicação com o mesmo número dentro da cadeia de retificação não é
        // duplicata, é a mesma linhagem (ADR-0103). Exclui a cadeia inteira do ato
        // retificado, não só o pai direto. O aviso é best-effort: como o número não
        // tem unicidade, dois registros concorrentes da mesma numeração podem ambos
        // observar zero conflitos; a consulta de detalhe recomputa e enxerga ambos.
        IReadOnlyCollection<Guid> excluirDoAviso = command.AtoRetificadoId is { } retificadoId
            ? await atosRepository.ListarIdsDaCadeiaAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : [];
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(atosRepository, ato, excluirDoAviso, cancellationToken)
            .ConfigureAwait(false);

        await atosRepository.AdicionarAsync(ato, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsLinearidadeConflict(constraint))
        {
            // Corrida check-then-act: dois atos tentaram retificar a mesma raiz ao
            // mesmo tempo. O índice único parcial deixou passar só um; o outro vira
            // recusa nomeada, não erro 500.
            return Result<RegistrarAtoNormativoResult>.Failure(new DomainError(
                AtoNormativoErrorCodes.RaizJaRetificada,
                RaizJaRetificadaConcorrenteMensagem(command.AtoRetificadoId)));
        }

        return Result<RegistrarAtoNormativoResult>.Success(
            new RegistrarAtoNormativoResult(ato.Id, ato.RegistradoEm, avisos));
    }

    /// <summary>
    /// Verifica as invariantes da cadeia de retificação que dependem do ato
    /// retificado (ADR-0103). Devolve o <see cref="DomainError"/> da primeira
    /// violação, ou <see langword="null"/> quando o registro não é retificação ou
    /// passa em todas. A garantia dura da linearidade contra a corrida é do índice
    /// único parcial; esta consulta dá a mensagem legível no caso comum.
    /// </summary>
    private static async Task<DomainError?> ValidarRetificacaoAsync(
        RegistrarAtoNormativoCommand command,
        TipoAtoPublicado tipo,
        IAtoNormativoRepository atosRepository,
        CancellationToken cancellationToken)
    {
        if (command.AtoRetificadoId is not { } retificadoId)
        {
            return null;
        }

        AtoNormativo? retificado = await atosRepository
            .ObterPorIdParaLeituraAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);
        if (retificado is null)
        {
            return new DomainError(
                AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "O ato retificado {0} não corresponde a nenhum ato registrado.",
                    retificadoId));
        }

        // congela(retificador) == congela(retificado): a classe de congelamento do
        // novo ato é a do tipo vigente já resolvido; a do retificado é o snapshot que
        // ele congelou no próprio registro. É essa classe, não o rótulo do tipo, que
        // protege a integridade da configuração publicada (RN08).
        if (retificado.CongelaConfiguracao != tipo.CongelaConfiguracao)
        {
            return new DomainError(
                AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente,
                "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado: "
                + "um ato não congelante não emenda um congelante, nem o inverso.");
        }

        // Linearidade: se a raiz já foi retificada, a nova retificação deveria emendar
        // a cabeça da cadeia, não a raiz. Nomeia quem já a retificou.
        AtoNormativo? retificador = await atosRepository
            .ObterRetificadorAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);
        if (retificador is not null)
        {
            return new DomainError(
                AtoNormativoErrorCodes.RaizJaRetificada,
                RaizJaRetificadaMensagem(retificadoId, retificador));
        }

        return null;
    }

    private static string RaizJaRetificadaMensagem(Guid retificadoId, AtoNormativo retificador)
    {
        string numero = retificador.Numero is null ? "sem número" : "nº " + retificador.Numero;
        return string.Format(
            CultureInfo.InvariantCulture,
            "O ato {0} já foi retificado pelo ato {1} ({2} {3}/{4}). A cadeia é linear: "
            + "retifique a cabeça da cadeia, não uma raiz já retificada.",
            retificadoId,
            retificador.Id,
            retificador.Serie,
            numero,
            retificador.Ano);
    }

    private static string RaizJaRetificadaConcorrenteMensagem(Guid? retificadoId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "O ato {0} já foi retificado por outro ato registrado concorrentemente. "
            + "A cadeia é linear: retifique a cabeça da cadeia.",
            retificadoId);

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
