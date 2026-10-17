namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A identificação de uma <see cref="Entities.VersaoConfiguracao"/>, sem o documento congelado.</summary>
/// <param name="NumeroVersao">Número da versão, a partir de 1.</param>
/// <param name="VigenteAPartirDe">Instante, pelo relógio do sistema, em que a versão passou a valer.</param>
/// <param name="AtoCriadorId">O ato que criou a versão.</param>
/// <param name="AtoCriadorHash">Hash do documento do ato criador.</param>
/// <param name="AtoCriadorRetificaId">O ato que o ato criador retifica; nulo na primeira versão.</param>
public sealed record ResumoDaVersao(
    int NumeroVersao,
    DateTimeOffset VigenteAPartirDe,
    Guid AtoCriadorId,
    string AtoCriadorHash,
    Guid? AtoCriadorRetificaId);
