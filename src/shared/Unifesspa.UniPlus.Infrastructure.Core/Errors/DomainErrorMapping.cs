namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// A classificação pública de um erro de domínio: o status HTTP, o código do catálogo, o título,
/// e — para os conflitos — se repetir a mesma requisição pode agora dar certo.
/// </summary>
/// <param name="RetryableConflict">
/// Verdadeiro só quando a requisição <b>idêntica</b>, repetida, poderia agora ter outro desfecho:
/// o conflito descreve uma corrida que já passou, não um estado que permanece.
/// <para>
/// O default é falso, e a assimetria é deliberada. Classificar um conflito durável como
/// retentável faz a chave de idempotência liberar uma mutação que o cliente acredita ter
/// falhado — "código já existe" repetido depois que alguém liberou o código criaria o registro,
/// que é exatamente o que a chave existe para impedir. Classificar um transitório como durável
/// só preserva o comportamento de hoje. A direção perigosa exige declaração explícita.
/// </para>
/// </param>
public sealed record DomainErrorMapping(
    int Status,
    string Code,
    string Title,
    bool RetryableConflict = false);
