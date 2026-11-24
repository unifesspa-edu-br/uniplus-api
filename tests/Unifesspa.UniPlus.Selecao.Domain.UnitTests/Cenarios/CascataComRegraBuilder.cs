namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class CascataComRegraBuilder
{
    internal static RegraCatalogo Regra(string declaracao)
    {
        using JsonDocument documento = JsonDocument.Parse(declaracao);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar("REMANEJAMENTO", "v1", TipoRegra.CriterioRemanejamento,
            documento.RootElement, invariantes.RootElement, "Base legal");
    }

    internal static ConfiguracaoCascataRemanejamento Cascata(RegraCatalogo regra) =>
        ConfiguracaoCascataRemanejamento.Criar(regra.Referencia(), "AC",
            [DestinoRemanejamento.Criar("LB_PPI", 1, "LB_Q").Value!]).Value!;
}
