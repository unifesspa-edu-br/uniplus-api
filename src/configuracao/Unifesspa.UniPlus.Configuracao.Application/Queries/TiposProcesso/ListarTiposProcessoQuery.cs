namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// <paramref name="ApenasAtivos"/> tem default <c>true</c>: a leitura pública do
/// cadastro só expõe tipo ativo (UNI-REQ-0098). Somente a rota de manutenção o
/// desliga, para que plataforma-admin encontre o tipo desativado que quer reativar.
/// </summary>
public sealed record ListarTiposProcessoQuery(Guid? AfterId, int Limit, PaginationDirection Direction, bool ApenasAtivos = true)
    : IQuery<ListarTiposProcessoResult>;
