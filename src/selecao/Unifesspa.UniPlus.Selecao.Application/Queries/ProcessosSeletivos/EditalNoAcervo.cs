namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// O ato cujo registro a divulgação materializa, com o que o endereço do edital no acervo precisa
/// dele.
/// </summary>
/// <param name="Id">O ato que criou a versão divulgada.</param>
/// <param name="Ano">Ano do ato, o da numeração oficial.</param>
/// <param name="TipoCodigo">Código do tipo do ato.</param>
internal sealed record AtoDaDivulgacao(Guid Id, int Ano, string TipoCodigo);

/// <summary>
/// O documento do edital como o ato o publica no acervo público: de onde a cópia parte, onde ela
/// chega e com que nome se apresenta. A divulgação copia por aqui e o contrato público divulga o
/// endereço da mesma <see cref="Chave"/> — as duas pontas leem o mesmo valor.
/// </summary>
/// <param name="DocumentoEditalId">O documento confirmado que o edital congelou.</param>
/// <param name="HashSha256">Hash do documento, o que o edital congelou.</param>
/// <param name="Chave">A chave no acervo.</param>
/// <param name="NomeDeApresentacao">Nome com que o arquivo é salvo por quem o baixa.</param>
internal sealed record EditalNoAcervo(Guid DocumentoEditalId, string HashSha256, string Chave, string NomeDeApresentacao);
