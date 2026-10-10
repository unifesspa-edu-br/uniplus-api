namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos blocos de fatos e derivação (RN08): <c>fatosColetados</c>,
/// <c>regrasDerivacao</c>, e as sub-chaves de <c>documentosExigidos</c> que descrevem o
/// vocabulário de fatos — <c>referenciaTemporalFatos</c>, <c>metadadosFatos</c> e os valores
/// de domínio declarados. Inclui o gate fail-closed que fecha o vocabulário citado contra o
/// que o processo efetivamente coleta e deriva.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// A POLÍTICA crua (B-03) — o insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.
    /// </summary>
    private static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonObject? json = leitor.ObjetoOpcional(bloco, "referenciaTemporalFatos", "documentosExigidos");
        if (leitor.Falhou || json is null)
        {
            return null;
        }

        const string path = "documentosExigidos.referenciaTemporalFatos";
        leitor.ExigirChaves(json, path, "tipo", "data", "faseId");

        string tipoCodigo = leitor.TextoNaoVazio(json, "tipo", path);
        DateOnly? data = leitor.DataOpcional(json, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(json, "faseId", path);
        if (leitor.Falhou)
        {
            return null;
        }

        // FromCodigo mapeia um token não reconhecido para o sentinela Nenhuma — e Criar já
        // o rejeita com um DomainError nomeado (ReferenciaTemporalFatos.TipoObrigatorio),
        // sem precisar de uma checagem de "código desconhecido" própria aqui.
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(
            ReferenciaTipoCodigo.FromCodigo(tipoCodigo), data, faseId);
        return resultado.IsFailure ? leitor.Propagar<ReferenciaTemporalFatos>(resultado.Error!) : resultado.Value;
    }

    /// <summary>
    /// Simétrico de <c>SnapshotPublicacaoCanonicalizer.SerializarMetadadosFatos</c>: array
    /// ordenado por <c>codigo</c> (o encoder já ordena — este leitor não reordena, só
    /// decodifica item a item), chaves fechadas por item. Código duplicado no array é
    /// envelope malformado (o encoder nunca emite duas entradas para o mesmo fato). Dentro de
    /// cada item, <c>valoresDominioDeclarados[]</c> (quando presente) recebe a mesma disciplina:
    /// <c>ordem</c> obrigatória e não negativa, <c>valorCodigo</c> sem repetição
    /// (<see cref="LerValoresDominioDeclarados"/>).
    /// </summary>
    private static Dictionary<string, MetadadoFatoCongelado> LerMetadadosFatos(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "metadadosFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal);
        }

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"documentosExigidos.metadadosFatos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "documentosExigidos.metadadosFatos");
            leitor.ExigirChaves(
                item, path,
                "fatoCodigo", "dominio", "origem", "cardinalidade", "pontoResolucao", "binding",
                "valoresDominio", "valoresDominioDeclarados");

            string codigo = leitor.TextoNaoVazio(item, "fatoCodigo", path);
            string dominio = leitor.TextoNaoVazio(item, "dominio", path);
            string origem = leitor.TextoNaoVazio(item, "origem", path);
            string cardinalidade = leitor.TextoNaoVazio(item, "cardinalidade", path);
            string pontoResolucao = leitor.TextoNaoVazio(item, "pontoResolucao", path);
            string binding = leitor.TextoNaoVazio(item, "binding", path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<string>? valoresDominio = LerValoresDominio(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDeclarados = LerValoresDominioDeclarados(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            if (!metadados.TryAdd(codigo, new MetadadoFatoCongelado(
                codigo, dominio, origem, cardinalidade, pontoResolucao, binding, valoresDominio, valoresDeclarados)))
            {
                leitor.Propagar<MetadadoFatoCongelado>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}': o fato '{codigo}' aparece mais de uma vez em 'metadadosFatos' — cada fato tem no máximo um metadado congelado."));
                return metadados;
            }
        }

        return metadados;
    }

    /// <summary>
    /// <c>valoresDominio</c> — <see langword="null"/> significante (categórico de escopo
    /// dinâmico, booleano ou numérico), array quando o fato é categórico estático. Mesma
    /// técnica de <c>LerFormatosPermitidos</c> para o campo nulo-ou-array:
    /// leitura do <see cref="JsonNode"/> bruto, não <see cref="LeitorEnvelope.Array"/> (que
    /// rejeitaria <see langword="null"/>).
    /// </summary>
    private static IReadOnlyList<string>? LerValoresDominio(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominio";
        if (item["valoresDominio"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "valoresDominio", pathPai);
    }

    /// <summary>
    /// Mesma técnica de <see cref="LerValoresDominio"/> para o campo nulo-ou-array. Cada item
    /// exige <c>ordem</c> (issue #1059, UNI-REQ-0072) — não negativa — e <c>valorCodigo</c> não
    /// pode se repetir dentro do array: o encoder nunca emite duas entradas para o mesmo valor.
    /// </summary>
    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresDominioDeclarados(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominioDeclarados";
        if (item["valoresDominioDeclarados"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array ou null."));
        }

        List<ValorDominioDeclaradoCongelado> valores = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject valorItem = leitor.ItemObjeto(array, i, path);
            leitor.ExigirChaves(valorItem, itemPath, "valorCodigo", "descricao", "ordem");

            string codigoValor = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
            string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
            int ordem = leitor.Inteiro(valorItem, "ordem", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            if (ordem < 0)
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{itemPath}.ordem' não pode ser negativa."));
            }

            if (!codigos.Add(codigoValor))
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}': o valor '{codigoValor}' aparece mais de uma vez."));
            }

            valores.Add(new ValorDominioDeclaradoCongelado(codigoValor, descricao, ordem));
        }

        return valores;
    }

    /// <summary>
    /// Os fatos coletados (Story #928, §7.4) — reconstruídos por <see cref="FatoColetado.Criar"/>
    /// (que revalida a forma: código, ordem, auto-referência), Ids novos (não são congelados no
    /// envelope, diferente de <c>etapa.Id</c>). A pré-condição é a mesma forma DNF do gatilho.
    /// <c>ValoresSelecionaveis</c> (issue #1059, UNI-REQ-0072) é o dicionário reidratado —
    /// completo, uma entrada por fato coletado (array para os de seleção, <see langword="null"/>
    /// para os demais) — que <see cref="Application.Services.RestauradorDeConfiguracao"/> repassa
    /// intacto para recanonicalizar, sem reconsultar o catálogo vivo.
    /// </summary>
    private static (
        IReadOnlyList<FatoColetado> Fatos,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
        LerFatosColetados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal);

        JsonArray array = leitor.Array(payload, "fatosColetados", "$");
        return leitor.Falhou ? ([], valoresSelecionaveis) : (LerListaDeFatos(leitor, array, "fatosColetados", valoresSelecionaveis), valoresSelecionaveis);
    }

    /// <summary>
    /// Os grupos repetíveis (UNI-REQ-0146), reconstruídos por <see cref="GrupoColetado.Criar"/>, que
    /// revalida a forma; os campos de cada um são lidos como os itens, e os valores selecionáveis
    /// deles entram no mesmo dicionário dos itens. Os agregados sobre cada grupo entram em
    /// <paramref name="agregados"/>: cada um agrega um campo do próprio grupo, na ordem do código.
    /// </summary>
    private static IReadOnlyList<GrupoColetado> LerGruposColetados(
        LeitorEnvelope leitor,
        JsonObject payload,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        List<DefinicaoAgregado> agregados)
    {
        JsonArray array = leitor.Array(payload, "gruposColetados", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<GrupoColetado> grupos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"gruposColetados[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "gruposColetados");
            leitor.ExigirChaves(
                item, path, "codigo", "finalidade", "etapaCodigo", "ordem", "rotulo", "minimo", "maximo", "incluiCandidato", "exibicao", "obrigatoriedade", "subitens", "agregados");

            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.Fato);
            FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(item, "finalidade", path));
            string? etapaCodigo = leitor.TextoOpcional(item, "etapaCodigo", path, LimitesDoEnvelope.CodigoEtapaFormulario);
            int ordem = leitor.Inteiro(item, "ordem", path);
            string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro);
            int minimo = leitor.Inteiro(item, "minimo", path);
            int? maximo = leitor.InteiroOpcional(item, "maximo", path);
            bool incluiCandidato = leitor.Booleano(item, "incluiCandidato", path);
            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> exibicao = LerDnf(leitor, item, "exibicao", path);
            JsonArray subitens = leitor.Array(item, "subitens", path);
            if (leitor.Falhou)
            {
                return [];
            }

            if (finalidade == FinalidadeFormulario.Nenhuma)
            {
                return leitor.Propagar<IReadOnlyList<GrupoColetado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.finalidade' fora do vocabulário de finalidades.")) ?? [];
            }

            Result<PredicadoDnf?> exibicaoLida = PredicadoOpcional(exibicao);
            if (exibicaoLida.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<GrupoColetado>>(exibicaoLida.Error!) ?? [];
            }

            if (LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
            {
                return [];
            }

            IReadOnlyList<FatoColetado> campos = LerListaDeFatos(leitor, subitens, $"{path}.subitens", valoresSelecionaveis);
            if (leitor.Falhou)
            {
                return [];
            }

            if (campos.FirstOrDefault(c => c.Finalidade != finalidade || c.EtapaCodigo is not null) is { } foraDoGrupo)
            {
                return leitor.Propagar<IReadOnlyList<GrupoColetado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}.subitens': o campo '{foraDoGrupo.FatoCodigo}' tem finalidade ou seção própria, mas o campo do grupo segue o grupo.")) ?? [];
            }

            Result<GrupoColetado> grupo = GrupoColetado.Criar(
                codigo, ordem, etapaCodigo, rotulo, minimo, maximo, exibicaoLida.Value, obrigatoriedade, campos, finalidade, incluiCandidato);
            if (grupo.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<GrupoColetado>>(grupo.Error!) ?? [];
            }

            if (!LerAgregadosDoGrupo(leitor, item, path, grupo.Value!, agregados))
            {
                return [];
            }

            grupos.Add(grupo.Value!);
        }

        return grupos;
    }

    private static bool LerAgregadosDoGrupo(LeitorEnvelope leitor, JsonObject item, string path, GrupoColetado grupo, List<DefinicaoAgregado> agregados)
    {
        JsonArray array = leitor.Array(item, "agregados", path);
        string? anterior = null;
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            string caminho = $"{path}.agregados[{i}]";
            JsonObject agregado = leitor.ItemObjeto(array, i, $"{path}.agregados");
            leitor.ExigirChaves(agregado, caminho, "codigo", "fatoDeMembro", "operacao");
            string codigo = leitor.TextoNaoVazio(agregado, "codigo", caminho, LimitesDoEnvelope.Fato);
            string fatoDeMembro = leitor.TextoNaoVazio(agregado, "fatoDeMembro", caminho, LimitesDoEnvelope.Fato);
            OperacaoAgregado operacao = AgregadoDeGrupo.DoToken(leitor.TextoNaoVazio(agregado, "operacao", caminho));
            if (leitor.Falhou)
            {
                break;
            }

            string? defeito = operacao == OperacaoAgregado.Nenhuma ? "a operação não é EXISTE nem VALORES_PRESENTES"
                : !grupo.Subitens.Any(s => string.Equals(s.FatoCodigo, fatoDeMembro, StringComparison.Ordinal)) ? $"o fato de membro '{fatoDeMembro}' não é campo do grupo"
                : anterior is not null && string.CompareOrdinal(anterior, codigo) >= 0 ? "os agregados não estão na ordem do código"
                : null;
            if (defeito is not null)
            {
                _ = leitor.Propagar<object>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{caminho}': {defeito}."));
                break;
            }

            agregados.Add(new DefinicaoAgregado(codigo, grupo.Codigo, fatoDeMembro, operacao));
            anterior = codigo;
        }

        return !leitor.Falhou;
    }

    /// <summary>Uma lista de fatos coletados — os itens ou os campos de um grupo —, na forma congelada.</summary>
    private static IReadOnlyList<FatoColetado> LerListaDeFatos(
        LeitorEnvelope leitor,
        JsonArray array,
        string caminho,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        List<FatoColetado> fatos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{caminho}[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, caminho);
            leitor.ExigirChaves(
                item, path,
                "fatoCodigo", "finalidade", "etapaCodigo", "ordem", "rotulo", "tipoRenderizacao", "obrigatoriedade", "ajuda",
                "pedirConfirmacao", "restricoes", "impedimento", "origemValores", "formato", "classificacaoProtecao", "precondicao",
                "valoresSelecionaveis");

            string fatoCodigo = leitor.TextoNaoVazio(item, "fatoCodigo", path, LimitesDoEnvelope.Fato);
            FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(item, "finalidade", path));
            string? etapaCodigo = leitor.TextoOpcional(item, "etapaCodigo", path, LimitesDoEnvelope.CodigoEtapaFormulario);
            int ordem = leitor.Inteiro(item, "ordem", path);
            string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro);
            string tipoRenderizacaoCodigo = leitor.TextoNaoVazio(item, "tipoRenderizacao", path);
            string? ajuda = leitor.TextoOpcional(item, "ajuda", path, LimitesDoEnvelope.AjudaDoCampo);
            bool pedirConfirmacao = leitor.Booleano(item, "pedirConfirmacao", path);
            OrigemValoresColeta origemValores = leitor.Enumeracao<OrigemValoresColeta>(item, "origemValores", path);
            string? formato = leitor.TextoOpcional(item, "formato", path, LimitesDoEnvelope.Token);
            string classificacaoProtecao = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token);
            if (leitor.Falhou)
            {
                return [];
            }

            if (finalidade == FinalidadeFormulario.Nenhuma)
            {
                return leitor.Propagar<IReadOnlyList<FatoColetado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.finalidade' fora do vocabulário de finalidades.")) ?? [];
            }

            TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(tipoRenderizacaoCodigo);
            if (tipoRenderizacao is TipoRenderizacao.Nenhuma)
            {
                return leitor.Propagar<IReadOnlyList<FatoColetado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.tipoRenderizacao' fora do vocabulário de tipos de renderização.")) ?? [];
            }

            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes =
                LerDnf(leitor, item, "precondicao", path);
            if (leitor.Falhou)
            {
                return [];
            }

            List<CondicaoPrecondicaoFato> precondicoes = [];
            foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoes)
            {
                Result<CondicaoPrecondicaoFato> condicao = CondicaoPrecondicaoFato.Criar(clausula, fato, operador, valor);
                if (condicao.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<FatoColetado>>(condicao.Error!) ?? [];
                }

                precondicoes.Add(condicao.Value!);
            }

            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato =
                LerValoresSelecionaveis(leitor, item, path, tipoRenderizacao);
            if (leitor.Falhou)
            {
                return [];
            }

            if (LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade
                || LerRestricoes(leitor, item, path) is not { } restricoes)
            {
                return [];
            }

            Impedimento? impedimento = LerImpedimento(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            // A forma do item não conhece a finalidade; o agregado recusa o impedimento fora da
            // inscrição ao definir os itens, e o decoder tem de recusar o mesmo.
            if (Impedimento.ConferirFinalidade(finalidade, [impedimento]) is [{ } foraDaInscricao, ..])
            {
                return leitor.Propagar<IReadOnlyList<FatoColetado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.impedimento': {foraDaInscricao.Error.Message}")) ?? [];
            }

            Result<FatoColetado> fatoColetado = FatoColetado.Criar(
                fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatoriedade, precondicoes, origemValores, etapaCodigo, finalidade, formato,
                ajuda, pedirConfirmacao, restricoes, impedimento, classificacaoProtecao);
            if (fatoColetado.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<FatoColetado>>(fatoColetado.Error!) ?? [];
            }

            fatos.Add(fatoColetado.Value!);

            // A unicidade de fatoCodigo é reconferida por ValidarBlocoDeFatosEDerivacao, mais
            // adiante — um envelope adulterado com fato duplicado sobrescreve a entrada aqui
            // (last-wins) e é recusado como malformado lá, não aqui.
            valoresSelecionaveis[fatoCodigo] = valoresDoFato;
        }

        return fatos;
    }

    /// <summary>
    /// A resposta que impede a inscrição, congelada no item; nula quando o item não tem. A forma — a
    /// mensagem e as cláusulas sobre o próprio campo — é reconferida por <see cref="FatoColetado.Criar"/>.
    /// </summary>
    private static Impedimento? LerImpedimento(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "impedimento", path);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string pathImpedimento = $"{path}.impedimento";
        leitor.ExigirChaves(bloco, pathImpedimento, "quando", "mensagem");
        string mensagem = leitor.TextoNaoVazio(bloco, "mensagem", pathImpedimento, LimitesDoEnvelope.MensagemDoImpedimento);
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas = LerDnf(leitor, bloco, "quando", pathImpedimento);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> quando = PredicadoOpcional(linhas);
        if (quando.IsFailure)
        {
            return leitor.Propagar<Impedimento>(quando.Error!);
        }

        return quando.Value is { } condicao
            ? new Impedimento(condicao, mensagem)
            : leitor.Propagar<Impedimento>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathImpedimento}.quando' sem condição."));
    }

    /// <summary>
    /// As opções que o processo declarou (issue #1619), reconstruídas dos valores selecionáveis
    /// congelados dos fatos coletados cuja fonte é o processo — o envelope não guarda uma segunda
    /// cópia delas. As dos fatos de atendimento não entram: vêm da oferta, reposta à parte.
    /// </summary>
    private static Result<IReadOnlyList<OpcaoDeclaradaFato>> ReconstruirOpcoesDeclaradas(
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        List<OpcaoDeclaradaFato> opcoes = [];
        foreach (FatoColetado fato in fatos)
        {
            if (!fato.OpcoesDoProcesso || OfertaAtendimentoEspecializado.GereOpcoesDoFato(fato.FatoCodigo))
            {
                continue;
            }

            foreach (ValorDominioDeclaradoCongelado valor in valoresSelecionaveis.GetValueOrDefault(fato.FatoCodigo) ?? [])
            {
                Result<OpcaoDeclaradaFato> opcao = OpcaoDeclaradaFato.Criar(
                    fato.FatoCodigo, valor.Codigo, valor.Descricao ?? string.Empty, valor.Ordem);
                if (opcao.IsFailure)
                {
                    return Result<IReadOnlyList<OpcaoDeclaradaFato>>.Failure(opcao.Error!);
                }

                opcoes.Add(opcao.Value!);
            }
        }

        return Result<IReadOnlyList<OpcaoDeclaradaFato>>.Success(opcoes);
    }

    /// <summary>
    /// Os valores selecionáveis de um fato coletado (issue #1059, UNI-REQ-0072) — bicondicional
    /// com <paramref name="tipoRenderizacao"/> (D1-bis do plano da issue): <c>SELECAO_UNICA</c>/
    /// <c>SELECAO_MULTIPLA</c> exige array com cardinalidade mínima 1 (issue #1077: nunca vazio);
    /// <c>BOOLEANO</c>/<c>NUMERO</c> exige <see langword="null"/>. Um envelope que descumpra a
    /// bicondicional em qualquer sentido — seletor mudo (<c>null</c> ou <c>[]</c> onde deveria
    /// ter opção), ou vocabulário pendurado num campo booleano/numérico — é malformado. Cada
    /// item exige <c>ordem</c> não negativa e
    /// <c>valorCodigo</c> sem repetição, mesma disciplina que
    /// <c>LerValoresDominioDeclarados</c> aplica a <c>valoresDominioDeclarados</c>.
    /// </summary>
    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresSelecionaveis(
        LeitorEnvelope leitor, JsonObject item, string pathPai, TipoRenderizacao tipoRenderizacao)
    {
        string path = $"{pathPai}.valoresSelecionaveis";
        bool ehFatoDeSelecao = tipoRenderizacao.EhSelecao();

        if (item["valoresSelecionaveis"] is not JsonNode node)
        {
            if (ehFatoDeSelecao)
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' é null, mas o tipo de renderização é de seleção — a bicondicional exige um array."));
            }

            return null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array ou null."));
        }

        if (!ehFatoDeSelecao)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{path}' é um array, mas o tipo de renderização '{tipoRenderizacao}' não é de seleção — " +
                "a bicondicional exige null."));
        }

        List<ValorDominioDeclaradoCongelado> valores = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject valorItem = leitor.ItemObjeto(array, i, path);
            leitor.ExigirChaves(valorItem, itemPath, "valorCodigo", "descricao", "ordem", "orientacao");

            string valorCodigo = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
            string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
            int ordem = leitor.Inteiro(valorItem, "ordem", itemPath);
            string? orientacao = leitor.TextoOpcional(valorItem, "orientacao", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            if (ordem < 0)
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{itemPath}.ordem' não pode ser negativa."));
            }

            if (!codigos.Add(valorCodigo))
            {
                return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}': o valor '{valorCodigo}' aparece mais de uma vez."));
            }

            valores.Add(new ValorDominioDeclaradoCongelado(valorCodigo, descricao, ordem, orientacao));
        }

        // issue #1077: a bicondicional exige array, mas array VAZIO para um fato de seleção não
        // é forma válida — um seletor publicado sem opção nenhuma não é respondível. Um envelope
        // adulterado com "valoresSelecionaveis": [] é malformado, não um caso legítimo a
        // reidratar silenciosamente. ehFatoDeSelecao já é garantidamente true aqui (o ramo
        // !ehFatoDeSelecao retornou acima) — checagem redundante removida (achado CodeQL).
        if (valores.Count == 0)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{path}' é um array vazio, mas o tipo de renderização é de seleção — a cardinalidade mínima é 1."));
        }

        return valores;
    }

    /// <summary>
    /// As regras de derivação (Story #928, §7.4) — reconstruídas nos três níveis por
    /// <see cref="ConfiguracaoDerivacaoFato.Criar"/>, <see cref="RegraDerivacaoConfigurada.Criar"/> e
    /// <see cref="CondicaoRegraDerivacao.Criar"/> (forma revalidada; Ids novos). A regra âncora tem
    /// <c>quando</c> nulo.
    /// </summary>
    private static IReadOnlyList<ConfiguracaoDerivacaoFato> LerRegrasDerivacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "regrasDerivacao", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<ConfiguracaoDerivacaoFato> configuracoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"regrasDerivacao[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "regrasDerivacao");
            leitor.ExigirChaves(item, path, "codigoFato", "regras");

            string codigoFato = leitor.TextoNaoVazio(item, "codigoFato", path, LimitesDoEnvelope.Fato);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<RegraDerivacaoConfigurada> regras = LerRegrasDaDerivacao(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            Result<ConfiguracaoDerivacaoFato> config = ConfiguracaoDerivacaoFato.Criar(codigoFato, regras);
            if (config.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<ConfiguracaoDerivacaoFato>>(config.Error!) ?? [];
            }

            configuracoes.Add(config.Value!);
        }

        return configuracoes;
    }

    private static IReadOnlyList<RegraDerivacaoConfigurada> LerRegrasDaDerivacao(
        LeitorEnvelope leitor, JsonObject configItem, string pathPai)
    {
        JsonArray array = leitor.Array(configItem, "regras", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<RegraDerivacaoConfigurada> regras = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.regras[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.regras");
            leitor.ExigirChaves(item, path, "ordem", "contribui", "quando");

            int ordem = leitor.Inteiro(item, "ordem", path);
            string? contribui = leitor.TextoOpcional(item, "contribui", path, LimitesDoEnvelope.Fato);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoesTuplas =
                LerDnf(leitor, item, "quando", path);
            if (leitor.Falhou)
            {
                return [];
            }

            List<CondicaoRegraDerivacao> condicoes = [];
            foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoesTuplas)
            {
                Result<CondicaoRegraDerivacao> condicao = CondicaoRegraDerivacao.Criar(clausula, fato, operador, valor);
                if (condicao.IsFailure)
                {
                    return leitor.Propagar<IReadOnlyList<RegraDerivacaoConfigurada>>(condicao.Error!) ?? [];
                }

                condicoes.Add(condicao.Value!);
            }

            Result<RegraDerivacaoConfigurada> regra = RegraDerivacaoConfigurada.Criar(ordem, contribui, condicoes);
            if (regra.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<RegraDerivacaoConfigurada>>(regra.Error!) ?? [];
            }

            regras.Add(regra.Value!);
        }

        return regras;
    }

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> (pré-condição de fato ou <c>quando</c> de
    /// regra), na mesma forma do gatilho: array de cláusulas (OU), cada uma array de condições (E).
    /// Ausente/nulo = sem condição. Mesma disciplina de <c>LerCondicaoGatilho</c> —
    /// a factory revalida a forma; token de operador não reconhecido vira <c>Nenhuma</c>, que a
    /// factory rejeita como falha de domínio.
    /// </summary>
    private static IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> LerDnf(
        LeitorEnvelope leitor, JsonObject item, string chave, string pathPai)
    {
        if (item[chave] is not JsonNode raiz)
        {
            return [];
        }

        if (raiz is not JsonArray clausulas)
        {
            return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.")) ?? [];
        }

        // O encoder nunca emite DNF vazio: "sem condição" é `null`, não `[]`. Um array vazio (ou uma
        // cláusula vazia `[]`) num envelope adulterado colapsaria para "sem pré-condição"/"regra âncora"
        // — uma forma que a projeção viva não produz — e a restauração o recanonicalizaria como `null`,
        // aceitando em silêncio um snapshot que o certame nunca congelou. Recusa como malformado.
        if (clausulas.Count == 0)
        {
            return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{pathPai}.{chave}' é um array de cláusulas vazio — a ausência de condição é `null`, nunca `[]`.")) ?? [];
        }

        List<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            string clausulaPath = $"{pathPai}.{chave}[{c}]";
            if (clausulas[c] is not JsonArray condicoesDaClausula)
            {
                return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{clausulaPath}' deveria ser um array de condições.")) ?? [];
            }

            if (condicoesDaClausula.Count == 0)
            {
                return leitor.Propagar<IReadOnlyList<(int, string, Operador, JsonElement)>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{clausulaPath}' é uma cláusula vazia — toda cláusula tem ao menos uma condição.")) ?? [];
            }

            for (int i = 0; i < condicoesDaClausula.Count; i++)
            {
                string condicaoPath = $"{clausulaPath}[{i}]";
                JsonObject condicaoItem = leitor.ItemObjeto(condicoesDaClausula, i, clausulaPath);
                leitor.ExigirChaves(condicaoItem, condicaoPath, "fato", "operador", "valor");

                string fato = leitor.TextoNaoVazio(condicaoItem, "fato", condicaoPath, LimitesDoEnvelope.Fato);
                string operadorCodigo = leitor.TextoNaoVazio(condicaoItem, "operador", condicaoPath);
                JsonElement valor = leitor.Valor(condicaoItem, "valor", condicaoPath);
                if (leitor.Falhou)
                {
                    return [];
                }

                condicoes.Add((c, fato, OperadorCodigo.FromCodigo(operadorCodigo), valor));
            }
        }

        return condicoes;
    }

    /// <summary>
    /// Recusa fail-closed do bloco de coleta/derivação (RN08): um envelope válido nunca declara fato
    /// duplicado, cita fato inexistente, contribui código fora do domínio de modalidades, fecha ciclo,
    /// nem congela um grafo/modalidades que não reproduzem o recomputado. Um envelope adulterado que o
    /// faça é recusado como malformado — o grafo conjunto ignora deliberadamente referências ausentes
    /// (projeta só o que existe), então o testemunho por si só não prova a completude do vocabulário
    /// citado; estas checagens fecham o que o grafo não prova. Devolve <see langword="null"/> quando o
    /// bloco é íntegro.
    /// </summary>
    private static DomainError? ValidarBlocoDeFatosEDerivacao(
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyList<GrupoColetado> grupos,
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        IReadOnlyList<FormularioProcesso> formularios,
        IReadOnlyList<TermoExigidoFormulario> termos,
        string versaoInterpretador,
        IReadOnlyList<string> modalidadesOfertadas,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao,
        JsonObject payload)
    {
        // Pré-produção: há uma só semântica de motor vigente (MotorDerivacao.VersaoSemantica) e nenhum
        // snapshot congelado em banco. Exigir que a versão do envelope seja a corrente é a leitura
        // honesta enquanto não existe versão legada a preservar — uma evolução da semântica antes da
        // produção reescreve as fixtures (bump de forma no 0.x), não reidrata uma versão anterior. O despacho por versão do interpretador (aceitar
        // uma versão anterior e recanonicalizar na semântica DELA, com encoder legado) é o mesmo
        // versionamento forense deliberadamente adiado para a 1ª release de produção (1.0.0) — a versão
        // é congelada AGORA justamente para esse despacho futuro poder existir sem migrar dado.
        if (!string.Equals(versaoInterpretador, MotorDerivacao.VersaoSemantica, StringComparison.Ordinal))
        {
            return Malformado(
                $"'versaoInterpretador' desconhecida: '{versaoInterpretador}' — este sistema resolve a semântica "
                + $"'{MotorDerivacao.VersaoSemantica}'.");
        }

        // Produtor único no processo, contando os campos dos grupos; ordem única dentro de cada
        // formulário, entre itens e grupos, e dentro de cada grupo; código de grupo único no processo
        // e fora do espaço de códigos dos fatos.
        HashSet<string> coletados = new(StringComparer.Ordinal);
        HashSet<(FinalidadeFormulario, int)> ordens = [];
        foreach (FatoColetado fato in fatos)
        {
            if (!coletados.Add(fato.FatoCodigo))
            {
                return Malformado($"'fatosColetados': o fato '{fato.FatoCodigo}' aparece mais de uma vez.");
            }

            if (!ordens.Add((fato.Finalidade, fato.Ordem)))
            {
                return Malformado($"'fatosColetados': a ordem {fato.Ordem} é usada por mais de um fato do mesmo formulário.");
            }
        }

        HashSet<string> codigosDeGrupo = new(StringComparer.Ordinal);
        foreach (GrupoColetado grupo in grupos)
        {
            if (!codigosDeGrupo.Add(grupo.Codigo))
            {
                return Malformado($"'gruposColetados': o código '{grupo.Codigo}' aparece em mais de um grupo.");
            }

            if (!ordens.Add((grupo.Finalidade, grupo.Ordem)))
            {
                return Malformado($"'gruposColetados': a ordem {grupo.Ordem} é usada por mais de um item ou grupo do mesmo formulário.");
            }

            HashSet<int> ordensNoGrupo = [];
            foreach (FatoColetado campo in grupo.Subitens)
            {
                if (!coletados.Add(campo.FatoCodigo))
                {
                    return Malformado($"'gruposColetados': o fato '{campo.FatoCodigo}' aparece mais de uma vez.");
                }

                if (!ordensNoGrupo.Add(campo.Ordem))
                {
                    return Malformado($"'gruposColetados': a ordem {campo.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}'.");
                }
            }
        }

        // Depois de todos os campos: o código de grupo não coincide com fato coletado, item ou campo.
        if (codigosDeGrupo.Overlaps(coletados))
        {
            return Malformado("'gruposColetados': um código de grupo coincide com o de um fato coletado.");
        }

        HashSet<string> derivados = new(StringComparer.Ordinal);
        // A derivação cita item ou derivado, nunca campo de grupo repetível, que existe por ocorrência.
        HashSet<string> universo = new(fatos.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (!derivados.Add(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o fato '{config.CodigoFato}' tem mais de uma configuração de derivação.");
            }

            // O derivado não é fato coletado — item ou campo de grupo — nem grupo.
            if (coletados.Contains(config.CodigoFato) || codigosDeGrupo.Contains(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o código '{config.CodigoFato}' já é de fato coletado ou de grupo.");
            }

            universo.Add(config.CodigoFato);
        }

        // Toda citação de regra de derivação e de gatilho de exigência existe em coletados ∪ derivados.
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            foreach (RegraDerivacaoConfigurada regra in config.Regras)
            {
                foreach (CondicaoRegraDerivacao condicao in regra.Condicoes)
                {
                    if (!universo.Contains(condicao.Fato))
                    {
                        return Malformado(
                            $"'regrasDerivacao': a derivação de '{config.CodigoFato}' cita '{condicao.Fato}', que o processo não coleta nem deriva.");
                    }
                }
            }
        }

        // A completude das citações de GATILHO (o fato citado existe em coletados ∪ derivados) é
        // recusa da fatia de dependência declarada (§7.3), não desta: o publish ainda não a barra,
        // e recusá-la só aqui divergiria decode de encode. O grafo conjunto já ignora de propósito a
        // citação a fato ausente, então o testemunho reproduz o mesmo grafo (sem a aresta) dos dois
        // lados — consistente até a fatia que fecha a recusa no publish.

        // O código contribuído pela derivação de MODALIDADE tem de pertencer ao domínio congelado das
        // modalidades ofertadas — o testemunho de conjunto prova o domínio, não que cada `contribui`
        // caiba nele. O VO da regra é o contrato: reconstruí-lo contra o domínio recusa `contribui`
        // fora dele (e revalida dependências/auto-referência de brinde).
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (string.Equals(config.CodigoFato, RegrasDerivacaoModalidadeLei12711.CodigoFato, StringComparison.Ordinal))
            {
                Result<RegrasDerivacaoFato> vo = config.ParaRegrasDerivacao(modalidadesOfertadas);
                if (vo.IsFailure)
                {
                    return vo.Error;
                }
            }
        }

        // Aciclicidade do grafo conjunto + testemunho: o grafo/modalidades congelados têm de reproduzir
        // exatamente o recomputado das partes reidratadas (byte a byte, pela mesma projeção canônica).
        Result<GrafoDependenciaConjunta> grafo =
            GrafoDependenciaConjunta.Construir(fatos, regrasDerivacao, documentosExigidos, formularios, termos, grupos);
        if (grafo.IsFailure)
        {
            return grafo.Error;
        }

        if (DivergeDoCongelado(payload, "grafoDependencia",
            SnapshotPublicacaoCanonicalizer.SerializarGrafoDependencia(grafo.Value!)))
        {
            return Malformado("'grafoDependencia' congelado não reproduz o grafo recomputado das partes reidratadas.");
        }

        if (DivergeDoCongelado(payload, "modalidadesOfertadas",
            SnapshotPublicacaoCanonicalizer.SerializarModalidadesOfertadas(distribuicao)))
        {
            return Malformado("'modalidadesOfertadas' congelado não reproduz o conjunto recomputado da distribuição.");
        }

        return null;

        static DomainError Malformado(string mensagem) => new(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);
    }

    /// <summary>
    /// Compara o bloco congelado com o esperado recomputado — ambos pela MESMA projeção canônica
    /// (<see cref="PerfilCanonicoV1"/>), imune à ordem de chaves. É o testemunho: o congelado não é
    /// segunda fonte de verdade, e sim uma cópia verificável do que as partes reidratadas reproduzem.
    /// </summary>
    private static bool DivergeDoCongelado(JsonObject payload, string chave, JsonNode esperado)
    {
        // Bloco ausente ou JSON `null` (uma coluna adulterada com hash recomputado consegue produzir
        // `"grafoDependencia": null`): o recomputado nunca é nulo, então diverge — recusa fail-closed,
        // nunca um NullReferenceException (500).
        if (payload[chave] is not JsonNode congelado)
        {
            return true;
        }

        // O perfil serializa um JsonObject; envolve-se cada lado num wrapper para comparar array ou
        // objeto pela mesma projeção. O congelado é clonado antes de reparentar — mutá-lo tiraria o
        // bloco do payload que ainda está sendo lido.
        byte[] bytesEsperados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = esperado });
        byte[] bytesCongelados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = congelado.DeepClone() });
        return !bytesEsperados.AsSpan().SequenceEqual(bytesCongelados);
    }
}
