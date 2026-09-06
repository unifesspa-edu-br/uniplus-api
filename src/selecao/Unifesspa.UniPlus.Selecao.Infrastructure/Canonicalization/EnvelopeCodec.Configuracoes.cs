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

/// <summary>
/// Leitura dos blocos de configuração autocontidos do envelope: <c>formulario</c>,
/// <c>divulgacao</c>, <c>taxaInscricao</c>, <c>localidade</c>, <c>algoritmoContagemPrazo</c>
/// e <c>calendarioDiasUteis</c>.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// Título e termo de aceite do formulário de inscrição (Story #559) — forma fechada mesmo
    /// quando os dois campos são nulos, mesmo raciocínio de <see cref="LerDadosEdital"/> para
    /// campos individualmente opcionais (não um toggle por presença como
    /// <see cref="LerBonusRegional"/>).
    /// </summary>
    private static (string? Titulo, string? TermoAceiteTexto) LerFormulario(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "formulario", "$");
        if (leitor.Falhou)
        {
            return (null, null);
        }

        leitor.ExigirChaves(bloco, "formulario", "titulo", "termoAceiteTexto");

        string? titulo = leitor.TextoOpcional(bloco, "titulo", "formulario", LimitesDoEnvelope.NomeDeCadastro);
        string? termoAceiteTexto = leitor.TextoOpcional(bloco, "termoAceiteTexto", "formulario", LimitesDoEnvelope.TermoDeAceite);
        return leitor.Falhou ? (null, null) : (titulo, termoAceiteTexto);
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

        leitor.ExigirChaves(bloco, "algoritmoContagemPrazo", "presente", "codigo", "versao", "hash");

        string codigo = leitor.TextoNaoVazio(bloco, "codigo", "algoritmoContagemPrazo");
        string versao = leitor.TextoNaoVazio(bloco, "versao", "algoritmoContagemPrazo");
        string hash = leitor.TextoNaoVazio(bloco, "hash", "algoritmoContagemPrazo");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(codigo, versao, hash);
        return referencia.IsFailure
            ? leitor.Propagar<ReferenciaRegra?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'algoritmoContagemPrazo' congelado não é uma referência de regra válida: {referencia.Error!.Message}"))
            : referencia.Value;
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
