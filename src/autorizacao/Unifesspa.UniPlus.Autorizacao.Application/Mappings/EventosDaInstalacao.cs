namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Os eventos que a instalação acrescenta à trilha da concessão que ela gerou, montados na
/// leitura a partir das colunas e das tentativas. Nunca trazem o código.
/// </summary>
public sealed class EventosDaInstalacao : IEventosDaOrigem
{
    private readonly IInstalacaoRepository _repositorio;

    public EventosDaInstalacao(IInstalacaoRepository repositorio)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        _repositorio = repositorio;
    }

    public TipoDeOrigem Tipo => TipoDeOrigem.Instalacao;

    public async Task<IReadOnlyList<EventoDaTrilhaDto>?> ObterAsync(Guid origemId, CancellationToken cancellationToken)
    {
        Instalacao? instalacao = await _repositorio.ObterParaLeituraAsync(origemId, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            return null;
        }

        IReadOnlyList<TentativaDeInstalacao> tentativas = await _repositorio
            .ListarTentativasAsync(origemId, cancellationToken).ConfigureAwait(false);

        return Montar(instalacao, tentativas);
    }

    private static IReadOnlyList<EventoDaTrilhaDto> Montar(Instalacao instalacao, IReadOnlyList<TentativaDeInstalacao> tentativas)
    {
        ArgumentNullException.ThrowIfNull(instalacao);
        ArgumentNullException.ThrowIfNull(tentativas);

        IdentidadeDto executor = new(instalacao.ExecutorEmissor, instalacao.ExecutorSub);
        List<EventoDaTrilhaDto> eventos =
        [
            new("instalacao_iniciada", instalacao.IniciadaEm, executor, $"Documento de designação nº {instalacao.NumeroDoDocumento}."),
        ];

        if (instalacao.DocumentoVerificadoEm is { } verificadoEm)
        {
            eventos.Add(new("documento_verificado", verificadoEm, executor, $"SHA-256 do PDF: {instalacao.DocumentoSha256}."));
        }

        if (instalacao.DocumentoRecusadoEm is { } recusadoEm)
        {
            eventos.Add(new("documento_recusado", recusadoEm, executor, "O verificador recusou o documento ou pediu o reenvio."));
        }

        if (instalacao.CodigoEnviadoEm is { } enviadoEm)
        {
            eventos.Add(new("codigo_enviado", enviadoEm, executor, "Código enviado ao e-mail institucional de quem executou."));
        }

        eventos.AddRange(tentativas
            .Where(t => t.Resultado == ResultadoDaTentativaDeInstalacao.CodigoReenviado)
            .Select(t => new EventoDaTrilhaDto("codigo_reenviado", t.Instante, executor, "Novo código enviado ao e-mail institucional de quem executou; o anterior deixou de valer.")));

        List<TentativaDeInstalacao> confirmacoes = [.. tentativas.Where(t => t.Resultado != ResultadoDaTentativaDeInstalacao.CodigoReenviado)];
        if (confirmacoes.Count > 0)
        {
            int erradas = confirmacoes.Count(t => t.Resultado == ResultadoDaTentativaDeInstalacao.CodigoErrado);
            eventos.Add(new(
                "tentativas",
                confirmacoes.Max(t => t.Instante),
                executor,
                $"{confirmacoes.Count} tentativa(s) de confirmação, {erradas} com código errado."));
        }

        if (instalacao.ConfirmadaEm is { } confirmadaEm)
        {
            eventos.Add(new("instalacao_confirmada", confirmadaEm, executor, null));
        }

        if (instalacao.CanceladaEm is { } canceladaEm)
        {
            eventos.Add(new("instalacao_cancelada", canceladaEm, executor, null));
        }

        if (instalacao.Situacao == SituacaoDaInstalacao.Expirada && instalacao.CodigoExpiraEm is { } expiraEm)
        {
            eventos.Add(new("instalacao_expirada", expiraEm, executor, "O prazo do código esgotou."));
        }

        return [.. eventos.OrderBy(e => e.Instante)];
    }
}
