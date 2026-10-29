namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// A classificação pública de um erro de domínio: o status HTTP, o código do catálogo, o título,
/// e — para os conflitos — se repetir a mesma requisição pode agora dar certo.
/// </summary>
/// <param name="RetryableConflict">
/// Verdadeiro só quando a requisição <b>idêntica</b>, repetida, poderia agora ter outro desfecho:
/// o conflito descreve uma corrida que já passou, não um estado que permanece.
/// <para>
/// A declaração é um sinal ao cliente, publicado no envelope como <c>retryable: true</c>; o
/// filtro de idempotência não a lê, pois toda resposta 4xx produzida no MVC libera a chave. O
/// default é falso: a ausência do campo diz ao cliente que não conte com a repetição, e só o
/// conflito que descreve uma corrida já passada deve prometer o contrário.
/// </para>
/// </param>
/// <param name="RecusaAposGravacao">
/// Verdadeiro quando o handler já gravou estado ao devolver este erro: o pedido que volta ao
/// gestor, a tentativa de código registrada. A resposta é então armazenada pelo filtro de
/// idempotência e reproduzida no retry com a mesma chave, porque executar de novo repetiria a
/// gravação (ADR-0027, emenda "Status codes em cache"). Vale só para 4xx; o default é falso.
/// </param>
public sealed record DomainErrorMapping(
    int Status,
    string Code,
    string Title,
    bool RetryableConflict = false,
    bool RecusaAposGravacao = false);
