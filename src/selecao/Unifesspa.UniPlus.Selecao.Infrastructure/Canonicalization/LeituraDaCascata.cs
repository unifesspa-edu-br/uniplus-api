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

        return LerCascataPresente(leitor, bloco);
    }

    private static ConfiguracaoCascataRemanejamento? LerCascataPresente(LeitorEnvelope leitor, JsonObject bloco)
    {
        leitor.ExigirChaves(bloco, "cascataRemanejamento", "presente", "regra", "destinoFinalCodigo", "ordens");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "cascataRemanejamento", RegraRemanejamentoCodigo.Cascata);
        string fallbackCodigo = leitor.Texto(bloco, "destinoFinalCodigo", "cascataRemanejamento", LimitesDoEnvelope.ModalidadeCodigo);
        JsonArray ordens = leitor.Array(bloco, "ordens", "cascataRemanejamento");
        if (leitor.Falhou)
        {
            return null;
        }

        return ConstruirCascata(leitor, regra, fallbackCodigo, ordens);
    }

    private static ConfiguracaoCascataRemanejamento? ConstruirCascata(
        LeitorEnvelope leitor, ReferenciaRegra regra, string fallbackCodigo, JsonArray ordens)
    {
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
            if (!AdicionarDestinos(leitor, ordens, i, destinos))
            {
                return null;
            }
        }

        return destinos;
    }

    private static bool AdicionarDestinos(LeitorEnvelope leitor, JsonArray ordens, int indice, List<DestinoRemanejamento> destinos)
    {
        string path = $"cascataRemanejamento.ordens[{indice}]";
        JsonObject item = leitor.ItemObjeto(ordens, indice, "cascataRemanejamento.ordens");
        leitor.ExigirChaves(item, path, "origem", "destinos");
        string origem = leitor.Texto(item, "origem", path, LimitesDoEnvelope.ModalidadeCodigo);
        IReadOnlyList<string> codigos = leitor.Textos(item, "destinos", path);
        return !leitor.Falhou && AdicionarCodigos(leitor, origem, codigos, destinos);
    }

    private static bool AdicionarCodigos(LeitorEnvelope leitor, string origem, IReadOnlyList<string> codigos, List<DestinoRemanejamento> destinos)
    {
        for (int i = 0; i < codigos.Count; i++)
        {
            Result<DestinoRemanejamento> destino = DestinoRemanejamento.Criar(origem, i + 1, codigos[i]);
            if (destino.IsFailure)
            {
                leitor.Propagar<DestinoRemanejamento>(destino.Error!);
                return false;
            }

            destinos.Add(destino.Value!);
        }

        return true;
    }
}
