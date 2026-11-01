namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Envia à proteção de dados, de uma vez, todos os tipos de documento e fatos pendentes — novos e com a
/// proteção alterada — dos cadastros que quem envia pode manter (ADR-0153).
/// </summary>
/// <param name="Acesso">O que quem envia pode manter, decidido na borda.</param>
/// <param name="Destinatarios">Os e-mails de quem tem a permissão de aprovar, lidos na borda.</param>
public sealed record SolicitarAprovacaoDaProtecaoCommand(
    AcessoAoCatalogoDeProtecao Acesso,
    IReadOnlyList<string> Destinatarios) : ICommand<Result<SolicitacaoDaProtecaoDto>>;
