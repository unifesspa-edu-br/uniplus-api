namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Um evento de manifestação técnica sobre a proteção de um tipo de documento ou fato do candidato (UNI-REQ-0373). O
/// nome de quem se manifestou é o que o token trazia no instante; o identificador da pessoa fica registrado e não sai
/// na resposta.
/// </summary>
/// <param name="Ciclo">O ciclo de aprovação a que a manifestação pertence.</param>
/// <param name="Parecer">Token canônico: <c>ADEQUADA</c>, <c>RECOMENDA_AJUSTE</c> ou <c>INADEQUADA</c>.</param>
/// <param name="Texto">O texto da manifestação; sempre presente nos pareceres recomenda ajuste e inadequada.</param>
/// <param name="AutorNome">Nome de quem se manifestou; nulo quando o token não trazia nome.</param>
/// <param name="SubstituiId">A manifestação que esta substituiu; nulo no registro inicial.</param>
/// <param name="Situacao">
/// <c>VIGENTE</c> (do ciclo corrente e não substituída), <c>SUBSTITUIDA</c> (outra manifestação da mesma pessoa a
/// substituiu) ou <c>ENCERRADA</c> (de ciclo encerrado e não substituída).
/// </param>
public sealed record ManifestacaoDaProtecaoDto(
    Guid Id,
    int Ciclo,
    string Parecer,
    string? Texto,
    string? AutorNome,
    DateTimeOffset Instante,
    Guid? SubstituiId,
    string Situacao);
