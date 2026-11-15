namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O repositório de processos seletivos sobre o contexto do teste, com o relógio do sistema e a espera padrão pela
/// trava; o teste que prova a espera passa as opções do host. É o único ponto que monta o repositório: uma nova
/// dependência dele muda só aqui.
/// </summary>
internal static class ProcessoSeletivoRepositoryDeTeste
{
    public static ProcessoSeletivoRepository Criar(UniPlusDbContext context, IOptions<TravaDoProcessoOptions>? trava = null) =>
        new(context, TimeProvider.System, trava ?? Options.Create(new TravaDoProcessoOptions()));
}
