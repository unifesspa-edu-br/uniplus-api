namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere no catálogo de Publicações, ANTES de publicar, o tipo de ato que o operador
/// declarou (ADR-0056 — leitura cross-módulo).
/// </summary>
/// <remarks>
/// <para>
/// O registro do ato recebe os atributos do tipo já conferidos, por valor: o que o catálogo recusaria tem de virar 422
/// aqui, antes de qualquer escrita.
/// </para>
/// <para>
/// A regra de fundo não é sobre rótulos, e sim sobre o que o ato FAZ: publicar e retificar
/// um Processo Seletivo <b>congelam a configuração</b> numa nova
/// <c>VersaoConfiguracao</c> (congelamento dos parâmetros na publicação, UNI-REQ-0019). Um ato que declara não congelar não pode ser o criador
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
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);

        Result<TipoAtoPublicadoView> vigente = await VigenteAsync(reader, ato.TipoAtoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return vigente;
        }

        return vigente.Value!.CongelaConfiguracao
            ? vigente
            : Result<TipoAtoPublicadoView>.Failure(new DomainError(
                "ProcessoSeletivo.TipoDeAtoNaoCongelaConfiguracao",
                $"O tipo de ato '{ato.TipoAtoCodigo}' não congela configuração, e publicar ou retificar um Processo Seletivo congela — o ato que cria a versão da configuração precisa ser de um tipo congelante."));
    }

    /// <summary>
    /// A versão do tipo que vigora no dia civil da publicação, no fuso institucional, ou a recusa de que não há. A
    /// data que o documento declara não escolhe o tipo.
    /// </summary>
    public static async Task<Result<TipoAtoPublicadoView>> VigenteAsync(
        ITipoAtoPublicadoReader reader,
        string tipoAtoCodigo,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        TipoAtoPublicadoView? tipo = await reader
            .ObterVigenteAsync(tipoAtoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);

        return tipo is null
            ? Result<TipoAtoPublicadoView>.Failure(new DomainError(
                "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
                $"Não há versão vigente do tipo de ato '{tipoAtoCodigo}' na data da publicação ({diaDaPublicacao:yyyy-MM-dd})."))
            : Result<TipoAtoPublicadoView>.Success(tipo);
    }
}
