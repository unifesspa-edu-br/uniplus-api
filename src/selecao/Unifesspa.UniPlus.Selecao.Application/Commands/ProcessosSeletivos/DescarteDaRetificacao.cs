namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// O descarte da sessão editorial de um processo já travado e com a precondição conferida: prova a reposição da
/// configuração congelada, repõe e encerra a sessão, apagando o que ela deixou para trás. A reposição se prova antes de
/// tocar a raiz viva: decodifica o envelope congelado, repõe numa sombra destacada, recanonicaliza com o encoder daquela
/// versão e compara byte a byte com os bytes congelados. Uma prova que falha devolve <c>Failure</c> antes de qualquer
/// gravação.
/// </summary>
public sealed class DescarteDaRetificacao(
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRestauradorDeConfiguracao restaurador,
    IRascunhoDePublicacaoRepository rascunhoDePublicacaoRepository,
    IMotivoDoProcessoRepository motivoDoProcessoRepository,
    IUnitOfWork unitOfWork)
{
    internal async Task<Result> DescartarAsync(ProcessoSeletivo processo, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        // O descarte da sessão anula o rascunho do processo: a versão em preparo é lida antes.
        int versaoEmPreparo = processo.Rascunho!.NumeroDaVersaoEmPreparo;
        Result<ReposicaoProvada> prova = await ProvarAsync(processo, cancellationToken).ConfigureAwait(false);
        if (prova.IsFailure)
        {
            return Result.Failure(prova.Error!);
        }

        await ReporAVersaoCongeladaAsync(processo, prova.Value!, cancellationToken).ConfigureAwait(false);
        await EncerrarAsync(processo, precondicao, versaoEmPreparo, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<ReposicaoProvada>> ProvarAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> baseDaSessao = await BaseDaSessaoAsync(processo, cancellationToken).ConfigureAwait(false);
        if (baseDaSessao.IsFailure)
        {
            return Result<ReposicaoProvada>.Failure(baseDaSessao.Error!);
        }

        Result<GrafoConfiguracao> grafo = restaurador.Restaurar(processo, baseDaSessao.Value!);
        return grafo.IsFailure
            ? Result<ReposicaoProvada>.Failure(grafo.Error!)
            : Result<ReposicaoProvada>.Success(new ReposicaoProvada(baseDaSessao.Value!, grafo.Value!));
    }

    /// <summary>
    /// A versão sobre a qual a sessão foi aberta, que tem de ser o topo da cadeia. Ela é, por invariante: o atalho atômico
    /// recusa com sessão aberta, publicar exige rascunho, e o fechamento — o único outro caminho que sucede a cadeia —
    /// encerra a sessão na mesma transação. Somado ao bloqueio da linha do processo, a cadeia é imóvel enquanto a sessão
    /// existe. A conferência falha fechada contra o dia em que um caminho novo quebrar a invariante: restaurar a versão
    /// errada repõe no certame uma configuração que ele não publicou, e a prova de fidelidade passaria, porque os bytes
    /// daquela versão batem com ela mesma.
    /// </summary>
    private async Task<Result<VersaoConfiguracao>> BaseDaSessaoAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository.ObterVersaoAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);

        return versaoAtual is null || versaoAtual.Id != processo.Rascunho!.VersaoBaseId
            ? Result<VersaoConfiguracao>.Failure(new DomainError(
                "RascunhoRetificacao.BaseDesatualizada",
                "A versão sobre a qual esta retificação foi aberta não é mais o topo da cadeia de configuração — "
                + "a sessão não pode ser descartada com segurança."))
            : Result<VersaoConfiguracao>.Success(versaoAtual);
    }

    /// <summary>
    /// A sessão pode ter editado fatos e regras, inclusive trocado ordens, e repor as instâncias congeladas por cima das
    /// vivas colidiria no índice único de ordem e código na mesma transação: limpa as duas coleções e grava, para que os
    /// DELETEs saiam antes dos INSERTs das congeladas. Daqui em diante nenhum caminho devolve <c>Failure</c>, porque o
    /// Wolverine confirma a transação ambiente no fim normal do handler, inclusive num <c>Failure</c>, e gravaria o DELETE
    /// sem o INSERT correspondente. Uma falha depois da gravação é defeito, já que a sombra provou o mesmo grafo, e lança
    /// para desfazer a transação.
    /// </summary>
    private async Task ReporAVersaoCongeladaAsync(
        ProcessoSeletivo processo, ReposicaoProvada reposicao, CancellationToken cancellationToken)
    {
        processo.LimparColetaEDerivacaoParaRestauracao();
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        Result aplicada = processo.RestaurarConfiguracaoCongelada(reposicao.Versao, reposicao.Grafo);
        if (aplicada.IsFailure)
        {
            throw new InvalidOperationException(
                $"A restauração da versão {reposicao.Versao.NumeroVersao} foi provada na sombra mas falhou ao aplicar na "
                + $"raiz viva ({aplicada.Error!.Code}) — estado inconsistente após a gravação, revertendo a transação.");
        }
    }

    /// <summary>
    /// A sessão abandonada e a configuração congelada reposta: não haverá ato de retificação, e o rascunho de publicação
    /// que existia para ele deixa de ter destino. Apagá-lo é o que impede o nome do assinante de sobreviver a uma
    /// retificação que não aconteceu; os motivos que a sessão incluiu saem com ela pelo mesmo motivo.
    /// </summary>
    private async Task EncerrarAsync(ProcessoSeletivo processo, PrecondicaoIfMatch precondicao, int versaoEmPreparo, CancellationToken cancellationToken)
    {
        Result descarte = processo.DescartarRetificacao(precondicao);
        if (descarte.IsFailure)
        {
            throw new InvalidOperationException(
                $"O descarte falhou após a configuração congelada já ter sido reaplicada ({descarte.Error!.Code}) — "
                + "estado inconsistente após a gravação, revertendo a transação.");
        }

        await rascunhoDePublicacaoRepository.RemoverDoProcessoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        await RemoverMotivosPendentesAsync(processo.Id, versaoEmPreparo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoverMotivosPendentesAsync(Guid processoId, int versaoEmPreparo, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhaAtual = await motivoDoProcessoRepository.ListarEscolhaAtualAsync(processoId, cancellationToken).ConfigureAwait(false);
        foreach (MotivoDoProcesso pendente in escolhaAtual.Where(m => m.PendenteNa(versaoEmPreparo)))
        {
            motivoDoProcessoRepository.Remover(pendente);
        }
    }

    /// <summary>A versão a repor e o grafo congelado dela, cuja fidelidade a sombra já provou.</summary>
    private sealed record ReposicaoProvada(VersaoConfiguracao Versao, GrafoConfiguracao Grafo);
}
