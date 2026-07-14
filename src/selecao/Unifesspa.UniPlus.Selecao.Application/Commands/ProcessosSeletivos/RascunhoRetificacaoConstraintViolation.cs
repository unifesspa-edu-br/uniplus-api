namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// Traduz a violação do índice único da sessão editorial (ADR-0110 D3) para o mesmo
/// <see cref="DomainError"/> que a checagem em memória produz — e por isso o cliente vê
/// <b>409</b> nos dois caminhos, sem saber (nem precisar saber) qual dos dois o pegou.
/// </summary>
/// <remarks>
/// A checagem em memória (<c>Rascunho is not null</c>) recusa antes, no caso normal. Mas
/// duas aberturas concorrentes leem o agregado <b>sem</b> rascunho e passam ambas por ela —
/// e é o índice que decide. Sem esta tradução, a perdedora sairia como <b>500</b>: a
/// unicidade estaria garantida, e a mensagem, errada.
/// </remarks>
internal static class RascunhoRetificacaoConstraintViolation
{
    private const string ProcessoUnicoConstraint = "ux_rascunhos_retificacao_processo";

    public static DomainError? Traduzir(string? constraint) => constraint switch
    {
        ProcessoUnicoConstraint => new DomainError(
            "RascunhoRetificacao.JaAberta",
            "Já existe uma retificação em curso neste processo — feche-a ou descarte-a antes de abrir outra."),

        _ => null,
    };
}
