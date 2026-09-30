namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos blocos de configuração autocontidos do envelope: <c>formulario</c>,
/// <c>divulgacao</c>, <c>taxaInscricao</c>, <c>localidade</c>, <c>algoritmoContagemPrazo</c>
/// e <c>calendarioDiasUteis</c>.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// Os formulários por finalidade (UNI-REQ-0144), em ordem de finalidade e sem repetição, cada um
    /// remontado pelas factories do domínio — que reconferem a estrutura das etapas — com os seus
    /// termos, em ordem crescente e com código único no formulário.
    /// </summary>
    private static (IReadOnlyList<FormularioProcesso> Formularios, IReadOnlyList<TermoExigidoFormulario> Termos) LerFormularios(
        LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray blocos = leitor.Array(payload, "formularios", "$");
        if (leitor.Falhou)
        {
            return ([], []);
        }

        List<FormularioProcesso> formularios = [];
        List<TermoExigidoFormulario> termos = [];
        FinalidadeFormulario? finalidadeAnterior = null;
        for (int f = 0; f < blocos.Count; f++)
        {
            string path = $"formularios[{f}]";
            JsonObject bloco = leitor.ItemObjeto(blocos, f, "formularios");
            leitor.ExigirChaves(bloco, path, "finalidade", "faseId", "titulo", "modeloOrigem", "etapas", "termos");

            FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(bloco, "finalidade", path));
            Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
            string? titulo = leitor.TextoOpcional(bloco, "titulo", path, LimitesDoEnvelope.NomeDeCadastro);
            (Guid? modeloId, string? modeloCodigo) = LerModeloDeOrigem(leitor, bloco, path);
            IReadOnlyList<EtapaFormulario> etapas = LerEtapasDoFormulario(leitor, bloco, path);
            JsonArray itensDeTermo = leitor.Array(bloco, "termos", path);
            if (leitor.Falhou)
            {
                return ([], []);
            }

            // O encoder emite as finalidades em ordem estritamente crescente, uma vez cada.
            if (finalidade == FinalidadeFormulario.Nenhuma || finalidade <= finalidadeAnterior)
            {
                leitor.Propagar<object>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.finalidade' fora do vocabulário, repetida ou fora de ordem."));
                return ([], []);
            }

            finalidadeAnterior = finalidade;
            Result<FormularioProcesso> formulario = FormularioProcesso.Criar(finalidade, faseId, titulo, etapas, modeloId, modeloCodigo);
            if (formulario.IsFailure)
            {
                leitor.Propagar<object>(formulario.Error!);
                return ([], []);
            }

            formularios.Add(formulario.Value!);

            HashSet<string> codigos = new(StringComparer.Ordinal);
            int? ordemAnterior = null;
            for (int i = 0; i < itensDeTermo.Count; i++)
            {
                string pathTermo = $"{path}.termos[{i}]";
                TermoExigidoFormulario? termo = LerTermoExigido(leitor, leitor.ItemObjeto(itensDeTermo, i, $"{path}.termos"), pathTermo, finalidade);
                if (leitor.Falhou || termo is null)
                {
                    return ([], []);
                }

                // O encoder emite os termos em ordem estritamente crescente e com código único; outra
                // forma só vem de envelope adulterado.
                if (!codigos.Add(termo.Codigo) || termo.Ordem <= ordemAnterior)
                {
                    leitor.Propagar<object>(new DomainError(
                        ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathTermo}' repete código ou quebra a ordem crescente dos termos."));
                    return ([], []);
                }

                ordemAnterior = termo.Ordem;
                termos.Add(termo);
            }
        }

        return (formularios, termos);
    }

    private static (Guid? Id, string? Codigo) LerModeloDeOrigem(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        if (leitor.ObjetoOpcional(bloco, "modeloOrigem", path) is not { } modelo)
        {
            return (null, null);
        }

        string pathModelo = $"{path}.modeloOrigem";
        leitor.ExigirChaves(modelo, pathModelo, "id", "codigo");
        return (leitor.Identificador(modelo, "id", pathModelo), leitor.TextoOpcional(modelo, "codigo", pathModelo, LimitesDoEnvelope.CodigoModeloDeFormulario));
    }

    private static IReadOnlyList<EtapaFormulario> LerEtapasDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        JsonArray itens = leitor.Array(bloco, "etapas", path);
        if (leitor.Falhou)
        {
            return [];
        }

        List<EtapaFormulario> etapas = [];
        int? ordemAnterior = null;
        for (int i = 0; i < itens.Count; i++)
        {
            string pathEtapa = $"{path}.etapas[{i}]";
            JsonObject item = leitor.ItemObjeto(itens, i, $"{path}.etapas");
            leitor.ExigirChaves(item, pathEtapa, "codigo", "ordem", "tipo", "bloco", "titulo", "descricao", "aviso");
            string codigo = leitor.TextoNaoVazio(item, "codigo", pathEtapa, LimitesDoEnvelope.CodigoEtapaFormulario);
            int ordem = leitor.Inteiro(item, "ordem", pathEtapa);
            string tipo = leitor.TextoNaoVazio(item, "tipo", pathEtapa);
            string? blocoDeSistema = leitor.TextoOpcional(item, "bloco", pathEtapa);
            string titulo = leitor.TextoNaoVazio(item, "titulo", pathEtapa, LimitesDoEnvelope.TituloEtapaFormulario);
            string? descricao = leitor.TextoOpcional(item, "descricao", pathEtapa, LimitesDoEnvelope.TextoEtapaFormulario);
            string? aviso = leitor.TextoOpcional(item, "aviso", pathEtapa, LimitesDoEnvelope.TextoEtapaFormulario);
            if (leitor.Falhou)
            {
                return [];
            }

            // O encoder emite as etapas em ordem estritamente crescente; outra forma só vem de
            // envelope adulterado e não reproduziria os bytes.
            if (ordem <= ordemAnterior)
            {
                return leitor.Propagar<IReadOnlyList<EtapaFormulario>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathEtapa}' quebra a ordem crescente das etapas.")) ?? [];
            }

            ordemAnterior = ordem;
            Result<EtapaFormulario> etapa = EtapaFormulario.Criar(
                codigo, ordem, EstruturaFormulario.TipoDoToken(tipo), EstruturaFormulario.BlocoDoToken(blocoDeSistema), titulo, descricao, aviso);
            if (etapa.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<EtapaFormulario>>(etapa.Error!) ?? [];
            }

            etapas.Add(etapa.Value!);
        }

        return etapas;
    }

    /// <summary>
    /// A obrigatoriedade de um item ou de um termo: <c>SEMPRE</c> ou <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele. Nulo quando a forma é outra, com a recusa já propagada ao leitor.
    /// </summary>
    private static Obrigatoriedade? LerObrigatoriedade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject bloco = leitor.Objeto(item, "obrigatoriedade", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string pathObrigatoriedade = $"{path}.obrigatoriedade";
        leitor.ExigirChaves(bloco, pathObrigatoriedade, "tipo", "predicado");
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathObrigatoriedade);
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas =
            LerDnf(leitor, bloco, "predicado", pathObrigatoriedade);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> predicado = PredicadoOpcional(linhas);
        if (predicado.IsFailure)
        {
            return leitor.Propagar<Obrigatoriedade>(predicado.Error!);
        }

        return (PredicadoDnfJson.TipoDoToken(tipo), predicado.Value) switch
        {
            (TipoObrigatoriedade.Sempre, null) => Obrigatoriedade.Sempre,
            (TipoObrigatoriedade.Nunca, null) => Obrigatoriedade.Nunca,
            (TipoObrigatoriedade.Quando, { } quando) => Obrigatoriedade.Quando(quando),
            _ => leitor.Propagar<Obrigatoriedade>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'{pathObrigatoriedade}' tem tipo SEMPRE ou NUNCA sem predicado, ou QUANDO com predicado.")),
        };
    }

    private static TermoExigidoFormulario? LerTermoExigido(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade)
    {
        leitor.ExigirChaves(item, path, "codigo", "ordem", "termoId", "versaoId", "nome", "texto", "baseLegal",
            "formaAceite", "hashVersao", "exibicao", "obrigatoriedade");

        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoTermoExigido);
        int ordem = leitor.Inteiro(item, "ordem", path);
        Guid termoId = leitor.Identificador(item, "termoId", path);
        Guid versaoId = leitor.Identificador(item, "versaoId", path);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.NomeDoTermo);
        string texto = leitor.TextoNaoVazio(item, "texto", path, LimitesDoEnvelope.TextoDoTermo);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegalDoTermo);
        string formaAceite = leitor.TextoNaoVazio(item, "formaAceite", path, LimitesDoEnvelope.FormaAceiteDoTermo);
        string hash = leitor.TextoNaoVazio(item, "hashVersao", path, LimitesDoEnvelope.HashDaVersaoDoTermo);
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> exibicao = LerDnf(leitor, item, "exibicao", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> exibicaoLida = PredicadoOpcional(exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<TermoExigidoFormulario>(exibicaoLida.Error!);
        }

        if (LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
        {
            return null;
        }

        Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
            codigo, ordem, new VersaoTermoEscolhida(termoId, versaoId, nome, texto, baseLegal, formaAceite, hash),
            exibicaoLida.Value, obrigatoriedade, finalidade);
        return termo.IsSuccess ? termo.Value : leitor.Propagar<TermoExigidoFormulario>(termo.Error!);
    }

    /// <summary>O predicado das linhas lidas; <see langword="null"/> quando não há condição.</summary>
    private static Result<PredicadoDnf?> PredicadoOpcional(
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas)
    {
        if (linhas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        List<(int Clausula, CondicaoDnf Condicao)> condicoes = [];
        foreach ((int clausula, string fato, Operador operador, JsonElement valor) in linhas)
        {
            Result<CondicaoDnf> condicao = CondicaoDnf.Criar(fato, operador, valor);
            if (condicao.IsFailure)
            {
                return Result<PredicadoDnf?>.Failure(condicao.Error!);
            }

            condicoes.Add((clausula, condicao.Value!));
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(condicoes);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050, issue #563) — forma fechada, no molde de
    /// <see cref="LerFormulario"/>. <see cref="LeitorEnvelope.ExigirChaves"/> roda ANTES de
    /// qualquer retorno antecipado: é o que fecha a gramática do bloco agora que a guarda global
    /// de stub saiu — o antigo <c>{"status":"nao_construido"}</c> é recusado exatamente aqui, por
    /// faltarem as três chaves e sobrar <c>status</c>.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder: vocabulário fechado, piso
    /// <c>numero_inscricao</c> sempre presente e <c>nome</c>/<c>nome_abreviado</c> nunca juntos
    /// são reconferidos por <see cref="ConfiguracaoDivulgacao.Criar"/>, que este método chama
    /// como fonte única daquelas invariantes. O que só a LEITURA pode conferir — porque o
    /// congelamento nunca as violaria pelo caminho normal de escrita — fica aqui: repetição,
    /// ordem canônica (pela MESMA política do encoder,
    /// <see cref="SnapshotPublicacaoCanonicalizer.OrdenarPorConteudo(IEnumerable{JsonValue})"/>,
    /// nunca um <see cref="StringComparer.Ordinal"/> reimplementado), a bicondicional entre
    /// <c>nome_abreviado</c> e <c>regraNomeAbreviado</c>, o identificador de regra conhecido, e a
    /// forma canônica (Trim + NFC) da justificativa. Um bloco cujo conteúdo é exatamente o
    /// default minimizado (D5) reidrata como <see langword="null"/> — a restauração não fabrica
    /// entidade para um processo que nunca configurou divulgação.
    /// </remarks>
    private static ConfiguracaoDivulgacao? LerDivulgacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "divulgacao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "divulgacao", "camposPublicos", "regraNomeAbreviado", "justificativa");

        IReadOnlyList<string> camposPublicos = leitor.Textos(bloco, "camposPublicos", "divulgacao");
        string? regraNomeAbreviado = leitor.TextoOpcional(bloco, "regraNomeAbreviado", "divulgacao");
        string? justificativa = leitor.TextoOpcional(bloco, "justificativa", "divulgacao", LimitesDoEnvelope.Justificativa);
        if (leitor.Falhou)
        {
            return null;
        }

        if (camposPublicos.Distinct(StringComparer.Ordinal).Count() != camposPublicos.Count)
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'divulgacao.camposPublicos' tem token repetido."));
        }

        // A MESMA política do encoder (ADR-0109 D9) — nunca um comparador próprio, que
        // coincidiria hoje (três tokens ASCII) e divergiria no dia em que o vocabulário crescesse
        // com um token não-ASCII. Não reordena: fora de ordem é malformado.
        IReadOnlyList<string> naOrdemCanonica = [.. SnapshotPublicacaoCanonicalizer
            .OrdenarPorConteudo(camposPublicos.Select(static c => JsonValue.Create(c)!))
            .Select(static n => n!.GetValue<string>())];
        if (!naOrdemCanonica.SequenceEqual(camposPublicos, StringComparer.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'divulgacao.camposPublicos' não está na ordem canônica."));
        }

        bool temNomeAbreviado = camposPublicos.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal);
        if (temNomeAbreviado != (regraNomeAbreviado is not null))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.regraNomeAbreviado' tem de estar presente se, e somente se, 'camposPublicos' contém 'nome_abreviado'."));
        }

        if (regraNomeAbreviado is not null && !RegrasDeNomeAbreviado.EhConhecida(regraNomeAbreviado))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'divulgacao.regraNomeAbreviado' não é uma regra conhecida: '{regraNomeAbreviado}'."));
        }

        // O codificador NUNCA emite justificativa vazia nem só-espaços — quando não há
        // justificativa, ele emite null (D5). Uma string vazia/em branco só chega aqui por
        // adulteração, e tem de ser recusada ANTES da conferência de Trim/NFC abaixo: uma
        // string vazia já é, trivialmente, a sua própria forma Trim+NFC, então a conferência
        // seguinte não a pegaria, e o teste de default (mais abaixo) também não — ele só
        // reconhece justificativa null, e uma string vazia não é null.
        if (justificativa is not null && string.IsNullOrWhiteSpace(justificativa))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.justificativa' não pode ser vazia nem só espaços — o codificador nunca emite essa forma, só null."));
        }

        if (justificativa is not null
            && !string.Equals(HashCanonicalComputer.NormalizeNfc(justificativa.Trim()), justificativa, StringComparison.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoDivulgacao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'divulgacao.justificativa' não está na forma canônica (sem espaço nas bordas, em NFC)."));
        }

        if (ConfiguracaoDivulgacao.EhDefaultMinimizado(camposPublicos, justificativa))
        {
            return null;
        }

        Result<ConfiguracaoDivulgacao> configuracao = ConfiguracaoDivulgacao.Criar(camposPublicos, justificativa);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoDivulgacao>(configuracao.Error!) : configuracao.Value;
    }

    /// <summary>
    /// Taxa de inscrição e isenção (issue #1112) — forma do bloco no molde do toggle
    /// <c>"presente"</c> de <see cref="LerBonusRegional"/>, mas o desfecho
    /// <c>presente:false</c> nunca é um estado válido AQUI: CA-01 recusa <see cref="Domain.Entities.ProcessoSeletivo.Publicar"/>
    /// quando <see cref="Domain.Entities.ProcessoSeletivo.ConfiguracaoTaxaInscricao"/> é
    /// <see langword="null"/>, então nenhum envelope que passou pelo gate consegue congelar
    /// <c>presente:false</c> — só chega aqui bytes adulterados ou de um caminho que nunca deveria
    /// ter sido aceito. Decodificar como "sem taxa declarada" repetiria, na leitura, o próprio
    /// default silencioso que a issue existe para banir na escrita.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder (mesmo raciocínio de
    /// <see cref="LerDivulgacao"/>): vocabulário fechado de <c>fundamentos</c> (token desconhecido
    /// é malformado, nunca ignorado), sem repetição, na MESMA ordem canônica que
    /// <see cref="SnapshotPublicacaoCanonicalizer.OrdenarPorConteudo(IEnumerable{JsonValue})"/>
    /// usaria — o encoder nunca embaralha porque <see cref="ConfiguracaoTaxaInscricao.Criar"/> já
    /// deduplica e ordena antes de guardar; achar duplicata ou ordem diferente aqui é sinal de
    /// bytes que não vieram desse caminho. <c>cobra:true</c> com <c>fundamentos:[]</c> é a mesma
    /// classe de impossível: a fábrica recusa a combinação (issue #1310), então o encoder nunca
    /// a emite.
    /// </remarks>
    /// <summary>
    /// Lê o bloco da convenção de contagem congelada (UNI-REQ-0112).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O bloco é fechado nas duas formas: ausente, só a chave de presença; presente, a
    /// identidade inteira. A combinação parcial não é representável — uma versão que
    /// declarasse código sem hash não provaria qual definição aplicou, que é a única razão
    /// de o bloco existir.
    /// </para>
    /// <para>
    /// A referência é reconstruída por <see cref="ReferenciaRegra.Criar"/>, e não por
    /// atribuição direta, pela mesma razão da localidade: um envelope adulterado com hash
    /// fora de forma é malformado, e reidratá-lo daria ao processo restaurado uma
    /// referência que o domínio recusaria criar.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Calendário congelado por valor (UNI-REQ-0080). Reconstruído pelos mesmos value objects que
    /// a publicação usa, e não por atribuição direta: a assimetria entre escrita e leitura é
    /// invisível ao round-trip byte a byte — o encoder reemitiria o valor sujo tal qual, a prova
    /// passaria, e a configuração restaurada carregaria um calendário que o domínio recusaria
    /// criar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recusa, nunca normaliza.</b> Aparar espaço ou corrigir caixa aqui faria o valor divergir
    /// dos bytes congelados, e a recanonicalização passaria a recusar um artefato legítimo.
    /// </para>
    /// <para>
    /// A ordem canônica é conferida em vez de reordenada, pela mesma razão: o envelope é comparado
    /// byte a byte, e reordenar em silêncio esconderia um artefato que não foi emitido por este
    /// encoder.
    /// </para>
    /// </remarks>
    private static CalendarioDiasUteisCongelado? LerCalendarioDiasUteis(
        LeitorEnvelope leitor,
        JsonObject payload,
        IReadOnlyList<FaseCronograma> cronogramaFases)
    {
        JsonObject bloco = leitor.Objeto(payload, "calendarioDiasUteis", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "calendarioDiasUteis");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "calendarioDiasUteis", "presente");

            // Invariante entre blocos: nenhuma transição que gera versão publica processo com
            // fase que aceita recurso e sem calendário vigente. Um envelope nessa combinação não
            // foi produzido por publicação legítima, e aceitá-lo restauraria configuração que o
            // gate recusa — o round-trip byte a byte não acusaria, porque o encoder reemitiria a
            // mesma ausência.
            if (cronogramaFases.Any(static fase => fase.RegraRecurso is not null))
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    "'calendarioDiasUteis' declara ausência num processo com fase que aceita recurso — "
                        + "combinação que nenhuma publicação produz."));
            }

            return null;
        }

        leitor.ExigirChaves(bloco, "calendarioDiasUteis", "presente", "origemId", "versaoDataset", "diasNaoUteis");

        Guid origemId = leitor.Identificador(bloco, "origemId", "calendarioDiasUteis");
        string versaoDataset = leitor.TextoNaoVazio(bloco, "versaoDataset", "calendarioDiasUteis");
        JsonArray dias = leitor.Array(bloco, "diasNaoUteis", "calendarioDiasUteis");
        if (leitor.Falhou)
        {
            return null;
        }

        List<DiaNaoUtilCongelado> congelados = [];
        for (int i = 0; i < dias.Count; i++)
        {
            string path = $"calendarioDiasUteis.diasNaoUteis[{i}]";
            JsonObject item = leitor.ItemObjeto(dias, i, path);
            if (leitor.Falhou)
            {
                return null;
            }

            leitor.ExigirChaves(item, path, "data", "abrangencia", "municipioIbge", "municipioNome", "uf");

            DateOnly data = leitor.Data(item, "data", path);
            string abrangencia = leitor.TextoNaoVazio(item, "abrangencia", path);
            string? municipioIbge = leitor.TextoOpcional(item, "municipioIbge", path);
            string? municipioNome = leitor.TextoOpcional(item, "municipioNome", path);
            string? uf = leitor.TextoOpcional(item, "uf", path);
            if (leitor.Falhou)
            {
                return null;
            }

            Result<DiaNaoUtilCongelado> dia = DiaNaoUtilCongelado.Criar(
                data, abrangencia, municipioIbge, municipioNome, uf);
            if (dia.IsFailure)
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' congelado não é um dia não útil válido: {dia.Error!.Message}"));
            }

            // A factory normaliza — apara espaço e sobe a UF para maiúscula —, porque é o caminho
            // de ESCRITA. Aqui isso seria aceitar em silêncio um artefato que este encoder nunca
            // emitiria: o objeto reidratado passaria a divergir dos bytes que o originaram, e a
            // saída de Reidratar deixaria de representar fielmente o envelope. Comparar o lido
            // com o normalizado é o que mantém o decoder fail-closed sem abrir mão da fonte única
            // de validação.
            if (!MesmoTextoOriginal(dia.Value!, abrangencia, municipioIbge, municipioNome, uf))
            {
                return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}' congelado não está na forma canônica — espaço em volta do valor ou UF fora de caixa alta."));
            }

            congelados.Add(dia.Value!);
        }

        // A ordem do artefato tem de ser a canônica. Criar() reordena; comparar antes é o que
        // distingue "envelope emitido por este encoder" de "envelope montado à mão".
        List<DiaNaoUtilCongelado> canonica = [.. CalendarioDiasUteisCongelado.Ordenar(congelados)];
        if (!canonica.SequenceEqual(congelados))
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'calendarioDiasUteis.diasNaoUteis' não está na ordem canônica (data, abrangência, município, UF)."));
        }

        Result<CalendarioDiasUteisCongelado> calendario =
            CalendarioDiasUteisCongelado.Criar(origemId, versaoDataset, congelados);

        if (calendario.IsFailure)
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'calendarioDiasUteis' congelado é inválido: {calendario.Error!.Message}"));
        }

        // Mesma razão da conferência por dia: a factory apara espaço da versão, e aceitar o
        // texto normalizado devolveria um objeto que não representa os bytes decodificados.
        if (!string.Equals(calendario.Value!.VersaoDataset, versaoDataset, StringComparison.Ordinal)
            || !string.Equals(HashCanonicalComputer.NormalizeNfc(versaoDataset), versaoDataset, StringComparison.Ordinal))
        {
            return leitor.Propagar<CalendarioDiasUteisCongelado?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'calendarioDiasUteis.versaoDataset' não está na forma canônica — espaço em volta do valor "
                    + "ou texto fora da normalização Unicode que o encoder aplica."));
        }

        return calendario.Value;
    }

    /// <summary>
    /// Se o dia reconstruído reproduz, caractere a caractere, os textos que estavam no envelope.
    /// Divergir significa que a factory normalizou algo — e o artefato não era o que este encoder
    /// emite.
    /// </summary>
    private static bool MesmoTextoOriginal(
        DiaNaoUtilCongelado dia,
        string abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? uf) =>
        string.Equals(dia.Abrangencia, abrangencia, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioIbge, municipioIbge, StringComparison.Ordinal)
        && string.Equals(dia.MunicipioNome, municipioNome, StringComparison.Ordinal)
        && string.Equals(dia.Uf, uf, StringComparison.Ordinal)
        // O encoder normaliza o nome do município para NFC. Sem exigir o mesmo aqui, um texto
        // decomposto passaria na comparação — a factory só apara espaço — e a recanonicalização
        // mudaria os bytes, revelando a divergência tarde demais, já na restauração.
        && (municipioNome is null
            || string.Equals(HashCanonicalComputer.NormalizeNfc(municipioNome), municipioNome, StringComparison.Ordinal));

    private static ReferenciaRegra? LerAlgoritmoContagemPrazo(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "algoritmoContagemPrazo", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "algoritmoContagemPrazo");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "algoritmoContagemPrazo", "presente");
            return null;
        }

        // Esta forma tem uma chave a mais que a tripla — a de presença —, e é só por isso que o
        // bloco lia a referência por conta própria; ler por conta própria era o que o deixava sem
        // a conferência do rol que todas as outras têm. Declarada a chave extra, fechamento e
        // leitura voltam para o leitor compartilhado.
        ReferenciaRegra referencia = leitor.RegraDoObjeto(
            bloco, "algoritmoContagemPrazo", [.. AlgoritmoContagemPrazoCodigo.Todos], "presente");

        return leitor.Falhou ? null : referencia;
    }

    /// <summary>
    /// Lê o bloco fechado com a localidade regente e o fuso aplicado (UNI-REQ-0111).
    /// </summary>
    /// <remarks>
    /// A localidade é reconstruída por <see cref="LocalidadeRegente.Criar"/>, e não por atribuição
    /// direta: um envelope adulterado com código fora de forma ou UF incoerente é malformado, e
    /// reidratá-lo daria ao processo restaurado uma localidade que o domínio recusaria criar. O
    /// fuso é validado como zona conhecida aqui mesmo — deixar
    /// <c>FindSystemTimeZoneById</c> estourar depois transformaria envelope adulterado em exceção
    /// sem causa nomeada.
    /// </remarks>
    private static (LocalidadeRegente? Localidade, string? FusoHorario) LerLocalidade(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "localidade", "$");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        leitor.ExigirChaves(bloco, "localidade", "codigoIbge", "nome", "uf", "fusoHorario");

        string codigoIbge = leitor.TextoNaoVazio(bloco, "codigoIbge", "localidade");
        string nome = leitor.TextoNaoVazio(bloco, "nome", "localidade");
        string uf = leitor.TextoNaoVazio(bloco, "uf", "localidade");
        string fusoHorario = leitor.TextoNaoVazio(bloco, "fusoHorario", "localidade");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        Result<LocalidadeRegente> localidade = LocalidadeRegente.Criar(codigoIbge, nome, uf);
        if (localidade.IsFailure)
        {
            return (leitor.Propagar<LocalidadeRegente?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'localidade' congelada não é uma referência de cidade válida: {localidade.Error!.Message}")), null);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fusoHorario, out _))
        {
            return (leitor.Propagar<LocalidadeRegente?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'localidade.fusoHorario' congelado ('{fusoHorario}') não é uma zona reconhecida por este ambiente.")), null);
        }

        return (localidade.Value!, fusoHorario);
    }

    private static ConfiguracaoTaxaInscricao? LerTaxaInscricao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "taxaInscricao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'taxaInscricao.presente' não pode ser falso — CA-01 recusa publicar sem declarar taxa."));
        }

        leitor.ExigirChaves(bloco, "taxaInscricao", "presente", "cobra", "valor", "fundamentos");

        bool cobra = leitor.Booleano(bloco, "cobra", "taxaInscricao");
        decimal? valor = leitor.DecimalOpcional(bloco, "valor", ConfiguracaoTaxaInscricao.ValorEscala, "taxaInscricao", LimitesDoEnvelope.PrecisaoTaxaInscricao);
        IReadOnlyList<string> fundamentos = leitor.Textos(bloco, "fundamentos", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (fundamentos.Distinct(StringComparer.Ordinal).Count() != fundamentos.Count)
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'taxaInscricao.fundamentos' tem token repetido."));
        }

        IReadOnlyList<string> fundamentosNaOrdemCanonica = [.. SnapshotPublicacaoCanonicalizer
            .OrdenarPorConteudo(fundamentos.Select(static f => JsonValue.Create(f)!))
            .Select(static n => n!.GetValue<string>())];
        if (!fundamentosNaOrdemCanonica.SequenceEqual(fundamentos, StringComparer.Ordinal))
        {
            return leitor.Propagar<ConfiguracaoTaxaInscricao?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, "'taxaInscricao.fundamentos' não está na ordem canônica."));
        }

        Result<ConfiguracaoTaxaInscricao> configuracao = ConfiguracaoTaxaInscricao.Criar(cobra, valor, fundamentos);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoTaxaInscricao>(configuracao.Error!) : configuracao.Value;
    }
}
