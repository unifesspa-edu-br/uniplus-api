namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Leitura do bloco <c>etapas</c> do envelope.</summary>
public sealed partial class EnvelopeCodec
{
    private static IReadOnlyList<EtapaProcesso> LerEtapas(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "etapas", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<EtapaProcesso> etapas = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"etapas[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "etapas");
            leitor.ExigirChaves(item, path, "id", "nome", "carater", "tipoEtapa", "peso", "notaMinima", "ordem");

            Guid id = leitor.Identificador(item, "id", path);
            string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.EtapaNome);
            CaraterEtapa carater = leitor.Enumeracao<CaraterEtapa>(item, "carater", path);
            TipoEtapaSnapshot? tipoEtapa = LerTipoEtapaDaEtapa(leitor, item, path);
            decimal? peso = leitor.DecimalOpcional(item, "peso", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            decimal? notaMinima = leitor.DecimalOpcional(item, "notaMinima", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            int? ordem = leitor.InteiroOpcional(item, "ordem", path);

            if (leitor.Falhou)
            {
                return [];
            }

            // Limites que hoje só o FluentValidation do comando impõe — a entidade não os
            // conhece. Um envelope legítimo já os satisfaz (passou pelo validator quando
            // foi criado); recusá-los aqui custa nada e fecha a porta a um envelope
            // adulterado que reidrataria num agregado impossível de persistir.
            if (carater == CaraterEtapa.Nenhum
                || peso is <= 0
                || notaMinima is < 0
                || ordem is <= 0)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}': peso e ordem devem ser maiores que zero e a nota mínima não negativa.")) ?? [];
            }

            etapas.Add(EtapaProcesso.Reidratar(id, nome, carater, tipoEtapa!, peso, notaMinima, ordem));
        }

        return etapas;
    }

    /// <summary>
    /// Lê o snapshot de tipo aninhado em cada item de <c>etapas</c> (issue #1071) — mesmo
    /// shape do bloco de topo <c>tipoProcesso</c>, mas diferente daquele (só forma, nunca
    /// reidratado: o tipo do processo não muda ao restaurar uma versão), o valor lido aqui é
    /// efetivamente usado para reconstruir a identidade de tipo da etapa. Um envelope
    /// adulterado com <c>origemId</c>/<c>codigo</c>/<c>nome</c> vazio ou acima do limite é
    /// recusado como malformado pela própria factory do VO, nunca reidratado.
    /// </summary>
    private static TipoEtapaSnapshot? LerTipoEtapaDaEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonObject tipo = leitor.Objeto(item, "tipoEtapa", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string tipoPath = $"{path}.tipoEtapa";
        leitor.ExigirChaves(tipo, tipoPath, "origemId", "codigo", "nome");
        Guid origemId = leitor.Identificador(tipo, "origemId", tipoPath);
        string codigo = leitor.TextoNaoVazio(tipo, "codigo", tipoPath, LimitesDoEnvelope.TipoEtapaCodigo);
        string nome = leitor.TextoNaoVazio(tipo, "nome", tipoPath, LimitesDoEnvelope.TipoEtapaNome);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(origemId, codigo, nome);
        return resultado.IsFailure ? leitor.Propagar<TipoEtapaSnapshot>(resultado.Error!) : resultado.Value;
    }
}
