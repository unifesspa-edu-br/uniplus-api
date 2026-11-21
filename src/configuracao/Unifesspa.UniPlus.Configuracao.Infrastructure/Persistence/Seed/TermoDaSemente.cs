namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Um termo de consentimento que uma semente grava, com a versão promovida que o modelo cita. O código do termo é
/// formado do nome pelo domínio, ao gravar.
/// </summary>
/// <param name="TermoId">O identificador fixo do termo.</param>
/// <param name="VersaoId">O identificador fixo da versão promovida.</param>
/// <param name="Nome">O nome do termo.</param>
/// <param name="Texto">O texto do termo, proposta que a tela confirma.</param>
/// <param name="BaseLegal">A base legal do tratamento que o termo autoriza.</param>
public sealed record TermoDaSemente(Guid TermoId, Guid VersaoId, string Nome, string Texto, string BaseLegal);
