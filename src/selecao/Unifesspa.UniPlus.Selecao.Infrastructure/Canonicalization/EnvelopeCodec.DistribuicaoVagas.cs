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
/// Leitura dos blocos de distribuição de vagas: <c>distribuicao</c>, <c>modalidades</c>,
/// <c>vagas</c>, <c>ofertas</c> e <c>atendimento</c> (oferta de atendimento especializado).
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// As ações ao indeferir que o <b>cadastro</b> admite — domínio fechado
    /// (<c>AcoesQuandoIndeferido</c>, módulo Configuração). O comando nunca produz outra
    /// coisa: ele <b>copia</b> o token da view do cadastro.
    /// </summary>
    /// <remarks>
    /// O envelope o congela por valor (ADR-0061), e o decoder não passa pelo cadastro — sem
    /// esta lista, um token inventado faria round-trip perfeito e restauraria uma
    /// <b>instrução que o motor de homologação não sabe executar</b>. A lista está aqui, e
    /// não numa referência ao módulo Configuração, porque ela é a do vocabulário
    /// <b>congelado</b> nesta forma do envelope: um token novo no cadastro exige atualizar
    /// esta lista.
    /// </remarks>
    private static readonly string[] AcoesQuandoIndeferido =
        ["RECLASSIFICAR_AC", "RECLASSIFICAR_REGRA_EDITAL"];

    /// <summary>
    /// O formato que o <c>CodigoModalidade</c> do cadastro impõe (<c>^[A-Z0-9_]+$</c>). Um
    /// código fora dele nunca sai do cadastro — mas é <b>chave</b> de composição e de
    /// remanejamento dentro da oferta, e um envelope adulterado o usaria como tal.
    /// </summary>
    private static readonly Regex FormatoCodigoModalidade =
        new("^[A-Z0-9_]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// O formato que o <c>CodigoCondicao</c> do cadastro impõe
    /// (<c>^[A-Z][A-Z0-9_]{1,49}$</c>). O código da condição é <b>chave natural</b> — é por
    /// ele que a invariante ADR-0067 reconhece a condição PcD (<c>CodigoCondicaoPcd</c>) —,
    /// e um <c>"pcd"</c> minúsculo restaurado do envelope seria uma condição que o cadastro
    /// não produz e que o resto do código trata como chave.
    /// </summary>
    private static readonly Regex FormatoCodigoCondicao =
        new("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Recombina os <b>três blocos derivados</b> da mesma coleção — <c>distribuicao</c>,
    /// <c>modalidades</c> e <c>ofertas</c> (ADR-0110 D8). A exigência é <b>igualdade
    /// exata de conjuntos</b> de <c>ofertaCursoOrigemId</c>, não mera inclusão: recombinar
    /// em silêncio um envelope incoerente reconstruiria um agregado que <b>nunca
    /// existiu</b> — uma modalidade sem oferta, uma oferta sem vagas.
    /// </summary>
    private static IReadOnlyList<ConfiguracaoDistribuicaoVagas> LerDistribuicao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray arrayDistribuicao = leitor.Array(payload, "distribuicao", "$");
        JsonArray arrayModalidades = leitor.Array(payload, "modalidades", "$");
        JsonArray arrayVagas = leitor.Array(payload, "vagas", "$");
        IReadOnlyList<string> ofertasDeclaradas = leitor.Textos(payload, "ofertas", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        // O bloco `vagas` (issue #848/ADR-0115) é validado por forma — cada entrada tem de
        // existir, ter chave estrutural correta e cobrir a mesma oferta que `distribuicao`.
        // O VALOR (o quadro em si) NÃO é lido daqui: ele é recomputado por
        // ConfiguracaoDistribuicaoVagas.Criar a partir dos insumos (voBase/pr/regras/
        // modalidades com quantidadeDeclarada), que é o que prova a reprodutibilidade
        // não-circular (CA-13) — ler e injetar o valor congelado tornaria a prova tautológica.
        List<Guid> ofertasEmVagas = [];
        for (int i = 0; i < arrayVagas.Count; i++)
        {
            string path = $"vagas[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayVagas, i, "vagas");
            leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "quadro", "vrNominal", "vrFinal", "estouro", "capadoEmVo", "totalPublicado");

            Guid ofertaVagaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            JsonArray quadroArray = leitor.Array(item, "quadro", path);
            leitor.Inteiro(item, "vrNominal", path);
            leitor.Inteiro(item, "vrFinal", path);
            leitor.Inteiro(item, "estouro", path);
            leitor.Booleano(item, "capadoEmVo", path);
            leitor.Inteiro(item, "totalPublicado", path);

            if (leitor.Falhou)
            {
                return [];
            }

            for (int j = 0; j < quadroArray.Count; j++)
            {
                string quadroPath = $"{path}.quadro[{j}]";
                JsonObject linha = leitor.ItemObjeto(quadroArray, j, $"{path}.quadro");
                leitor.ExigirChaves(linha, quadroPath, "modalidadeCodigo", "quantidade");
                leitor.TextoNaoVazio(linha, "modalidadeCodigo", quadroPath, LimitesDoEnvelope.ModalidadeCodigo);
                leitor.Inteiro(linha, "quantidade", quadroPath);
            }

            if (leitor.Falhou)
            {
                return [];
            }

            ofertasEmVagas.Add(ofertaVagaId);
        }

        Dictionary<Guid, List<ModalidadeSelecionada>> modalidadesPorOferta = [];
        List<Guid> ofertasEmModalidades = [];
        for (int i = 0; i < arrayModalidades.Count; i++)
        {
            string path = $"modalidades[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayModalidades, i, "modalidades");
            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            ModalidadeSelecionada? modalidade = LerModalidade(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            ofertasEmModalidades.Add(ofertaId);
            if (!modalidadesPorOferta.TryGetValue(ofertaId, out List<ModalidadeSelecionada>? lista))
            {
                lista = [];
                modalidadesPorOferta[ofertaId] = lista;
            }

            lista.Add(modalidade!);
        }

        List<Guid> ofertasEmDistribuicao = [];
        List<(Guid Oferta, int VoBase, decimal Pr, ReferenciaRegra Regra, ReferenciaRegra? RegraAjuste, ReferenciaReservaDemograficaSnapshot? Demografica)> inputs = [];
        for (int i = 0; i < arrayDistribuicao.Count; i++)
        {
            string path = $"distribuicao[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayDistribuicao, i, "distribuicao");
            leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "voBase", "pr", "regraDistribuicao", "regraAjuste", "referenciaDemografica");

            Guid ofertaId = leitor.Identificador(item, "ofertaCursoOrigemId", path);
            int voBase = leitor.Inteiro(item, "voBase", path);
            decimal pr = leitor.Decimal(item, "pr", EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPr);
            // O rol deriva de RegraDistribuicaoVagasCodigo.Todos, não de literais fixos: um
            // código federal (EhRamoFederal) ou de quadro fixo (EhQuadroFixo) publicado sob
            // qualquer regra do catálogo tem de reidratar — dois literais aqui já deixaram
            // PSIQ e a regra que hoje é COM-PCD-PURO irreidratáveis antes desta correção.
            ReferenciaRegra regra = leitor.Regra(
                item,
                "regraDistribuicao",
                path,
                [.. RegraDistribuicaoVagasCodigo.Todos]);
            ReferenciaRegra? regraAjuste = leitor.RegraOpcional(
                item,
                "regraAjuste",
                path,
                RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico);
            ReferenciaReservaDemograficaSnapshot? demografica = LerReferenciaDemografica(leitor, item, path);

            if (leitor.Falhou)
            {
                return [];
            }

            ofertasEmDistribuicao.Add(ofertaId);
            inputs.Add((ofertaId, voBase, pr, regra, regraAjuste, demografica));
        }

        if (VerificarBlocosDerivados(ofertasEmDistribuicao, ofertasEmModalidades, ofertasEmVagas, ofertasDeclaradas) is { } incoerencia)
        {
            return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(incoerencia) ?? [];
        }

        List<ConfiguracaoDistribuicaoVagas> distribuicao = [];
        foreach ((Guid oferta, int voBase, decimal pr, ReferenciaRegra regra, ReferenciaRegra? regraAjuste, ReferenciaReservaDemograficaSnapshot? demografica) in inputs)
        {
            Result<ConfiguracaoDistribuicaoVagas> configuracao = ConfiguracaoDistribuicaoVagas.Criar(
                oferta, voBase, pr, regra, regraAjuste, demografica, modalidadesPorOferta[oferta],
                modalidadesAdmitidas: RegraDistribuicaoVagasCodigo.RolFechado(regra.Codigo));
            if (configuracao.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>(configuracao.Error!) ?? [];
            }

            distribuicao.Add(configuracao.Value!);
        }

        return distribuicao;
    }

    private static DomainError? VerificarBlocosDerivados(
        List<Guid> emDistribuicao,
        List<Guid> emModalidades,
        List<Guid> emVagas,
        IReadOnlyList<string> emOfertas)
    {
        if (emDistribuicao.Distinct().Count() != emDistribuicao.Count)
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "O bloco 'distribuicao' repete uma oferta de curso — cada oferta tem no máximo uma distribuição.");
        }

        if (emVagas.Distinct().Count() != emVagas.Count)
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "O bloco 'vagas' repete uma oferta de curso — cada oferta tem no máximo um quadro.");
        }

        List<Guid> ofertas = [];
        foreach (string texto in emOfertas)
        {
            if (!Guid.TryParseExact(texto, "D", out Guid oferta)
                || !string.Equals(oferta.ToString(), texto, StringComparison.Ordinal)
                || oferta == Guid.Empty)
            {
                return new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'ofertas': '{texto}' não é um Guid canônico não vazio.");
            }

            ofertas.Add(oferta);
        }

        if (ofertas.Distinct().Count() != ofertas.Count)
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "O bloco 'ofertas' repete uma oferta de curso.");
        }

        HashSet<Guid> conjuntoDistribuicao = [.. emDistribuicao];
        HashSet<Guid> conjuntoOfertas = [.. ofertas];
        HashSet<Guid> conjuntoModalidades = [.. emModalidades];
        HashSet<Guid> conjuntoVagas = [.. emVagas];

        if (!conjuntoDistribuicao.SetEquals(conjuntoOfertas)
            || !conjuntoDistribuicao.SetEquals(conjuntoModalidades)
            || !conjuntoDistribuicao.SetEquals(conjuntoVagas))
        {
            return new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "Os blocos 'distribuicao', 'modalidades', 'vagas' e 'ofertas' derivam da mesma coleção e não declaram o mesmo conjunto de ofertas de curso (ADR-0110 D8).");
        }

        return null;
    }

    private static ReferenciaReservaDemograficaSnapshot? LerReferenciaDemografica(
        LeitorEnvelope leitor,
        JsonObject distribuicao,
        string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "referenciaDemografica", pathPai);
        if (leitor.Falhou || item is null)
        {
            return null;
        }

        string path = $"{pathPai}.referenciaDemografica";
        leitor.ExigirChaves(
            item,
            path,
            "origemId",
            "censoReferencia",
            "ppiPercentual",
            "quilombolaPercentual",
            "pcdPercentual",
            "baseLegal");

        Guid origemId = leitor.Identificador(item, "origemId", path);
        string censo = leitor.TextoNaoVazio(item, "censoReferencia", path, LimitesDoEnvelope.CensoReferencia);
        decimal ppi = leitor.Decimal(item, "ppiPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal quilombola = leitor.Decimal(item, "quilombolaPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        decimal pcd = leitor.Decimal(item, "pcdPercentual", EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);

        if (leitor.Falhou)
        {
            return null;
        }

        Result<ReferenciaReservaDemograficaSnapshot> referencia =
            ReferenciaReservaDemograficaSnapshot.Criar(origemId, censo, ppi, quilombola, pcd, baseLegal);

        return referencia.IsFailure
            ? leitor.Propagar<ReferenciaReservaDemograficaSnapshot>(referencia.Error!)
            : referencia.Value;
    }

    private static ModalidadeSelecionada? LerModalidade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(
            item,
            path,
            "ofertaCursoOrigemId",
            "modalidadeOrigemId",
            "codigo",
            "descricao",
            "naturezaLegal",
            "composicaoVagas",
            "composicaoOrigemCodigo",
            "regraRemanejamento",
            "remanejamentoDestino",
            "remanejamentoPar",
            "remanejamentoFallback",
            "criteriosCumulativos",
            "acaoQuandoIndeferido",
            "baseLegal",
            "quantidadeDeclarada");

        Guid modalidadeOrigemId = leitor.Identificador(item, "modalidadeOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? descricao = leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.ModalidadeDescricao);
        NaturezaLegalModalidade natureza = leitor.Enumeracao<NaturezaLegalModalidade>(item, "naturezaLegal", path);
        ComposicaoVagasModalidade composicao = leitor.Enumeracao<ComposicaoVagasModalidade>(item, "composicaoVagas", path);
        string? composicaoOrigem = leitor.TextoOpcional(item, "composicaoOrigemCodigo", path, LimitesDoEnvelope.ModalidadeCodigo);
        RegraRemanejamentoModalidade remanejamento = leitor.Enumeracao<RegraRemanejamentoModalidade>(item, "regraRemanejamento", path);
        string? destino = leitor.TextoOpcional(item, "remanejamentoDestino", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? par = leitor.TextoOpcional(item, "remanejamentoPar", path, LimitesDoEnvelope.ModalidadeCodigo);
        string? fallback = leitor.TextoOpcional(item, "remanejamentoFallback", path, LimitesDoEnvelope.ModalidadeCodigo);

        // A ordem de `criteriosCumulativos` no envelope é a CANÔNICA (issue #1067, ADR-0109
        // D9) — o array é um conjunto sem posição própria entre os critérios, e o encoder o
        // ordena pela chave de conteúdo de cada item, não pela ordem de entrada. O decoder não
        // reordena nada aqui: lê a sequência tal como está no JSON, que já é a canônica, e é
        // essa mesma sequência que a recanonicalização tem de reproduzir byte a byte.
        IReadOnlyList<string> criterios = leitor.Textos(item, "criteriosCumulativos", path);
        string? acaoQuandoIndeferido = leitor.TextoOpcional(item, "acaoQuandoIndeferido", path, LimitesDoEnvelope.Token);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);
        int? quantidadeDeclarada = leitor.InteiroOpcional(item, "quantidadeDeclarada", path);

        if (leitor.Falhou)
        {
            return null;
        }

        if (VocabularioDaModalidade(codigo, acaoQuandoIndeferido, composicaoOrigem, destino, par, fallback) is { } vocabulario)
        {
            return leitor.Propagar<ModalidadeSelecionada>(vocabulario);
        }

        if (CoerenciaNaturezaRemanejamento(codigo, natureza, remanejamento) is { } incoerencia)
        {
            return leitor.Propagar<ModalidadeSelecionada>(incoerencia);
        }

        Result<ModalidadeSelecionada> modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId,
            codigo,
            descricao,
            natureza,
            composicao,
            composicaoOrigem,
            remanejamento,
            destino,
            par,
            fallback,
            criterios,
            acaoQuandoIndeferido,
            baseLegal,
            quantidadeDeclarada);

        return modalidade.IsFailure ? leitor.Propagar<ModalidadeSelecionada>(modalidade.Error!) : modalidade.Value;
    }

    /// <summary>
    /// O <b>vocabulário</b> da modalidade — os tokens que só o cadastro produz.
    /// </summary>
    /// <remarks>
    /// O código é <b>chave</b>: a composição (<c>RETIRA_DE</c>) e o remanejamento
    /// (<c>DESTINO_UNICO</c>, <c>CRUZADO</c>) apontam para códigos de outras modalidades da
    /// mesma oferta. Um código fora do formato do cadastro (<c>^[A-Z0-9_]+$</c>) nunca sai
    /// de lá — mas um envelope adulterado o usaria como chave, e o motor de vagas do certame
    /// receberia um grafo de remanejamento cujos nós não existem no cadastro.
    /// </remarks>
    private static DomainError? VocabularioDaModalidade(
        string codigo,
        string? acaoQuandoIndeferido,
        string? composicaoOrigem,
        string? destino,
        string? par,
        string? fallback)
    {
        IEnumerable<string> foraDoFormato = new[] { codigo, composicaoOrigem, destino, par, fallback }
            .Where(static c => c is not null)
            .Select(static c => c!)
            .Where(c => !FormatoCodigoModalidade.IsMatch(c));

        foreach (string cruzamento in foraDoFormato)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': o código '{cruzamento}' não tem o formato do cadastro " +
                "(A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");
        }

        if (acaoQuandoIndeferido is not null && !AcoesQuandoIndeferido.Contains(acaoQuandoIndeferido, StringComparer.Ordinal))
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': a ação ao indeferir '{acaoQuandoIndeferido}' não pertence ao " +
                $"domínio fechado do cadastro ({string.Join(", ", AcoesQuandoIndeferido)}) — restaurá-la daria ao " +
                "motor de homologação uma instrução que ele não sabe executar.");
        }

        return null;
    }

    /// <summary>
    /// A coerência entre a <b>natureza legal</b> e a <b>regra de remanejamento</b>, congelada
    /// nesta forma do envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A regra é do <b>cadastro</b> de modalidades
    /// (<c>Modalidade.ValidarCoerenciaNaturezaRemanejamento</c>, módulo Configuração), e é
    /// por isso que o caminho de comando <b>nunca</b> a viola: o handler não lê estes campos
    /// do payload — <b>copia-os da view do cadastro</b>. Mas o snapshot-copy (ADR-0061)
    /// congela os três <b>por valor</b>, e quem reconstrói a modalidade a partir dos bytes
    /// não passa pelo cadastro. <c>ModalidadeSelecionada.Criar</c> só replica <b>uma</b> das
    /// três (a INV-12).
    /// </para>
    /// <para>
    /// Sem as outras duas, um envelope adulterado com
    /// <c>{naturezaLegal: "Ampla", regraRemanejamento: "DestinoUnico"}</c> restaura uma
    /// <b>ampla concorrência que remaneja as próprias vagas ociosas</b> — configuração que o
    /// caminho de escrita jamais produz, que faz round-trip <b>perfeito</b> (o encoder
    /// reemite os enums verbatim) e que é <b>input do motor de vagas</b> do certame.
    /// </para>
    /// <para>
    /// <b>Por que aqui, e não em <c>ModalidadeSelecionada.Criar</c>.</b> A entidade é a cópia
    /// congelada de uma modalidade — não a modalidade viva. Se o cadastro mudar a tabela de
    /// coerência amanhã, um snapshot <b>legítimo</b> de hoje passaria a ser irreidratável, e
    /// o certame publicado ficaria sem descarte: exatamente o que o congelamento existe para
    /// impedir. Aqui, a tabela é a que esta forma do envelope congelou — uma regra nova é
    /// mudança de forma, que reescreve a fixture enquanto não há certame publicado a
    /// preservar (ver <see cref="EnvelopeCodec"/>).
    /// </para>
    /// </remarks>
    private static DomainError? CoerenciaNaturezaRemanejamento(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento) => natureza switch
        {
            // A INV-12 já é da entidade; repeti-la aqui seria redundância — ela cai na
            // factory logo abaixo. As duas que faltam:
            NaturezaLegalModalidade.Ampla when remanejamento != RegraRemanejamentoModalidade.Nenhuma =>
                new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'modalidades': {codigo} é de ampla concorrência e não admite regra de " +
                    "remanejamento — as vagas ociosas dela não vão para lugar nenhum."),

            NaturezaLegalModalidade.Suplementar or NaturezaLegalModalidade.OutraModalidade
                when remanejamento is not (RegraRemanejamentoModalidade.DestinoUnico or RegraRemanejamentoModalidade.Cruzado) =>
                new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'modalidades': {codigo} é suplementar ou de outra natureza e exige " +
                    "regra de remanejamento DESTINO_UNICO ou CRUZADO."),

            _ => null,
        };

    private static OfertaAtendimentoEspecializado? LerAtendimento(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "atendimento", "$");
        leitor.ExigirChaves(bloco, "atendimento", "condicoes", "recursos", "tiposDeficiencia");

        JsonArray arrayCondicoes = leitor.Array(bloco, "condicoes", "atendimento");
        JsonArray arrayRecursos = leitor.Array(bloco, "recursos", "atendimento");
        JsonArray arrayTipos = leitor.Array(bloco, "tiposDeficiencia", "atendimento");
        if (leitor.Falhou)
        {
            return null;
        }

        List<OfertaCondicao> condicoes = [];
        for (int i = 0; i < arrayCondicoes.Count; i++)
        {
            string path = $"atendimento.condicoes[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayCondicoes, i, "atendimento.condicoes");
            leitor.ExigirChaves(item, path, "condicaoOrigemId", "condicaoCodigo", "condicaoNome");

            Guid origemId = leitor.Identificador(item, "condicaoOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "condicaoCodigo", path, LimitesDoEnvelope.CondicaoCodigo);
            if (!leitor.Falhou && !FormatoCodigoCondicao.IsMatch(codigo))
            {
                // O código da condição é CHAVE NATURAL: é por ele que a invariante ADR-0067
                // reconhece a condição PcD (OfertaAtendimentoEspecializado.CodigoCondicaoPcd).
                // Um "pcd" minúsculo, restaurado de um envelope adulterado, seria uma condição
                // que o cadastro nunca produz — e que o resto do código trata como chave.
                return leitor.Propagar<OfertaAtendimentoEspecializado>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{path}': o código de condição '{codigo}' não tem o formato do " +
                    "cadastro (maiúscula inicial, depois maiúsculas, dígitos e underscore)."));
            }

            string nome = leitor.TextoNaoVazio(item, "condicaoNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            condicoes.Add(OfertaCondicao.Criar(origemId, codigo, nome));
        }

        List<OfertaRecurso> recursos = [];
        for (int i = 0; i < arrayRecursos.Count; i++)
        {
            string path = $"atendimento.recursos[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayRecursos, i, "atendimento.recursos");
            leitor.ExigirChaves(item, path, "recursoOrigemId", "recursoNome");

            Guid origemId = leitor.Identificador(item, "recursoOrigemId", path);
            string nome = leitor.TextoNaoVazio(item, "recursoNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            recursos.Add(OfertaRecurso.Criar(origemId, nome));
        }

        List<OfertaTipoDeficiencia> tipos = [];
        for (int i = 0; i < arrayTipos.Count; i++)
        {
            string path = $"atendimento.tiposDeficiencia[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayTipos, i, "atendimento.tiposDeficiencia");
            leitor.ExigirChaves(item, path, "tipoDeficienciaOrigemId", "tipoDeficienciaCodigo", "tipoDeficienciaNome");

            Guid origemId = leitor.Identificador(item, "tipoDeficienciaOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "tipoDeficienciaCodigo", path, LimitesDoEnvelope.TipoDeficienciaCodigo);
            string nome = leitor.TextoNaoVazio(item, "tipoDeficienciaNome", path, LimitesDoEnvelope.NomeDeCadastro);
            if (leitor.Falhou)
            {
                return null;
            }

            tipos.Add(OfertaTipoDeficiencia.Criar(origemId, codigo, nome));
        }

        Result<OfertaAtendimentoEspecializado> oferta = OfertaAtendimentoEspecializado.Criar(condicoes, recursos, tipos);
        return oferta.IsFailure ? leitor.Propagar<OfertaAtendimentoEspecializado>(oferta.Error!) : oferta.Value;
    }
}
