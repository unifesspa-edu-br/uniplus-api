namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lista as reclassificações com concessões que aguardam a etapa: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>.</summary>
public sealed record ListarReclassificacoesQuery(string? Etapa) : IQuery<Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>>;

/// <summary>O detalhe da reclassificação: as permissões de cada versão em uso, o documento e as concessões suspensas.</summary>
public sealed record ObterReclassificacaoQuery(Guid Id) : IQuery<Result<ReclassificacaoDto>>;

/// <summary>As divergências da governança, da mais recente para a mais antiga.</summary>
public sealed record ListarDivergenciasDaGovernancaQuery : IQuery<Result<IReadOnlyList<DivergenciaDaGovernancaDto>>>;
