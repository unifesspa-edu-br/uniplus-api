namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Gera o número de inscrição (issue #1858): uma sequência nativa do banco, formatada — nunca um
/// contador em memória nem uma coluna identity da tabela, porque <c>Inscricao.Submeter</c> precisa
/// do número já pronto ANTES de decidir se grava (o domínio não acessa o banco). Sem exigência de
/// não ter lacunas: uma submissão que consome um número e falha antes de persistir não o devolve.
/// </summary>
public interface IGeradorDeNumeroInscricao
{
    Task<string> ProximoAsync(CancellationToken cancellationToken = default);
}
