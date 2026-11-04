namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da cascata de remanejamento entre modalidades.
/// </summary>
internal static class LeituraDaCascata
{
    /// <summary>
    /// A cascata de remanejamento tem <b>duas formas fechadas</b> — mesmo
    /// raciocínio de <see cref="LeituraDoBonusEAtendimento.LerBonusRegional"/>: <c>{"presente": false}</c> e a completa.
    /// A forma (cascata de remanejamento de cotas, UNI-REQ-0056) é reconferida pela factory de <see cref="ConfiguracaoCascataRemanejamento"/>
    /// ao reconstruir — o decoder não a duplica, só extrai os valores tipados.
    /// </summary>
    internal static ConfiguracaoCascataRemanejamento? LerCascataRemanejamento(LeitorEnvelope leitor, JsonObject payload)
    {
        if (leitor.ObjetoSePresente(payload, "cascataRemanejamento") is not { } bloco)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "cascataRemanejamento", "presente", "regra", "fallbackCodigo", "ordens");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "cascataRemanejamento", RegraRemanejamentoCodigo.Cascata);
        string fallbackCodigo = leitor.Texto(bloco, "fallbackCodigo", "cascataRemanejamento", LimitesDoEnvelope.ModalidadeCodigo);
        JsonArray ordens = leitor.Array(bloco, "ordens", "cascataRemanejamento");
        if (leitor.Falhou)
        {
            return null;
        }

        List<DestinoRemanejamento>? destinos = LerDestinos(leitor, ordens);
        if (destinos is null || leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoCascataRemanejamento> cascata = ConfiguracaoCascataRemanejamento.Criar(regra, fallbackCodigo, destinos);
        return cascata.IsFailure ? leitor.Propagar<ConfiguracaoCascataRemanejamento>(cascata.Error!) : cascata.Value;
    }

    private static List<DestinoRemanejamento>? LerDestinos(LeitorEnvelope leitor, JsonArray ordens)
    {
        List<DestinoRemanejamento> destinos = [];
        for (int i = 0; i < ordens.Count; i++)
        {
            string pathOrigem = $"cascataRemanejamento.ordens[{i}]";
            JsonObject itemOrigem = leitor.ItemObjeto(ordens, i, "cascataRemanejamento.ordens");
            leitor.ExigirChaves(itemOrigem, pathOrigem, "origem", "destinos");

            string origem = leitor.Texto(itemOrigem, "origem", pathOrigem, LimitesDoEnvelope.ModalidadeCodigo);

            // O comprimento do código de destino é responsabilidade da factory de DestinoRemanejamento.
            IReadOnlyList<string> destinosDaOrigem = leitor.Textos(itemOrigem, "destinos", pathOrigem);
            if (leitor.Falhou)
            {
                return null;
            }

            for (int j = 0; j < destinosDaOrigem.Count; j++)
            {
                Result<DestinoRemanejamento> destino = DestinoRemanejamento.Criar(origem, j + 1, destinosDaOrigem[j]);
                if (destino.IsFailure)
                {
                    leitor.Propagar<DestinoRemanejamento>(destino.Error!);
                    return null;
                }

                destinos.Add(destino.Value!);
            }
        }

        return destinos;
    }
}
