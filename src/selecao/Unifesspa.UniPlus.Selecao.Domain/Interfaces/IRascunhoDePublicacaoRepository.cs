namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="RascunhoDePublicacao"/> — próprio, e não uma coleção dentro de
/// <see cref="IProcessoSeletivoRepository"/>, porque o rascunho não é entidade filha do
/// agregado (ver comentário da entidade).
/// </summary>
public interface IRascunhoDePublicacaoRepository : IRepository<RascunhoDePublicacao>
{
    /// <summary>
    /// O rascunho daquele operador naquele processo, ou <see langword="null"/>. É o único
    /// acesso de leitura que existe: rascunho tem dono, e não há caso de uso que liste os
    /// rascunhos alheios de um processo.
    /// </summary>
    Task<RascunhoDePublicacao?> ObterDoOperadorAsync(
        Guid processoSeletivoId,
        string usuarioSub,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga todos os rascunhos do processo, de qualquer operador, em um comando só.
    /// </summary>
    /// <remarks>
    /// É o que todo caminho que registra o ato chama. Deleta direto no banco, sem passar pelo
    /// change tracker: são linhas que ninguém leu nesta transação, e carregá-las só para
    /// marcá-las como removidas traria o conteúdo — o bloco transcrito do Diário Oficial — para
    /// dentro da memória do processo sem nenhuma razão para isso.
    /// </remarks>
    Task<int> ApagarDoProcessoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga o rascunho daquele operador naquele processo, se houver.
    /// </summary>
    /// <remarks>
    /// Escreve direto no banco em vez de carregar a linha para marcá-la como removida: trazer
    /// o conteúdo — o bloco transcrito do Diário Oficial — para a memória do processo só para
    /// destruí-lo não tem serventia nenhuma.
    /// </remarks>
    Task<int> ApagarDoOperadorAsync(
        Guid processoSeletivoId,
        string usuarioSub,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga o rascunho <b>somente se ele ainda estiver vencido</b> no instante da escrita.
    /// </summary>
    /// <remarks>
    /// A condição não é redundante com a conferência de quem chama. Entre a leitura que
    /// constatou o vencimento e este comando, o próprio operador pode ter gravado de outra aba
    /// e renovado o prazo — apagar por um veredito já obsoleto destruiria justamente o trabalho
    /// que o rascunho existe para preservar.
    /// </remarks>
    Task<int> ApagarSeVencidoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga <b>todos</b> os rascunhos vencidos, de qualquer processo e de qualquer operador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a varredura que o prazo precisa para valer de verdade. A expiração na leitura só
    /// alcança o que alguém volta a abrir — e o rascunho perigoso é justamente o contrário: o do
    /// certame que foi abandonado no meio, que ninguém lê e que guardaria o nome de quem
    /// assinaria o ato por tempo indeterminado.
    /// </para>
    /// <para>
    /// Roda a cada gravação de rascunho, e não num job: não há infraestrutura de tarefa
    /// recorrente neste repositório (todos os <c>IHostedService</c> são de partida única), e
    /// prometer um prazo que dependesse de infraestrutura inexistente seria prometer nada. A
    /// tabela é pequena e o índice por vencimento torna a varredura barata; basta que alguém,
    /// em algum processo, ainda esteja salvando rascunhos para que os abandonados saiam.
    /// </para>
    /// </remarks>
    /// <param name="exceto">
    /// O rascunho que está sendo gravado nesta mesma transação, que a varredura precisa poupar.
    /// Sem isso, o dono que volta a um rascunho vencido perde justamente o que veio salvar: a
    /// renovação do prazo vive na entidade rastreada e só chega ao banco no <c>SaveChanges</c>,
    /// enquanto a varredura roda como SQL imediato e ainda enxerga a data velha — apagaria a
    /// linha, e o <c>SaveChanges</c> seguinte falharia tentando atualizar o que não existe.
    /// </param>
    Task<int> ApagarVencidosAsync(
        DateTimeOffset agora,
        Guid? exceto = null,
        CancellationToken cancellationToken = default);
}
