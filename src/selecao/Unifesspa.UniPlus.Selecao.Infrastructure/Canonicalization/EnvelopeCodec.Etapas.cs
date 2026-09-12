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
            leitor.ExigirChaves(item, path, "id", "nome", "carater", "tipoEtapa", "peso", "notaMinima", "ordem", "faseCodigo", "produtos",
                "inicio", "fim", "emiteParecerIndividual", "bancas", "recursos");

            Guid id = leitor.Identificador(item, "id", path);
            string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.EtapaNome);
            CaraterEtapa carater = leitor.Enumeracao<CaraterEtapa>(item, "carater", path);
            TipoEtapaSnapshot? tipoEtapa = LerTipoEtapaDaEtapa(leitor, item, path);
            decimal? peso = leitor.DecimalOpcional(item, "peso", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            decimal? notaMinima = leitor.DecimalOpcional(item, "notaMinima", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);
            int? ordem = leitor.InteiroOpcional(item, "ordem", path);
            string? faseCodigo = leitor.TextoOpcional(item, "faseCodigo", path, LimitesDoEnvelope.EtapaNome);

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

            EtapaProcesso reidratada = EtapaProcesso.Reidratar(id, nome, carater, tipoEtapa!, peso, notaMinima, ordem, faseCodigo);

            JsonArray? arrayProdutos = leitor.Array(item, "produtos", path);
            List<ProdutoDaEtapa> produtos = [];
            for (int j = 0; arrayProdutos is not null && j < arrayProdutos.Count; j++)
            {
                string pathProduto = $"{path}.produtos[{j}]";
                JsonObject itemProduto = leitor.ItemObjeto(arrayProdutos, j, pathProduto);
                leitor.ExigirChaves(itemProduto, pathProduto, "id", "atoCodigo", "papel");
                Guid idProduto = leitor.Identificador(itemProduto, "id", pathProduto);
                string ato = leitor.TextoNaoVazio(itemProduto, "atoCodigo", pathProduto, LimitesDoEnvelope.EtapaNome);
                PapelProdutoFase? papel = leitor.EnumeracaoOpcional<PapelProdutoFase>(itemProduto, "papel", pathProduto);
                if (leitor.Falhou)
                {
                    return [];
                }

                produtos.Add(ProdutoDaEtapa.Reidratar(idProduto, ato, papel));
            }

            // Janela, parecer, bancas e recursos da etapa. O decodificador é a última linha
            // contra envelope adulterado: as guardas da entidade rodam de novo aqui.
            DateTimeOffset? inicioEtapa = leitor.InstanteOpcional(item, "inicio", path);
            DateTimeOffset? fimEtapa = leitor.InstanteOpcional(item, "fim", path);
            bool parecer = leitor.Booleano(item, "emiteParecerIndividual", path);
            if (reidratada.DefinirJanelaEParecer(inicioEtapa, fimEtapa, parecer).IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}': a janela da etapa está invertida.")) ?? [];
            }

            JsonArray? arrayBancas = leitor.Array(item, "bancas", path);
            List<BancaDaEtapa> bancas = [];
            for (int j = 0; arrayBancas is not null && j < arrayBancas.Count; j++)
            {
                string pathBanca = $"{path}.bancas[{j}]";
                JsonObject itemBanca = leitor.ItemObjeto(arrayBancas, j, pathBanca);
                leitor.ExigirChaves(itemBanca, pathBanca, "id", "tipoBancaOrigemId", "codigo");
                Guid idBanca = leitor.Identificador(itemBanca, "id", pathBanca);
                Guid origem = leitor.Identificador(itemBanca, "tipoBancaOrigemId", pathBanca);
                string codigoBanca = leitor.TextoNaoVazio(itemBanca, "codigo", pathBanca, LimitesDoEnvelope.EtapaNome);
                if (leitor.Falhou)
                {
                    return [];
                }

                bancas.Add(BancaDaEtapa.Reidratar(idBanca, origem, codigoBanca));
            }

            if (reidratada.DefinirBancas(bancas).IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}.bancas': o mesmo tipo aparece duas vezes.")) ?? [];
            }

            if (reidratada.DefinirProdutos(produtos).IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}.produtos': o mesmo ato aparece duas vezes na etapa.")) ?? [];
            }

            JsonArray? arrayRecursos = leitor.Array(item, "recursos", path);
            List<RecursoDaEtapa> recursos = [];
            for (int j = 0; arrayRecursos is not null && j < arrayRecursos.Count; j++)
            {
                string pathRecurso = $"{path}.recursos[{j}]";
                JsonObject itemRecurso = leitor.ItemObjeto(arrayRecursos, j, pathRecurso);
                leitor.ExigirChaves(
                    itemRecurso, pathRecurso,
                    "id", "ancora", "regraCodigo", "regraVersao", "prazoValor", "prazoUnidade", "produtoAncoraId");
                Guid idRecurso = leitor.Identificador(itemRecurso, "id", pathRecurso);
                AncoraDoRecurso ancora = leitor.Enumeracao<AncoraDoRecurso>(itemRecurso, "ancora", pathRecurso);
                string regraCodigo = leitor.TextoNaoVazio(itemRecurso, "regraCodigo", pathRecurso, LimitesDoEnvelope.EtapaNome);
                string regraVersao = leitor.TextoNaoVazio(itemRecurso, "regraVersao", pathRecurso, LimitesDoEnvelope.EtapaNome);
                decimal? prazo = leitor.DecimalOpcional(itemRecurso, "prazoValor", EscalaPadrao, pathRecurso, LimitesDoEnvelope.PrecisaoEtapa);
                UnidadePrazo unidade = leitor.Enumeracao<UnidadePrazo>(itemRecurso, "prazoUnidade", pathRecurso);
                Guid ancoraId = leitor.Identificador(itemRecurso, "produtoAncoraId", pathRecurso);
                if (leitor.Falhou)
                {
                    return [];
                }

                recursos.Add(RecursoDaEtapa.Reidratar(
                    idRecurso,
                    ancora,
                    ReferenciaRegra.Criar(regraCodigo, regraVersao, string.Empty).Value!,
                    new ArgsRegraPrazoRecurso(prazo ?? 0m, unidade, null, null, null, null),
                    ancoraId));
            }

            if (reidratada.DefinirRecursos(recursos).IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}.recursos': a âncora não resolve na etapa.")) ?? [];
            }

            etapas.Add(reidratada);
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
