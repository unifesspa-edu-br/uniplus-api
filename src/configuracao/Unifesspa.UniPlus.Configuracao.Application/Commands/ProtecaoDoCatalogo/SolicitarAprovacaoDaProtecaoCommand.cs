namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Envia à proteção de dados, de uma vez, os tipos de documento e fatos pendentes — novos e com a proteção
/// alterada — que quem envia escolheu, e só dos cadastros que pode manter (ADR-0153).
/// </summary>
/// <param name="Acesso">O que quem envia pode manter, decidido na borda.</param>
/// <param name="Destinatarios">Os e-mails de quem tem a permissão de aprovar, lidos na borda.</param>
/// <param name="Itens">Os itens a enviar, cada um com o cadastro (<c>TIPO_DOCUMENTO</c> ou <c>FATO</c>) e o identificador.</param>
public sealed record SolicitarAprovacaoDaProtecaoCommand(
    AcessoAoCatalogoDeProtecao Acesso,
    IReadOnlyList<string> Destinatarios,
    IReadOnlyList<ItemParaSolicitarDto?> Itens) : ICommand<Result<SolicitacaoDaProtecaoDto>>;
