namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// <paramref name="ApenasAtivos"/> tem default <c>true</c>: a leitura pública do cadastro
/// só expõe tipo ativo (UNI-REQ-0098). Somente a rota de manutenção o desliga, para que
/// plataforma-admin consiga abrir o tipo desativado que pretende reativar.
/// </summary>
public sealed record ObterTipoProcessoPorIdQuery(Guid Id, bool ApenasAtivos = true) : IQuery<TipoProcessoDto?>;
