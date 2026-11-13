namespace Unifesspa.UniPlus.Selecao.API.Hateoas;

using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

internal sealed class IndicadoresDoProcessoLinksBuilder(LinkGenerator gerador) : IResourceLinksBuilder<IndicadoresDoProcessoDto>
{
    public IReadOnlyDictionary<string, string> Build(IndicadoresDoProcessoDto indicadores)
    {
        ArgumentNullException.ThrowIfNull(indicadores);
        object valores = new { id = indicadores.ProcessoSeletivoId };
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = gerador.GetPathByAction(nameof(IndicadoresDoProcessoController.Obter), "IndicadoresDoProcesso", valores)
                ?? throw new InvalidOperationException("Não foi possível obter o endereço dos indicadores."),
            ["processo"] = gerador.GetPathByAction(nameof(ProcessoSeletivoController.ObterPorId), "ProcessoSeletivo", valores)
                ?? throw new InvalidOperationException("Não foi possível obter o endereço do processo."),
        };
    }
}
