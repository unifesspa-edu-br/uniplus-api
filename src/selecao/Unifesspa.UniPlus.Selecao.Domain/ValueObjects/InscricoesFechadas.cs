namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As inscrições estão fechadas: o motivo, que a lista "Minhas inscrições" informa à tela, e a recusa com o
/// código e a mensagem que as rotas do candidato devolvem. Quem confere o período marca o motivo de cada recusa;
/// ele nunca é deduzido do código do erro.
/// </summary>
public sealed record InscricoesFechadas(MotivoDasInscricoesFechadas Motivo, DomainError Recusa);
