namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Kernel.Results;

/// <summary>
/// Descarta a sessão editorial (Story #861, ADR-0110): <b>repõe a configuração congelada</b>
/// e encerra a sessão — na mesma transação.
/// </summary>
/// <remarks>
/// <para>
/// <b>A ordem dos dois passos é a coisa inteira.</b> Enquanto a sessão existe, os seis
/// <c>Definir*</c> escrevem <b>direto na configuração viva</b> — não há staging, e todo
/// <c>Definir*</c> substitui a coleção inteira (as relações EF são <c>Cascade</c>), de modo
/// que <b>não existe desfazer incremental</b>. Encerrar a sessão sem repor devolveria o
/// certame ao estado "publicado normal" servindo, em silêncio, uma configuração que
/// <b>nunca foi publicada</b>.
/// </para>
/// <para>
/// A reposição é a da S2, e ela <b>prova o que repôs</b>: decodifica o envelope congelado,
/// repõe numa sombra destacada, recanonicaliza com o encoder <b>daquela</b> versão e compara
/// byte a byte com os bytes congelados — só então toca a raiz viva. Uma prova que falha
/// devolve <c>Failure</c> antes de qualquer <c>SaveChanges</c>.
/// </para>
/// <para>
/// <b>Zero versão, zero evento, zero ato.</b> Descartar não é um fato publicável: o mundo
/// nunca soube da edição, e continua sem saber.
/// </para>
/// </remarks>
public static class DescartarRetificacaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRestauradorDeConfiguracao restaurador,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(restaurador);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // O If-Match é INCONDICIONAL nesta rota — ela existe para a sessão. A ausência da
        // precondição é falha de protocolo, e é respondida ANTES da checagem de existência do
        // rascunho (ADR-0110 D9, "3 antes de 10").
        if (!command.Precondicao.Presente)
        {
            return Result.Failure(new DomainError(
                "Precondicao.Requerida",
                "O descarte encerra a sessão editorial em curso — informe o If-Match com o ETag dela."));
        }

        if (processo.Rascunho is null)
        {
            return Result.Failure(new DomainError(
                "RascunhoRetificacao.NaoAberta",
                "Não há retificação em curso neste processo."));
        }

        // A precondição é conferida AQUI — ANTES da restauração, e não depois.
        //
        // O `DescartarRetificacao` do agregado a reconfere (defesa em profundidade), mas
        // chegar até lá significaria ter REPOSTO a configuração para só então descobrir que
        // o cliente mandou um ETag defasado. O Failure não persistiria nada hoje, é verdade —
        // mas deixaria o agregado *tracked* já reidratado, e qualquer SaveChanges adiante no
        // mesmo escopo gravaria uma restauração que ninguém autorizou. A atomicidade passaria
        // a depender de o handler lembrar de não salvar; é exatamente a disciplina informal
        // que a Feature existe para não precisar.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Guid versaoBaseId = processo.Rascunho.VersaoBaseId;

        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        // A base do rascunho TEM de ser o topo da cadeia. Ela é, hoje, por invariante: o
        // atalho atômico recusa com sessão aberta (D7), publicar exige Rascunho, e o
        // fechamento — o único outro caminho que sucede a cadeia — encerra a sessão na mesma
        // transação. Somado ao FOR UPDATE, a cadeia é IMÓVEL enquanto a sessão existe.
        //
        // A conferência é fail-closed contra o dia em que essa invariante for quebrada por
        // um caminho novo: restaurar a versão ERRADA repõe no certame uma configuração que
        // não é a que ele publicou — e o round-trip passaria, porque os bytes daquela versão
        // batem com ela mesma. A prova de fidelidade não protege contra restaurar a versão
        // errada; só esta checagem protege.
        if (versaoAtual is null || versaoAtual.Id != versaoBaseId)
        {
            return Result.Failure(new DomainError(
                "RascunhoRetificacao.BaseDesatualizada",
                "A versão sobre a qual esta retificação foi aberta não é mais o topo da cadeia de configuração — "
                + "a sessão não pode ser descartada com segurança."));
        }

        // Repõe E PROVA (round-trip byte a byte com o encoder daquela versão). Falha aqui não
        // deixa resíduo: a prova roda numa sombra destacada, antes de a raiz viva ser tocada.
        Result restauracao = restaurador.Restaurar(processo, versaoAtual);
        if (restauracao.IsFailure)
        {
            return restauracao;
        }

        // Só agora — com a configuração já de volta ao que o documento publicado diz.
        Result descarte = processo.DescartarRetificacao(command.Precondicao);
        if (descarte.IsFailure)
        {
            return descarte;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
