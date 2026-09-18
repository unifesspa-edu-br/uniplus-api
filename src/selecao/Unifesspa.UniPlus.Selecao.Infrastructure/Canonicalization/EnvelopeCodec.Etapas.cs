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
            string? faseCodigo = leitor.TextoOpcional(item, "faseCodigo", path, LimitesDoEnvelope.FaseCodigo);

            if (leitor.Falhou)
            {
                return [];
            }

            // Limites que hoje só o FluentValidation do comando impõe — a entidade não os
            // conhece. Um envelope legítimo já os satisfaz (passou pelo validator quando
            // foi criado); recusá-los aqui custa nada e fecha a porta a um envelope
            // adulterado que reidrataria num agregado impossível de persistir.
            //
            // São limites, e só limites: regra de coerência entre campos — peso exigindo caráter
            // que pontua, nota mínima exigindo caráter que elimina — fica de fora de propósito.
            // Uma regra criada depois do congelamento tornaria ilegível um envelope que era
            // válido quando nasceu, e o certame ficaria sem como reler a própria publicação.
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
            string pathProdutos = $"{path}.produtos";
            List<ProdutoDaEtapa> produtos = [];
            for (int j = 0; arrayProdutos is not null && j < arrayProdutos.Count; j++)
            {
                string pathProduto = $"{pathProdutos}[{j}]";

                // O caminho que `ItemObjeto` recebe é o do ARRAY: é ele que acrescenta o
                // índice. Passar o caminho do item já indexado emitiria `produtos[0][0]`, um
                // ponteiro para lugar nenhum justamente no erro que manda alguém abrir o
                // envelope adulterado e procurar o campo.
                JsonObject itemProduto = leitor.ItemObjeto(arrayProdutos, j, pathProdutos);
                leitor.ExigirChaves(itemProduto, pathProduto, "id", "atoCodigo", "papel");
                Guid idProduto = leitor.Identificador(itemProduto, "id", pathProduto);
                string ato = leitor.TextoNaoVazio(itemProduto, "atoCodigo", pathProduto, LimitesDoEnvelope.TipoAtoCodigo);
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
            string pathBancas = $"{path}.bancas";
            List<BancaDaEtapa> bancas = [];
            for (int j = 0; arrayBancas is not null && j < arrayBancas.Count; j++)
            {
                string pathBanca = $"{pathBancas}[{j}]";
                JsonObject itemBanca = leitor.ItemObjeto(arrayBancas, j, pathBancas);
                leitor.ExigirChaves(itemBanca, pathBanca, "tipoBancaOrigemId", "codigo");
                Guid origem = leitor.Identificador(itemBanca, "tipoBancaOrigemId", pathBanca);
                string codigoBanca = leitor.TextoNaoVazio(itemBanca, "codigo", pathBanca, LimitesDoEnvelope.TipoBancaCodigo);
                if (leitor.Falhou)
                {
                    return [];
                }

                // Id novo, como nas bancas da fase: o envelope não o congela porque nada o
                // referencia, e a identidade da linha não é o que a publicação promete.
                bancas.Add(BancaDaEtapa.Criar(origem, codigoBanca));
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
            string pathRecursos = $"{path}.recursos";
            List<RecursoDaEtapa> recursos = [];
            for (int j = 0; arrayRecursos is not null && j < arrayRecursos.Count; j++)
            {
                string pathRecurso = $"{pathRecursos}[{j}]";
                JsonObject itemRecurso = leitor.ItemObjeto(arrayRecursos, j, pathRecursos);
                leitor.ExigirChaves(
                    itemRecurso, pathRecurso,
                    "ancora", "regra", "args", "produtoAncoraId");
                AncoraDoRecurso ancora = leitor.Enumeracao<AncoraDoRecurso>(itemRecurso, "ancora", pathRecurso);

                // A referência vem inteira e conferida contra o código de regra esperado, como
                // na fase: reconstruí-la com hash vazio produzia uma `ReferenciaRegra` inválida,
                // cujo `Value` nulo seguia adiante e só estourava lá na frente.
                ReferenciaRegra regra = leitor.Regra(
                    itemRecurso, "regra", pathRecurso, RegraPrazoRecursoCodigo.AncoradoEmAto);

                // A âncora por ciência não tem produto — o campo vem nulo, e é assim que o
                // recurso volta com a mesma âncora que o comando lhe deu.
                Guid? ancoraId = leitor.IdentificadorOpcional(itemRecurso, "produtoAncoraId", pathRecurso);
                JsonObject argsObjeto = leitor.Objeto(itemRecurso, "args", pathRecurso);
                if (leitor.Falhou)
                {
                    return [];
                }

                // `Nenhuma` é o sentinela de ausência que `RecursoDaEtapa.Criar` recusa e que
                // `Reidratar` não reconfere — mesma razão pela qual o caráter `Nenhum` da etapa
                // é barrado acima. Sem esta guarda, um envelope adulterado restaura uma janela
                // recursal sem relógio: `DefinirRecursos` só reconhece ato publicado e ciência
                // individual, então o valor atravessa todas as guardas, persiste, e a reemissão
                // produz os mesmos bytes — nada acusa um prazo que não corre de instante nenhum.
                if (ancora == AncoraDoRecurso.Nenhuma)
                {
                    return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(new DomainError(
                        ErrosCodecEnvelope.EnvelopeMalformado,
                        $"Envelope malformado em '{pathRecurso}.ancora': o recurso tem de declarar de que instante o prazo corre.")) ?? [];
                }

                ArgsRegraPrazoRecurso? args = LerArgsDePrazoDeRecurso(leitor, argsObjeto, $"{pathRecurso}.args");
                if (leitor.Falhou || args is null)
                {
                    return [];
                }

                // Identidade nova, como nas bancas: o envelope não congela a do recurso porque
                // nada a referencia, e o que a publicação promete é a janela — âncora, regra e
                // prazos —, não a linha que a guarda.
                Result<RecursoDaEtapa> recurso = RecursoDaEtapa.Reidratar(
                    Guid.CreateVersion7(),
                    ancora,
                    regra,
                    args,
                    ancoraId ?? Guid.Empty);

                if (recurso.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<EtapaProcesso>>(recurso.Error!) ?? [];
                }

                recursos.Add(recurso.Value!);
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
