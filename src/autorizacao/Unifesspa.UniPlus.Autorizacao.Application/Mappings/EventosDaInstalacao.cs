namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Os eventos que a instalação acrescenta à trilha da concessão que ela gerou, montados na leitura a partir das colunas,
/// das tentativas e da exceção do termo de responsabilidade. Nunca trazem o código.
/// </summary>
public sealed class EventosDaInstalacao : IEventosDaOrigem
{
    // A exceção do termo é registrada pela própria confirmação, não por uma pessoa.
    private static readonly IdentidadeDto Sistema = new("uniplus:sistema", "instalacao");

    private readonly IInstalacaoRepository _repositorio;
    private readonly ITermoDeResponsabilidadeRepository _termos;

    public EventosDaInstalacao(IInstalacaoRepository repositorio, ITermoDeResponsabilidadeRepository termos)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(termos);

        _repositorio = repositorio;
        _termos = termos;
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
        TermoNaInstalacao? termo = await _termos.ObterExcecaoDaInstalacaoAsync(origemId, cancellationToken).ConfigureAwait(false);

        return Montar(instalacao, tentativas, termo);
    }

    private static IReadOnlyList<EventoDaTrilhaDto> Montar(
        Instalacao instalacao, IReadOnlyList<TentativaDeInstalacao> tentativas, TermoNaInstalacao? termo)
    {
        IdentidadeDto executor = new(instalacao.ExecutorEmissor, instalacao.ExecutorSub, instalacao.ExecutorNome);
        IEnumerable<EventoDaTrilhaDto> eventos =
        [
            new("instalacao_iniciada", instalacao.IniciadaEm, executor, $"Documento de designação nº {instalacao.NumeroDoDocumento}."),
            .. DoDocumento(instalacao, executor),
            .. DasTentativas(tentativas, executor),
            .. DoDesfecho(instalacao, executor),
            .. DoTermo(termo),
        ];

        return [.. eventos.OrderBy(e => e.Instante)];
    }

    private static IEnumerable<EventoDaTrilhaDto> DoDocumento(Instalacao instalacao, IdentidadeDto executor)
    {
        if (instalacao.DocumentoVerificadoEm is { } verificadoEm)
        {
            yield return new("documento_verificado", verificadoEm, executor, $"SHA-256 do PDF: {instalacao.DocumentoSha256}.");
        }

        if (instalacao.DocumentoRecusadoEm is { } recusadoEm)
        {
            yield return new("documento_recusado", recusadoEm, executor, "O verificador recusou o documento ou pediu o reenvio.");
        }

        if (instalacao.CodigoEnviadoEm is { } enviadoEm)
        {
            yield return new("codigo_enviado", enviadoEm, executor, "Código enviado ao e-mail institucional de quem executou.");
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DasTentativas(IReadOnlyList<TentativaDeInstalacao> tentativas, IdentidadeDto executor)
    {
        foreach (TentativaDeInstalacao reenvio in tentativas.Where(t => t.Resultado == ResultadoDaTentativaDeInstalacao.CodigoReenviado))
        {
            yield return new("codigo_reenviado", reenvio.Instante, executor, "Novo código enviado ao e-mail institucional de quem executou; o anterior deixou de valer.");
        }

        List<TentativaDeInstalacao> confirmacoes = [.. tentativas.Where(t => t.Resultado != ResultadoDaTentativaDeInstalacao.CodigoReenviado)];
        if (confirmacoes.Count > 0)
        {
            int erradas = confirmacoes.Count(t => t.Resultado == ResultadoDaTentativaDeInstalacao.CodigoErrado);
            yield return new(
                "tentativas", confirmacoes.Max(t => t.Instante), executor, $"{confirmacoes.Count} tentativa(s) de confirmação, {erradas} com código errado.");
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DoDesfecho(Instalacao instalacao, IdentidadeDto executor)
    {
        if (instalacao.ConfirmadaEm is { } confirmadaEm)
        {
            yield return new("instalacao_confirmada", confirmadaEm, executor, null);
        }

        if (instalacao.CanceladaEm is { } canceladaEm)
        {
            yield return new("instalacao_cancelada", canceladaEm, executor, null);
        }

        if (instalacao.Situacao == SituacaoDaInstalacao.Expirada && instalacao.CodigoExpiraEm is { } expiraEm)
        {
            yield return new("instalacao_expirada", expiraEm, executor, "O prazo do código esgotou.");
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DoTermo(TermoNaInstalacao? termo)
    {
        if (termo is null)
        {
            yield break;
        }

        string detalhe = termo.Situacao == SituacaoDoTermoNaInstalacao.SemTermoPublicado
            ? "Gestores instalados antes de existir termo de responsabilidade publicado."
            : $"Gestores instalados sem aceite do termo de responsabilidade vigente, versão {termo.Numero}.";
        yield return new("termo_excecao_da_instalacao", termo.RegistradaEm, Sistema, detalhe, "sistema");
    }
}
