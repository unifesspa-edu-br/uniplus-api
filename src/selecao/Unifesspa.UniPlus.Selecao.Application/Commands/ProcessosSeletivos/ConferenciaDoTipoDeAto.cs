namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere no catálogo de Publicações, ANTES de publicar, o tipo de ato que o operador
/// declarou (ADR-0056 — leitura cross-módulo).
/// </summary>
/// <remarks>
/// <para>
/// O ato é registrado depois, por mensagem durável (ADR-0108). Sem esta conferência, o que
/// o catálogo recusaria só apareceria no consumo da fila: o Edital já publicado, o cliente
/// já com o 204 na mão, e a recusa virando dead letter. O que dá para recusar agora, com
/// 422, não pode virar incidente operacional depois.
/// </para>
/// <para>
/// A regra de fundo não é sobre rótulos, e sim sobre o que o ato FAZ: publicar e retificar
/// um Processo Seletivo <b>congelam a configuração</b> numa nova
/// <c>VersaoConfiguracao</c> (RN08). Um ato que declara não congelar não pode ser o criador
/// dela — o ato diria uma coisa e a versão provaria outra. E a incoerência não ficaria
/// parada: a retificação seguinte, com um tipo congelante, seria recusada por classe
/// divergente (ADR-0103), deixando a retificação publicada sem ato.
/// </para>
/// <para>
/// Nenhum código aqui compara o código do tipo com um literal: pergunta-se ao catálogo se
/// ele congela. Acrescentar um tipo de ato continua sendo linha de cadastro (ADR-0103).
/// </para>
/// </remarks>
internal static class ConferenciaDoTipoDeAto
{
    public static async Task<Result<TipoAtoPublicadoView>> CongelaConfiguracaoAsync(
        ITipoAtoPublicadoReader reader,
        DadosDoAto ato,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(ato);

        TipoAtoPublicadoView? tipo = await reader
            .ObterVigenteAsync(ato.TipoAtoCodigo, ato.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            return Result<TipoAtoPublicadoView>.Failure(new DomainError(
                "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
                $"Não há versão vigente do tipo de ato '{ato.TipoAtoCodigo}' na data de publicação declarada ({ato.DataPublicacao:yyyy-MM-dd})."));
        }

        if (!tipo.CongelaConfiguracao)
        {
            return Result<TipoAtoPublicadoView>.Failure(new DomainError(
                "ProcessoSeletivo.TipoDeAtoNaoCongelaConfiguracao",
                $"O tipo de ato '{ato.TipoAtoCodigo}' não congela configuração, e publicar ou retificar um Processo Seletivo congela — o ato que cria a versão da configuração precisa ser de um tipo congelante."));
        }

        return Result<TipoAtoPublicadoView>.Success(tipo);
    }

    /// <summary>
    /// Confere a vaga que a linhagem reserva sobre o certame (ADR-0107), quando o tipo é
    /// único por objeto.
    /// </summary>
    /// <remarks>
    /// A vaga é monotônica: ocupada, nunca se libera. Se já estiver tomada por OUTRA
    /// linhagem — por um ato registrado pelo endpoint administrativo, por exemplo —, o
    /// registro seria recusado no consumo da fila, e o certame ficaria publicado sem ato.
    /// A recusa tem de vir agora, com 422.
    /// <para>
    /// <paramref name="atosDaPropriaLinhagem"/> são os atos que criaram as versões deste
    /// certame: uma retificação NÃO disputa a vaga que a sua própria linhagem já ocupa. Na
    /// abertura a lista é vazia, e o objeto tem de estar livre.
    /// </para>
    /// <para>
    /// A pergunta é feita sobre o HISTÓRICO de atos, e não só sobre a tabela de vagas, porque
    /// <c>unico_por_objeto</c> é editável: um ato registrado quando o tipo ainda não era único
    /// não reservou vaga, e mesmo assim conflita se o tipo passar a ser único depois. É a
    /// mesma checagem que o registro faz.
    /// </para>
    /// </remarks>
    public static async Task<Result> VagaDoObjetoAsync(
        IVagaDeLinhagemReader reader,
        TipoAtoPublicadoView tipo,
        Guid processoSeletivoId,
        IReadOnlyList<Guid> atosDaPropriaLinhagem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(atosDaPropriaLinhagem);

        if (!tipo.UnicoPorObjeto)
        {
            return Result.Success();
        }

        bool ocupadaPorOutro = await reader
            .ObjetoJaTemAtoDeOutraLinhagemAsync(
                MensagensDaPublicacao.EntidadeProcessoSeletivo,
                processoSeletivoId,
                tipo.Codigo,
                atosDaPropriaLinhagem,
                cancellationToken)
            .ConfigureAwait(false);

        if (!ocupadaPorOutro)
        {
            return Result.Success();
        }

        return Result.Failure(new DomainError(
            "ProcessoSeletivo.ObjetoJaTemAtoVivoDoTipo",
            $"Este Processo Seletivo já tem um ato vivo do tipo '{tipo.Codigo}', de outra linhagem. O tipo admite um único ato vivo por objeto, e a vaga não se libera."));
    }
}
