namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// O que uma mutação aceita da configuração devolve: o <c>ETag</c> <b>novo</b> da sessão
/// editorial (ADR-0110 D5).
/// </summary>
/// <remarks>
/// <para>
/// A revisão foi incrementada pela mutação que acabou de ser aceita, então o tag que o
/// cliente tinha em mãos <b>já não vale</b>. Devolvê-lo aqui é o que permite encadear a
/// próxima edição sem um <c>GET</c> no meio — e sem isso a sessão editorial teria um
/// round-trip a mais por dimensão alterada.
/// </para>
/// <para>
/// <see langword="null"/> quando o processo está em <c>Rascunho</c>: não há sessão
/// editorial, não há precondição a exigir, e não há tag a devolver. A obrigatoriedade do
/// <c>If-Match</c> é condicional ao estado do agregado (D5) — e é este <see langword="null"/>
/// que o transporte lê para decidir se escreve o header.
/// </para>
/// </remarks>
public sealed record MutacaoAceita(string? ETag);
