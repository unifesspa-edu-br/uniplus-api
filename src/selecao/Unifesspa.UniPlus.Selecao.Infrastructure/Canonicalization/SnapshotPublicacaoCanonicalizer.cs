namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Implementação da projeção canônica do envelope de congelamento (ADR-0100,
/// ADR-0109). Projeta a configuração viva do agregado num payload de <b>26 blocos
/// reais</b> — e devolve os bytes via <see cref="PerfilCanonicoV1"/>.
/// <c>documentosExigidos</c> (Story #853) já carrega <c>obrigatoriedades[]</c> e
/// <c>exigencias[]</c> (#554) reais. <c>vagas</c> (issue #848/ADR-0115) é outro: o
/// quadro por oferta, calculado no ramo federal ou fixado no institucional, sempre
/// materializado junto da configuração. <c>arvoreSatisfacao</c> (Story #923): a
/// topologia da árvore de satisfação de <c>documentosExigidos</c>, sempre
/// materializada (0..* raízes). <c>formulario</c> (Story #559): título, termo de
/// aceite do formulário de inscrição — forma fechada mesmo quando os dois campos
/// são nulos. <c>divulgacao</c> (UNI-REQ-0050, issue #563) é o mais novo: os campos
/// publicados nas listas de resultado, a regra de abreviação derivada e a
/// justificativa — forma fechada, default minimizado quando não configurada.
/// </summary>
/// <remarks>
/// <para>
/// Todo campo string de negócio passa por <see cref="HashCanonicalComputer.NormalizeNfc"/>;
/// todo decimal por <see cref="HashCanonicalComputer.SerializeDecimalCanonical"/>
/// com a escala declarada do campo; instantes por
/// <see cref="HashCanonicalComputer.SerializeInstantCanonical"/>. A
/// reordenação de chaves e a serialização byte-estável final acontecem no
/// <see cref="IPerfilCanonico"/> — este tipo só monta o <see cref="JsonObject"/>
/// com os valores já normalizados. A normalização NFC é responsabilidade daqui,
/// não do perfil: o perfil serializa o nó que recebe, e uma sequência combinante
/// que chegasse até ele produziria bytes distintos dos da forma pré-composta.
/// </para>
/// <para>
/// <strong>Leitura do bloco "vagas" vs. "distribuição" (ADR-0100 item 10, issue #848/ADR-0115):</strong>
/// <c>ConfiguracaoDistribuicaoVagas</c> é o conjunto de INPUTS (voBase, PR, regra, modalidades);
/// "vagas" é o <c>QuadroDeVagas</c> — a quantidade por modalidade, calculada no ramo federal
/// (Lei 12.711) ou fixada no institucional — sempre materializado junto da configuração, não
/// mais stub. Congelar o insumo ("distribuição") ao lado do output derivado ("vagas") é o que
/// torna a prova de reprodutibilidade não-tautológica: reidratar recompõe o insumo, o agregado
/// recalcula o quadro a partir dele, e o round-trip compara os bytes dos dois blocos.
/// </para>
/// <para>
/// <strong>Ordenação determinística das coleções — composição hierárquica (ADR-0109, emenda
/// #1069):</strong> <c>IProcessoSeletivoRepository.ObterComConfiguracaoAsync</c> não aplica
/// <c>ORDER BY</c> aos <c>Include</c> das coleções filhas — a ordem física devolvida pelo
/// Postgres para a MESMA linha pode variar entre leituras (plano de execução, VACUUM, etc.). Como
/// a ordem de um array entra no hash, todo bloco baseado em coleção é ordenado antes de
/// serializar — mas a política <b>não é a escolha de uma entre três camadas</b>; é a composição
/// delas em sequência, cada uma decidindo só o que a anterior deixou empatado:
/// </para>
/// <list type="number">
///   <item><c>Ordem</c> semântica, quando o campo existe — etapas (<c>Ordem</c> nulo ao fim),
///   critérios de desempate, fases do cronograma, destinos da cascata dentro de uma origem,
///   raízes/filhos da árvore de satisfação (a unicidade de <c>Ordem</c> por índice único dispensa
///   qualquer camada seguinte) e <c>valoresDominioDeclarados</c> (<c>Ordem</c>, depois
///   <c>Codigo</c> como desempate de negócio);</item>
///   <item>identidade de negócio única, quando existe — oferta por <c>OfertaCursoOrigemId</c> e,
///   dentro da oferta, modalidade por <c>Codigo</c> ordinal (não <c>ModalidadeOrigemId</c>: ordenar
///   pelo Guid técnico da linha seria o vazamento de identidade técnica para o hash que esta camada
///   existe para evitar); condição/recurso/banca por seus <c>*OrigemId</c>; exigências documentais
///   por fase mais tipo de documento (chave PARCIAL: só reduz o empate ao caso raro, sem discriminar
///   a coleção inteira sozinha);</item>
///   <item>a <b>chave de conteúdo</b> (ADR-0109 D9), usada onde não há chave natural SUFICIENTE
///   para decidir a posição sozinha: os bytes canônicos do próprio item, comparados como bytes
///   (<see cref="ComparadorLexicograficoDeBytes"/>), não como texto decodificado. Regras de
///   eliminação, critérios cumulativos, ocorrências esperadas e obrigatoriedades (sem nenhuma chave
///   de negócio) usam esta camada — e também, explicitamente, o desempate residual de exigências
///   com fase+tipo idênticos: a identidade da camada anterior é PARCIAL ali, não suficiente para
///   decidir sozinha.</item>
/// </list>
/// <para>
/// Por cima do resultado das três, um desempate <b>técnico</b> final por <c>EntityBase.Id</c>
/// estabiliza a rara duplicata verdadeira — só onde o Id já é congelado no envelope (etapa,
/// exigência). Não é código morto: o domínio recusa <c>Ordem</c> INFORMADA duplicada, não conteúdo
/// repetido, e o índice único do banco admite múltiplos <c>NULL</c> — duas etapas classificatórias
/// com o mesmo nome, caráter, peso e nota mínima, ambas sem <c>Ordem</c>, são aceitas e precisam de
/// um desempate alcançável. Ordenar por <c>Id</c> sozinho, sem as camadas de conteúdo/identidade
/// antes dele, era determinístico entre leituras da MESMA linha, mas não entre configurações
/// equivalentes: duas linhas idênticas inseridas em ordem inversa recebem Guids v7 distintos e
/// produziriam bytes distintos para a mesma configuração.
/// </para>
/// <para>
/// <c>grafoDependencia.nos</c>/<c>arestas</c>/<c>ordemTopologica</c> NÃO é uma quarta camada desta
/// lista — é ordem canônica <b>derivada pelo domínio e apenas preservada</b> aqui:
/// <see cref="Domain.ValueObjects.GrafoDependenciaConjunta"/> já devolve os três na sua forma final
/// (Kahn, com a ordem de coleta efetiva como prioridade e <c>(Classe, Codigo)</c> como desempate),
/// e a projeção não reordena o que recebe.
/// </para>
/// </remarks>
public sealed class SnapshotPublicacaoCanonicalizer : ISnapshotPublicacaoCanonicalizer
{
    /// <summary>
    /// Versão da <b>forma</b> do envelope (ADR-0109 D1). Não há CHECK de
    /// <c>schema_version</c> no banco, nem produção nem certame congelado ainda:
    /// o codec pré-produção é <b>um só</b> (não um por versão), reescrito no
    /// lugar sob a mesma versão corrente quando a forma muda — bump livre,
    /// sem migration, mas também sem obrigação de bump a cada mudança. O
    /// versionamento forense (um bump por chave nova ou por stub virando
    /// conteúdo real, com encoder anterior aposentado) começa a valer a
    /// partir do primeiro certame publicado em qualquer ambiente, inclusive
    /// homologação — não da primeira release de produção. Toda versão aqui
    /// declarada tem de ter a sua golden fixture correspondente — um teste
    /// de política falha o build se não tiver.
    /// </summary>
    /// <remarks>
    /// Story #575: <c>cascataRemanejamento</c> sai de stub para bloco real
    /// (RN-CASCATA-1..5) sob a MESMA <c>0.0.2</c> — reescrita no lugar, sem
    /// bump, pelo regime pré-produção descrito acima.
    /// Story #919 (RN08): <c>documentosExigidos</c> ganha a sub-chave
    /// <c>metadadosFatos</c> (<see cref="SerializarMetadadosFatos"/>). O bump para
    /// <c>1.3</c> também é o primeiro <c>schema_version</c> a refletir corretamente
    /// <c>formatosPermitidos</c> (Story #918, PR anterior) — que ficou sem bump próprio;
    /// não se retrabalha aquela PR, só se reconhece que "1.3" é a forma real de hoje.
    /// Story #923 (snapshot conjunto final, PR 4/4 da change
    /// <c>documentos-exigidos-composicao</c>): o bump para <c>1.4</c> acrescenta o 14º
    /// bloco de topo, <c>arvoreSatisfacao</c> — a TOPOLOGIA da árvore de satisfação
    /// (<see cref="Domain.Entities.NoExigencia"/>, Stories #920/#921/#922), até aqui
    /// fail-closed na publicação (<see cref="Domain.Entities.ProcessoSeletivo.PendenciaDaArvoreDeSatisfacaoAindaNaoPublicavel"/>,
    /// removido nesta Story) por não ter onde congelar. <c>documentosExigidos.exigencias</c>
    /// continua sendo a config POR-exigência (folha); <c>arvoreSatisfacao</c> é a
    /// TOPOLOGIA (grupos E/OU, cardinalidade de grupo, repetição por entidade) — cada
    /// folha da árvore referencia sua exigência pelo mesmo <c>exigenciaId</c> já congelado
    /// em <c>documentosExigidos.exigencias[].exigenciaId</c>, sem duplicar conteúdo.
    /// Story #850: o bump para <c>0.0.4</c> acrescenta <c>baseadoEmEnem</c> ao bloco
    /// <c>classificacao</c> — o sinal explícito que substitui a ramificação por
    /// <see cref="Domain.Entities.ProcessoSeletivo.TipoProcesso"/> na aceitação de
    /// <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c>.
    /// Story #559: o bump para <c>0.0.5</c> promove <c>formulario</c> de stub a bloco real
    /// (título, termo de aceite) e acrescenta <c>rotulo</c>/<c>tipoRenderizacao</c>/
    /// <c>obrigatorio</c> a cada item de <c>fatosColetados</c> — a apresentação do campo no
    /// formulário de inscrição.
    /// Issue #1059 (UNI-REQ-0072): sob a MESMA <c>0.0.5</c> (regime pré-produção, reescrita no
    /// lugar), cada item de <c>fatosColetados</c> ganha <c>valoresSelecionaveis</c> — as opções
    /// que o candidato pode escolher, quando o tipo de renderização é de seleção — e
    /// <c>documentosExigidos.metadadosFatos[].valoresDominioDeclarados[]</c> ganha <c>ordem</c>,
    /// que até aqui era descartada no congelamento.
    /// Issue #1069: o bump para <c>0.0.6</c> fecha o trem de mudanças de forma que #1059, #1067 e
    /// #1068 regeneraram sob a mesma <c>0.0.5</c> sem tocar a versão, de propósito — e acrescenta a
    /// própria mudança desta issue: <c>etapas</c> passa a desempatar duas etapas sem <c>Ordem</c>
    /// pelo conteúdo de negócio (nome, caráter, peso, nota mínima), não mais só por <c>Id</c> — ver
    /// <see cref="SerializarEtapas"/>. Como não há produção nem certame publicado em ambiente
    /// nenhum, o avanço não migra dado nem cria decodificador de compatibilidade: fixture nova,
    /// versão anterior deixa de ser reconhecida.
    /// Issue #1070: o bump para <c>0.0.7</c> acrescenta <c>tipoProcesso</c> como 24º bloco de
    /// topo, congelando por valor a identidade do cadastro institucional vinculada ao processo.
    /// Issue #1071: o bump para <c>0.0.8</c> enriquece cada item de <c>etapas</c> com
    /// <c>tipoEtapa</c> — mesmo shape de <see cref="SerializarTipoProcesso"/>, sem criar novo
    /// bloco de topo (continuam 24). Substitui a comparação por
    /// <see cref="Domain.Entities.EtapaProcesso.Nome"/> em <c>EtapaObrigatoria</c> por código
    /// congelado do tipo. Sem produção em ambiente nenhum: fixture nova, <c>0.0.7</c> deixa de
    /// ser reconhecida.
    /// Issue #1112: o bump para <c>0.0.9</c> acrescenta <c>taxaInscricao</c> como 25º bloco de
    /// topo — a declaração de cobrança de taxa e os fundamentos de isenção referenciados. Sem
    /// produção em ambiente nenhum: fixture nova, <c>0.0.8</c> deixa de ser reconhecida.
    /// Issue #1114: o bump para <c>0.0.10</c> enriquece <c>identidadesUnidade.administradora</c>
    /// com <c>cidadeCodigoIbge</c>/<c>cidadeNome</c>/<c>cidadeUf</c> — a cidade da Unidade
    /// administradora, congelada por valor (ADR-0090), sem criar novo bloco de topo (continuam
    /// 25). Nula só para processos anteriores a esta issue (sem backfill, sem produção); todo
    /// processo NOVO tem cidade não-nula, por força do gate de
    /// <c>CriarProcessoSeletivoCommandHandler</c> (CA-02). Sem produção em ambiente nenhum:
    /// fixture nova, <c>0.0.9</c> deixa de ser reconhecida.
    /// </remarks>
    internal const string SchemaVersionAtual = "0.0.11";

    /// <summary>
    /// Perfil de bytes sob o qual a emissão de hoje congela — as regras de ordenação, escape e
    /// digest, identificadas à parte da <see cref="SchemaVersionAtual"/>. O rótulo gravado em
    /// <c>versao_configuracao.algoritmo_hash</c> vem daqui, do mesmo objeto que produz os
    /// bytes: literal e código andando juntos por construção, não por disciplina.
    /// </summary>
    private static readonly PerfilCanonicoV1 PerfilAtual = PerfilCanonicoV1.Instancia;

    private const int EscalaPadrao = 4;
    private const int EscalaPercentual = 2;

    public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        ProcessoSeletivo processo = entrada.Processo;
        DadosEdital dados = entrada.Dados;
        RetificacaoInfo? retificacao = entrada.Retificacao;

        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(entrada.HashDocumento);

        JsonObject payload = new()
        {
            ["tipoProcesso"] = SerializarTipoProcesso(processo),
            ["periodo"] = SerializarPeriodo(dados),
            ["etapas"] = SerializarEtapas(processo),
            ["vagas"] = SerializarVagas(processo),
            ["distribuicao"] = SerializarDistribuicao(processo),
            ["modalidades"] = SerializarModalidades(processo),
            ["ofertas"] = SerializarOfertas(processo),
            ["atendimento"] = SerializarAtendimento(processo),
            ["bonusRegional"] = SerializarBonusRegional(processo),
            ["criteriosDesempate"] = SerializarCriteriosDesempate(processo),
            ["classificacao"] = SerializarClassificacao(processo),
            ["hashesEdital"] = SerializarHashesEdital(dados, entrada.HashDocumento),
            ["documentosExigidos"] = SerializarDocumentosExigidos(processo, entrada.Conformidade, entrada.MetadadosFatosCongelados),
            ["arvoreSatisfacao"] = SerializarArvoreSatisfacao(processo),
            ["formulario"] = SerializarFormulario(processo),
            ["cascataRemanejamento"] = SerializarCascataRemanejamento(processo),
            ["divulgacao"] = SerializarDivulgacao(processo),
            ["cronogramaFases"] = SerializarCronogramaFases(processo),
            ["identidadesUnidade"] = SerializarIdentidadesUnidade(processo),
            ["fatosColetados"] = SerializarFatosColetados(processo.FatosColetados, entrada.ValoresSelecionaveisCongelados),
            ["regrasDerivacao"] = SerializarRegrasDerivacao(processo.RegrasDerivacao),
            ["grafoDependencia"] = SerializarGrafoDependencia(processo),
            ["versaoInterpretador"] = MotorDerivacao.VersaoSemantica,
            ["modalidadesOfertadas"] = SerializarModalidadesOfertadas(processo.DistribuicaoVagas),
            ["taxaInscricao"] = SerializarTaxaInscricao(processo),
            ["localidade"] = SerializarLocalidade(processo, entrada.FusoHorario),
        };

        // ADR-0101: a retificação ACRESCENTA um 27º bloco preservando os 26
        // anteriores (a issue #1112 elevou de 24 para 25, e a localidade de 25 para 26). A
        // abertura não escreve esta chave —
        // seu payload é byte-a-byte o mesmo do T4 (a reordenação de chaves em
        // ComputeSnapshotBytes independe da ordem de inserção aqui).
        if (retificacao is not null)
        {
            payload["retificacao"] = new JsonObject
            {
                ["editalRetificadoId"] = retificacao.EditalRetificadoId,
                ["motivo"] = HashCanonicalComputer.NormalizeNfc(retificacao.Motivo),
            };
        }

        byte[] bytes = PerfilAtual.Serializar(payload);
        return new SnapshotCanonico(bytes, SchemaVersionAtual, PerfilAtual.Algoritmo);
    }

    /// <summary>
    /// Tipo de processo escolhido, como cópia de valor autocontida. Não há leitura
    /// da configuração atual ao interpretar uma publicação: desativação ou alteração
    /// posterior do cadastro não pode alterar a prova emitida.
    /// </summary>
    private static JsonObject SerializarTipoProcesso(ProcessoSeletivo processo) => new()
    {
        ["origemId"] = processo.TipoProcessoOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(processo.TipoProcesso.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.TipoProcesso.Nome),
    };

    private static JsonObject SerializarPeriodo(DadosEdital dados) => new()
    {
        ["numero"] = dados.Numero is { } numero ? HashCanonicalComputer.NormalizeNfc(numero) : null,
        ["inicio"] = dados.PeriodoInscricaoInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["fim"] = dados.PeriodoInscricaoFim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Título e termo de aceite do formulário de inscrição (Story #559) — forma fechada mesmo
    /// quando os dois campos são nulos (ausência = sem título/termo configurado; não é o toggle
    /// por presença de <see cref="SerializarBonusRegional"/>, os dois campos são
    /// independentemente nuláveis).
    /// </summary>
    private static JsonObject SerializarFormulario(ProcessoSeletivo processo) => new()
    {
        ["titulo"] = processo.FormularioTitulo is { } titulo ? HashCanonicalComputer.NormalizeNfc(titulo) : null,
        ["termoAceiteTexto"] = processo.FormularioTermoAceiteTexto is { } termo ? HashCanonicalComputer.NormalizeNfc(termo) : null,
    };

    /// <summary>
    /// Etapas do processo (issue #1069): <c>Ordem ?? int.MaxValue</c> primeiro (semântica, define a
    /// posição de apresentação — nunca o divisor da média, ver <see cref="Domain.Entities.ProcessoSeletivo.DefinirEtapas"/>);
    /// para as etapas sem <c>Ordem</c>, o desempate é pelo <b>conteúdo de negócio</b> do item — não
    /// pelo <c>Id</c>. Sem chave de negócio única entre etapas (o domínio só recusa <c>Ordem</c>
    /// INFORMADA duplicada), duas etapas classificatórias com o mesmo nome, caráter, peso e nota
    /// mínima, ambas sem <c>Ordem</c>, são aceitas — se o desempate fosse por <c>Id</c>, a posição de
    /// uma etapa sem <c>Ordem</c> dependeria da ordem de inserção no banco (Guid v7 cronológico) em
    /// vez do que a etapa efetivamente diz.
    /// </summary>
    /// <remarks>
    /// <c>Id</c> continua como desempate TÉCNICO final, alcançável: as duas etapas do parágrafo
    /// acima são conteudisticamente idênticas, então a chave de conteúdo empata, e sobra o Id para
    /// tornar a posição determinística entre leituras — não para discriminar etapas genuinamente
    /// distintas, que a chave de conteúdo já resolve.
    /// </remarks>
    private static JsonArray SerializarEtapas(ProcessoSeletivo processo)
    {
        // Uma projeção só por etapa: o JsonObject SEM "id" é montado uma única vez, os bytes-chave
        // saem dele, e "id" é inserido no MESMO objeto depois de ordenar. Duas projeções (uma para
        // a chave, outra para a emissão) poderiam divergir se uma normalização futura tocasse só
        // uma delas — a etapa sairia ordenada por bytes que não correspondem ao item emitido.
        IOrderedEnumerable<(EtapaProcesso Etapa, JsonObject SemId)> ordenadas = processo.Etapas
            .Select(static e => (Etapa: e, SemId: SerializarEtapaSemIdentidade(e)))
            .OrderBy(static par => par.Etapa.Ordem ?? int.MaxValue)
            .ThenBy(
                static par => PerfilAtual.Serializar(par.SemId),
                ComparadorLexicograficoDeBytes.Instancia)
            .ThenBy(static par => par.Etapa.Id);

        return new JsonArray([.. ordenadas.Select(static par =>
        {
            // Id incluído: os blocos "criteriosDesempate" (DESEMPATE-MAIOR-NOTA-ETAPA) e
            // "classificacao" (ELIM-NOTA-MINIMA-ETAPA) congelam um
            // etapaRef apontando para este Id — sem ele aqui, o snapshot
            // teria uma referência não resolvível dentro do próprio JSON
            // congelado, obrigando a consultar a tabela viva (mutável)
            // para interpretar um documento que deveria ser autocontido.
            JsonObject item = par.SemId;
            item.Insert(0, "id", JsonValue.Create(par.Etapa.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarEtapaSemIdentidade(EtapaProcesso etapa) => new()
    {
        ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.Nome),
        ["carater"] = etapa.Carater.ToString(),
        ["tipoEtapa"] = SerializarTipoEtapa(etapa),
        ["peso"] = etapa.Peso is { } peso ? HashCanonicalComputer.SerializeDecimalCanonical(peso, EscalaPadrao) : null,
        ["notaMinima"] = etapa.NotaMinima is { } notaMinima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMinima, EscalaPadrao) : null,
        ["ordem"] = etapa.Ordem,
    };

    /// <summary>
    /// Tipo de etapa escolhido, como cópia de valor autocontida (issue #1071) — mesmo shape de
    /// <see cref="SerializarTipoProcesso"/>. Não há leitura da configuração atual ao interpretar
    /// uma publicação: desativação ou alteração posterior do cadastro não pode alterar a prova
    /// emitida, nem o resultado da avaliação de <c>EtapaObrigatoria</c> já congelada.
    /// </summary>
    private static JsonObject SerializarTipoEtapa(EtapaProcesso etapa) => new()
    {
        ["origemId"] = etapa.TipoEtapaOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Nome),
    };

    private static JsonArray SerializarDistribuicao(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["voBase"] = configuracao.VoBase,
                ["pr"] = HashCanonicalComputer.SerializeDecimalCanonical(configuracao.Pr, EscalaPadrao),
                ["regraDistribuicao"] = SerializarReferenciaRegra(configuracao.RegraDistribuicao),
                ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? SerializarReferenciaRegra(regraAjuste) : null,
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemografica(referencia)
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// O quadro de vagas (issue #848/ADR-0115) — output derivado, congelado
    /// separadamente dos insumos (<see cref="SerializarDistribuicao"/>) para
    /// que a prova de reprodutibilidade não seja tautológica: recomputa-se o
    /// quadro a partir dos insumos congelados e compara-se a este bloco.
    /// </summary>
    private static JsonArray SerializarVagas(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            JsonArray quadro = [];
            foreach (VagaOfertada vaga in configuracao.VagasOfertadas.OrderBy(static v => v.ModalidadeCodigo, StringComparer.Ordinal))
            {
                quadro.Add(new JsonObject
                {
                    ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(vaga.ModalidadeCodigo),
                    ["quantidade"] = vaga.Quantidade,
                });
            }

            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["quadro"] = quadro,
                ["vrNominal"] = configuracao.VrNominal,
                ["vrFinal"] = configuracao.VrFinal,
                ["estouro"] = configuracao.Estouro,
                ["capadoEmVo"] = configuracao.CapadoEmVo,
                ["totalPublicado"] = configuracao.TotalPublicado,
            });
        }

        return array;
    }

    private static JsonObject SerializarReferenciaDemografica(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscalaPercentual),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscalaPercentual),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscalaPercentual),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    private static JsonArray SerializarModalidades(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            IOrderedEnumerable<ModalidadeSelecionada> modalidadesOrdenadas = configuracao.Modalidades
                .OrderBy(static m => m.Codigo, StringComparer.Ordinal);
            foreach (ModalidadeSelecionada modalidade in modalidadesOrdenadas)
            {
                array.Add(new JsonObject
                {
                    ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                    ["modalidadeOrigemId"] = modalidade.ModalidadeOrigemId,
                    ["codigo"] = HashCanonicalComputer.NormalizeNfc(modalidade.Codigo),
                    ["descricao"] = modalidade.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                    ["naturezaLegal"] = modalidade.NaturezaLegal.ToString(),
                    ["composicaoVagas"] = modalidade.ComposicaoVagas.ToString(),
                    ["composicaoOrigemCodigo"] = modalidade.ComposicaoOrigemCodigo,
                    ["regraRemanejamento"] = modalidade.RegraRemanejamento.ToString(),
                    ["remanejamentoDestino"] = modalidade.RemanejamentoDestino,
                    ["remanejamentoPar"] = modalidade.RemanejamentoPar,
                    ["remanejamentoFallback"] = modalidade.RemanejamentoFallback,
                    ["criteriosCumulativos"] = OrdenarPorConteudo(modalidade.CriteriosCumulativos.Select(static c => JsonValue.Create(c)!)),
                    ["acaoQuandoIndeferido"] = modalidade.AcaoQuandoIndeferido,
                    ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(modalidade.BaseLegal),
                    ["quantidadeDeclarada"] = modalidade.QuantidadeDeclarada,
                });
            }
        }

        return array;
    }

    // OfertaCursoOrigemId é único por processo (ConfiguracaoDistribuicaoVagas
    // — "cada oferta de curso só pode ter uma distribuição de vagas no
    // processo", validado em ProcessoSeletivo.DefinirDistribuicaoVagas) —
    // chave de negócio estável, sem empate possível.
    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemId(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    private static JsonArray SerializarOfertas(ProcessoSeletivo processo)
    {
        IEnumerable<string> ofertaIds = processo.DistribuicaoVagas
            .Select(static d => d.OfertaCursoOrigemId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);

        return new JsonArray([.. ofertaIds.Select(static id => (JsonNode?)JsonValue.Create(id))]);
    }

    private static JsonObject SerializarAtendimento(ProcessoSeletivo processo)
    {
        // Um bloco REAL nunca emite `nao_construido` (ADR-0100 item 10, decisão D8).
        // O atendimento é dimensão obrigatória: a sua ausência é pendência de
        // conformidade, e o gate (ProcessoSeletivo.PendenciaDeConformidade) recusa a
        // transição antes de a canonicalização acontecer. Chegar aqui sem oferta é
        // invariante quebrada — falha alto, não congela um stub em silêncio.
        if (processo.OfertaAtendimento is not { } oferta)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem oferta de atendimento especializado — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["condicoes"] = new JsonArray([.. oferta.Condicoes
                .OrderBy(static c => c.CondicaoOrigemId)
                .Select(static c => (JsonNode)new JsonObject
                {
                    ["condicaoOrigemId"] = c.CondicaoOrigemId,
                    ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                    ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
                })]),
            ["recursos"] = new JsonArray([.. oferta.Recursos
                .OrderBy(static r => r.RecursoOrigemId)
                .Select(static r => (JsonNode)new JsonObject
                {
                    ["recursoOrigemId"] = r.RecursoOrigemId,
                    ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
                })]),
            ["tiposDeficiencia"] = new JsonArray([.. oferta.TiposDeficiencia
                .OrderBy(static t => t.TipoDeficienciaOrigemId)
                .Select(static t => (JsonNode)new JsonObject
                {
                    ["tipoDeficienciaOrigemId"] = t.TipoDeficienciaOrigemId,
                    ["tipoDeficienciaNome"] = HashCanonicalComputer.NormalizeNfc(t.TipoDeficienciaNome),
                })]),
        };
    }

    private static JsonObject SerializarBonusRegional(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } bonus)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = SerializarReferenciaRegra(bonus.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(bonus.Fator, EscalaPadrao),
            ["teto"] = bonus.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscalaPadrao) : null,
            ["municipioConvenio"] = bonus.MunicipioConvenio is { } municipio ? HashCanonicalComputer.NormalizeNfc(municipio) : null,
            ["baseLegal"] = bonus.BaseLegal is { } baseLegal ? HashCanonicalComputer.NormalizeNfc(baseLegal) : null,
        };
    }

    /// <summary>
    /// Serializa a cascata de remanejamento (Story #575) — mesma semântica de
    /// <see cref="SerializarBonusRegional"/>: ausência congela <c>{"presente": false}</c>, nunca
    /// o stub <c>nao_construido</c>. Ordenação determinística (ADR-0100): origens por
    /// <see cref="StringComparer.Ordinal"/>, destinos por <c>Ordem</c> dentro de cada origem.
    /// </summary>
    private static JsonObject SerializarCascataRemanejamento(ProcessoSeletivo processo)
    {
        if (processo.Cascata is not { } cascata)
        {
            return new JsonObject { ["presente"] = false };
        }

        JsonArray ordens = [];
        foreach (string origem in cascata.Destinos.Select(static d => d.ModalidadeOrigemCodigo).Distinct(StringComparer.Ordinal).OrderBy(static o => o, StringComparer.Ordinal))
        {
            JsonArray destinos = [];
            foreach (DestinoRemanejamento destino in cascata.Destinos
                .Where(d => string.Equals(d.ModalidadeOrigemCodigo, origem, StringComparison.Ordinal))
                .OrderBy(static d => d.Ordem))
            {
                destinos.Add(HashCanonicalComputer.NormalizeNfc(destino.ModalidadeDestinoCodigo));
            }

            ordens.Add(new JsonObject
            {
                ["origem"] = HashCanonicalComputer.NormalizeNfc(origem),
                ["destinos"] = destinos,
            });
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = SerializarReferenciaRegra(cascata.Regra),
            ["fallbackCodigo"] = HashCanonicalComputer.NormalizeNfc(cascata.FallbackCodigo),
            ["ordens"] = ordens,
        };
    }

    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050, issue #563) — forma fechada, no molde de
    /// <see cref="SerializarFormulario"/>: as três chaves existem sempre, com
    /// <see langword="null"/> explícito quando ausentes. A ausência de
    /// <see cref="ProcessoSeletivo.ConfiguracaoDivulgacao"/> materializa o EFETIVO — o default
    /// minimizado que o certame já divulga —, e não a ausência de escolha administrativa: é
    /// diferente do toggle <c>"presente"</c> de <see cref="SerializarBonusRegional"/>/
    /// <see cref="SerializarCascataRemanejamento"/>. Um leitor do documento precisa ler os
    /// campos publicados, não inferir o default a partir de uma ausência.
    /// </summary>
    /// <remarks>
    /// <c>regraNomeAbreviado</c> é DERIVADO da regra vigente no instante do congelamento — não
    /// existe campo "regra de abreviação" no agregado. Enquanto
    /// <see cref="RegrasDeNomeAbreviado"/> conhecer uma única regra, derivar a vigente aqui é
    /// indistinguível de reinjetar uma regra congelada; o fitness test da regra única trava essa
    /// premissa e fala no dia em que ela deixar de valer.
    /// </remarks>
    private static JsonObject SerializarDivulgacao(ProcessoSeletivo processo)
    {
        ConfiguracaoDivulgacao? divulgacao = processo.ConfiguracaoDivulgacao;
        IReadOnlyList<string> camposPublicos = divulgacao?.CamposPublicos ?? [ConfiguracaoDivulgacao.NumeroInscricao];
        bool abrevia = camposPublicos.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal);

        return new JsonObject
        {
            ["camposPublicos"] = OrdenarPorConteudo(camposPublicos.Select(static c => JsonValue.Create(c)!)),
            ["regraNomeAbreviado"] = abrevia ? RegrasDeNomeAbreviado.Vigente : null,
            ["justificativa"] = divulgacao?.Justificativa is { } justificativa ? HashCanonicalComputer.NormalizeNfc(justificativa) : null,
        };
    }

    /// <summary>
    /// Taxa de inscrição e isenção (issue #1112) — mesmo toggle <c>"presente"</c> de
    /// <see cref="SerializarBonusRegional"/>. Na prática a publicação já recusa processo sem
    /// declaração (CA-01, <c>ItensEstruturaisDeConformidade</c>), então um envelope legítimo
    /// nunca tem <c>"presente": false</c> aqui — o encoder trata o caso defensivamente pela
    /// mesma disciplina do resto do arquivo, não porque seja alcançável em produção.
    /// </summary>
    /// <summary>
    /// Localidade regente e fuso aplicado, no mesmo bloco (UNI-REQ-0111): os dois formam um
    /// contexto civil único e têm o mesmo ciclo de publicação, e um bloco fechado impede a
    /// combinação parcial — versão com município mas sem fuso não converte âncora em dia civil.
    /// </summary>
    /// <remarks>
    /// O <c>codigoIbge</c> é o único valor normativo: dele se derivam os feriados municipais e,
    /// pelo prefixo, os estaduais. <c>nome</c> e <c>uf</c> viajam como cache de exibição da versão,
    /// e não participam de cálculo nenhum.
    /// </remarks>
    private static JsonObject SerializarLocalidade(ProcessoSeletivo processo, string fusoHorario) =>
        new()
        {
            ["codigoIbge"] = processo.Localidade.CodigoIbge,
            ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.Localidade.Nome),
            ["uf"] = processo.Localidade.Uf,
            ["fusoHorario"] = fusoHorario,
        };

    private static JsonObject SerializarTaxaInscricao(ProcessoSeletivo processo)
    {
        if (processo.ConfiguracaoTaxaInscricao is not { } taxa)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["cobra"] = taxa.Cobra,
            ["valor"] = taxa.Valor is { } valor ? HashCanonicalComputer.SerializeDecimalCanonical(valor, ConfiguracaoTaxaInscricao.ValorEscala) : null,
            ["fundamentos"] = new JsonArray([.. taxa.Fundamentos.Select(static f => (JsonNode)f.ToCodigo())]),
            ["confirmacaoFundamentos"] = taxa.ConfirmacaoFundamentos,
        };
    }

    /// <summary>
    /// Serializa a identidade da Unidade administradora (issue #849, CA-04 da Feature #40) —
    /// sempre presente, NOT NULL desde a criação do processo (diferente de
    /// <see cref="SerializarCascataRemanejamento"/>/<see cref="SerializarBonusRegional"/>, que
    /// alternam presença/ausência): não há toggle <c>"presente"</c> aqui. A cidade (issue #1114,
    /// ADR-0090) é opcional all-or-nothing dentro deste bloco sempre-presente — nula só para
    /// processos anteriores a #1114 (sem backfill, sem produção).
    /// </summary>
    private static JsonObject SerializarIdentidadesUnidade(ProcessoSeletivo processo) => new()
    {
        ["administradora"] = new JsonObject
        {
            ["origemId"] = processo.UnidadeAdministradoraOrigemId,
            ["sigla"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Sigla),
            ["slug"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Slug),
            ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Nome),
            ["tipo"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Tipo),
            ["cidadeCodigoIbge"] = processo.UnidadeAdministradora.CidadeCodigoIbge is { } codigoIbge
                ? HashCanonicalComputer.NormalizeNfc(codigoIbge) : null,
            ["cidadeNome"] = processo.UnidadeAdministradora.CidadeNome is { } cidadeNome
                ? HashCanonicalComputer.NormalizeNfc(cidadeNome) : null,
            ["cidadeUf"] = processo.UnidadeAdministradora.CidadeUf is { } cidadeUf
                ? HashCanonicalComputer.NormalizeNfc(cidadeUf) : null,
        },
    };

    private static JsonArray SerializarCriteriosDesempate(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate.OrderBy(static c => c.Ordem))
        {
            array.Add(new JsonObject
            {
                ["ordem"] = criterio.Ordem,
                ["regra"] = SerializarReferenciaRegra(criterio.Regra),
                ["args"] = SerializarArgsCriterioDesempate(criterio.Args),
            });
        }

        return array;
    }

    private static JsonObject SerializarArgsCriterioDesempate(ArgsCriterioDesempate args) => args switch
    {
        ArgsDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaRef"] = maiorNotaEtapa.EtapaRef },
        ArgsDesempateMaiorIdade => [],
        ArgsDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ArgsDesempatePredicadoFato predicadoFato => new JsonObject
        {
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Condicao.Fato),
            ["operador"] = predicadoFato.Condicao.Operador.ToCodigo(),
            ["valor"] = SerializarValorDeAtomo(predicadoFato.Condicao.Operador, predicadoFato.Condicao.Valor),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsCriterioDesempate)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarClassificacao(ProcessoSeletivo processo)
    {
        // Mesma regra de SerializarAtendimento acima (nenhum bloco real vira stub). A
        // classificação é o bloco que determina o resultado do certame; congelá-la como
        // `nao_construido` num documento juridicamente vinculante é o pior modo de falha
        // do envelope.
        if (processo.Classificacao is not { } classificacao)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem configuração de classificação — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["regraCalculo"] = SerializarReferenciaRegra(classificacao.RegraCalculo),
            ["regraArredondamento"] = classificacao.RegraArredondamento is { } arredondamento
                ? SerializarReferenciaRegra(arredondamento)
                : null,
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["regraOrdemAlocacao"] = SerializarReferenciaRegra(classificacao.RegraOrdemAlocacao),
            ["nOpcoesAlocacao"] = classificacao.NOpcoesAlocacao,
            ["baseadoEmEnem"] = classificacao.BaseadoEmEnem,
            // RegrasEliminacao não tem chave de negócio única (cardinalidade
            // múltipla: duas ELIM-NOTA-MINIMA-ETAPA distintas são válidas, ex. PS
            // Convênios). Ordenar por `Id` era determinístico entre leituras da
            // mesma linha, mas NÃO entre configurações equivalentes — dois processos
            // com as mesmas regras inseridas em ordem inversa recebem Guids v7
            // distintos e produziriam envelopes distintos para a mesma configuração.
            // A ordenação é pela CHAVE DE CONTEÚDO: os bytes canônicos do próprio
            // item. O envelope passa a depender só do que ele diz.
            ["regrasEliminacao"] = OrdenarPorConteudo(classificacao.RegrasEliminacao
                .Select(static r => new JsonObject
                {
                    ["regra"] = SerializarReferenciaRegra(r.Regra),
                    ["args"] = SerializarArgsRegraEliminacao(r.Args),
                })),
        };
    }

    private static JsonObject SerializarArgsRegraEliminacao(ArgsRegraEliminacao args) => args switch
    {
        ArgsElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscalaPadrao),
        },
        ArgsElimCorteRedacao corteRedacao => new JsonObject
        {
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteRedacao.Minimo, EscalaPadrao),
        },
        ArgsElimZeroEmArea => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsRegraEliminacao)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarHashesEdital(DadosEdital dados, string hashEdital) => new()
    {
        ["documentoEditalId"] = dados.DocumentoEditalId,
        ["hashSha256"] = hashEdital,
    };

    /// <summary>
    /// Story #554 (PR #903, bump 1.2): <c>exigencias</c> deixa de ser stub — cada
    /// <see cref="DocumentoExigido"/> viva do processo vira um item rico (CA-09: identidade
    /// estável por <c>exigenciaId</c>). Duas chaves-irmãs novas (B-03):
    /// <c>referenciaTemporalFatos</c> — a POLÍTICA crua (<see cref="ValueObjects.ReferenciaTemporalFatos"/>,
    /// o INSUMO) — e <c>dataReferenciaFatos</c> — a <see cref="DateOnly"/> já resolvida a
    /// partir dela (o OUTPUT). Mesmo padrão de <c>distribuicao</c>/<c>vagas</c>: congelar o
    /// insumo ao lado do output derivado é o que torna a prova de reprodutibilidade
    /// NÃO-tautológica — reidratar recompõe a política, <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>
    /// recalcula o output a partir dela, e o round-trip compara os bytes.
    /// </summary>
    private static JsonObject SerializarDocumentosExigidos(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) => new()
        {
            ["exigencias"] = SerializarExigencias(processo.DocumentosExigidos),
            ["obrigatoriedades"] = SerializarObrigatoriedades(conformidade),
            ["referenciaTemporalFatos"] = processo.ReferenciaTemporalFatos is { } referencia ? new JsonObject
            {
                ["tipo"] = referencia.Tipo.ToCodigo(),
                ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["faseId"] = referencia.FaseId,
            }
            : null,
            ["dataReferenciaFatos"] = processo.ResolverDataReferenciaFatos() is { } data
            ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
            ["metadadosFatos"] = SerializarMetadadosFatos(metadadosFatosCongelados),
        };

    /// <summary>
    /// Story #919 (RN08): congela o metadado — domínio, origem, cardinalidade, ponto de
    /// resolução, binding e o(s) conjunto(s) de valores — de cada fato do candidato citado
    /// em alguma <see cref="CondicaoGatilho"/> de alguma <see cref="DocumentoExigido"/> do
    /// processo. Bloco IRMÃO de <c>exigencias</c>/<c>obrigatoriedades</c>/
    /// <c>referenciaTemporalFatos</c> dentro de <c>documentosExigidos</c> — array SEMPRE
    /// presente (nunca <c>nao_construido</c>, D8), vazio quando nenhuma condição existe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O canonicalizador não deriva este dicionário de <c>processo.DocumentosExigidos</c> —
    /// ele apenas SERIALIZA o que o handler já resolveu via <c>IFatoCandidatoReader</c>
    /// (ADR-0042, mesmo tratamento que <see cref="SerializarObrigatoriedades"/> recebe para
    /// <c>Conformidade</c>). A garantia de que todo fato referenciado tem metadado
    /// resolvido — "sem faltante" — é do HANDLER, antes de canonicalizar: a projeção pura
    /// não tem I/O para revalidar isso contra o catálogo vivo.
    /// </para>
    /// <para>
    /// A chave do fato é <c>fatoCodigo</c> — não <c>codigo</c> — e a do valor declarado é
    /// <c>valorCodigo</c>: <c>EnvelopeCanonicoGoldenTests.Envelope_ReferenciasDeRegraSaoTripla</c>
    /// trata qualquer objeto com a chave BARE <c>codigo</c> (sem <c>naturezaLegal</c>/<c>ordem</c>)
    /// como candidato a referência de regra, exigindo a tripla <c>{codigo, versao, hash}</c> —
    /// um metadado de fato não é uma referência de regra, e usar a chave qualificada evita a
    /// falsa colisão (mesma convenção de <c>tipoDocumentoCodigo</c>/<c>modalidadeCodigo</c>).
    /// </para>
    /// </remarks>
    private static JsonArray SerializarMetadadosFatos(IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados)
    {
        if (metadadosFatosCongelados is null || metadadosFatosCongelados.Count == 0)
        {
            return [];
        }

        JsonArray array = [];
        foreach (MetadadoFatoCongelado metadado in metadadosFatosCongelados.Values.OrderBy(static m => m.Codigo, StringComparer.Ordinal))
        {
            array.Add(new JsonObject
            {
                ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(metadado.Codigo),
                ["dominio"] = HashCanonicalComputer.NormalizeNfc(metadado.Dominio),
                ["origem"] = HashCanonicalComputer.NormalizeNfc(metadado.Origem),
                ["cardinalidade"] = HashCanonicalComputer.NormalizeNfc(metadado.Cardinalidade),
                ["pontoResolucao"] = HashCanonicalComputer.NormalizeNfc(metadado.PontoResolucao),
                ["binding"] = HashCanonicalComputer.NormalizeNfc(metadado.Binding),
                ["valoresDominio"] = metadado.ValoresDominio is { } valores
                    ? OrdenarPorConteudo(valores.Select(static v => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(v))!))
                    : null,
                ["valoresDominioDeclarados"] = metadado.ValoresDominioDeclarados is { } declarados
                    ? new JsonArray([.. declarados
                        .OrderBy(static d => d.Ordem)
                        .ThenBy(static d => d.Codigo, StringComparer.Ordinal)
                        .Select(static d => (JsonNode)new JsonObject
                        {
                            ["valorCodigo"] = HashCanonicalComputer.NormalizeNfc(d.Codigo),
                            ["descricao"] = d.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                            ["ordem"] = d.Ordem,
                        })])
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// Story #923 — a TOPOLOGIA da árvore de satisfação (<see cref="NoExigencia"/>, Stories
    /// #920/#921/#922): raízes ordenadas pela primeira camada da composição hierárquica (ADR-0109,
    /// emenda #1069), <see cref="NoExigencia.Ordem"/> — posição declarada no fluxo documental
    /// apresentado ao candidato. <c>ux_nos_exigencia_raiz_ordem</c>/<c>ux_nos_exigencia_irmaos_ordem</c>
    /// (unique index, <c>NoExigenciaConfiguration</c>) garantem <c>Ordem</c> única entre raízes e
    /// entre irmãos — suficiente para tornar a posição determinística.
    /// </summary>
    private static JsonArray SerializarArvoreSatisfacao(ProcessoSeletivo processo) =>
        new([.. processo.RaizesDeExigencia.OrderBy(static r => r.Ordem).Select(static r => (JsonNode)SerializarNo(r))]);

    /// <summary>
    /// Um nó, recursivamente — mesmo formato (campos e nomes) de <c>NoExigenciaDto</c>
    /// (<c>ObterProcessoSeletivoQueryHandler.ProjectNoExigencia</c>), inclusive o token de
    /// <see cref="TipoNo"/> ("FOLHA"/"E"/"OU"). <see cref="NoExigencia.DocumentoExigidoId"/>
    /// (só em folha) referencia o MESMO <c>exigenciaId</c> já congelado em
    /// <c>documentosExigidos.exigencias[]</c> — não duplica o conteúdo da exigência aqui.
    /// Todo campo é emitido sempre, com <see langword="null"/> explícito onde não se aplica
    /// ao tipo do nó — nunca omitido (o envelope preserva a diferença entre "null" e "chave
    /// ausente", ADR-0109 D4).
    /// </summary>
    private static JsonObject SerializarNo(NoExigencia no) => new()
    {
        ["id"] = no.Id,
        ["ordem"] = no.Ordem,
        ["tipo"] = no.Tipo.ToCodigo(),
        ["exigenciaId"] = no.Tipo == TipoNo.Folha ? no.DocumentoExigidoId : null,
        ["quantidadeMinima"] = no.QuantidadeMinima,
        ["consequencia"] = no.Consequencia is { } consequencia ? HashCanonicalComputer.NormalizeNfc(consequencia) : null,
        ["basesLegais"] = SerializarBasesLegaisDeNo(no.BasesLegaisResolvidas()),
        ["chaveDistincao"] = no.ChaveDistincao?.ToCodigo(),
        ["dataReferencia"] = no.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["ocorrenciasEsperadas"] = no.OcorrenciasEsperadas is { } ocorrencias
            ? OrdenarPorConteudo(ocorrencias.Select(static o => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(o))!))
            : null,
        ["repetePorEntidade"] = no.RepetePorEntidade?.ToCodigo(),
        ["filhos"] = new JsonArray([.. no.Filhos.OrderBy(static f => f.Ordem).Select(static f => (JsonNode)SerializarNo(f))]),
    };

    /// <summary>
    /// Base legal PRÓPRIA de um grupo <see cref="TipoNo.GrupoOu"/> — mesmo shape de
    /// <see cref="SerializarBasesLegais"/> (a de <see cref="DocumentoExigido"/>), tipo
    /// diferente (<see cref="NoExigenciaBaseLegal"/>): mesma razão de duas classes
    /// concretas separadas em vez de herança (ver <see cref="NoExigencia"/>).
    /// </summary>
    private static JsonArray SerializarBasesLegaisDeNo(IEnumerable<NoExigenciaBaseLegal> basesLegais) =>
        OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            ["status"] = b.Status.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    /// <summary>
    /// <see cref="DocumentoExigido"/> não tem chave de negócio única (nada impede duas
    /// exigências para a mesma fase e o mesmo tipo de documento, com gatilhos distintos).
    /// Ordena pela chave de negócio PARCIAL (fase + tipo de documento — o caso comum, sem
    /// empate) e, no raro caso de empate, pela chave de conteúdo do restante do item —
    /// mesma regra de desempate por conteúdo (ADR-0109 D9) — (nunca por
    /// <c>exigenciaId</c> — um Guid v7 novo a cada <c>DefinirDocumentosExigidos</c>
    /// tornaria a ordem não-determinística entre configurações equivalentes, o mesmo
    /// problema que a chave de conteúdo evita em <see cref="OrdenarPorConteudo"/>).
    /// </summary>
    private static JsonArray SerializarExigencias(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(
                static e => PerfilAtual.Serializar(SerializarExigenciaSemIdentidade(e)),
                ComparadorLexicograficoDeBytes.Instancia)
            // Achado de revisão (Story #554, PR #903): duas exigências byte-idênticas em
            // todo o resto (mesma fase, mesmo tipo, mesmo conteúdo) empatam na chave de
            // conteúdo acima — e, ao contrário de regrasEliminacao (sem identidade), o Id
            // aqui É congelado no envelope (exigenciaId, CA-09), então usá-lo como
            // desempate FINAL não fere D9: não afeta a ordem de exigências
            // GENUINAMENTE distintas (a chave de conteúdo já as discrimina), só torna
            // determinística a ordem do caso raro de duplicata verdadeira — sem isso, a
            // ordem de materialização do EF (sem ORDER BY no Include) poderia produzir
            // bytes diferentes para a MESMA configuração persistida entre leituras.
            .ThenBy(static e => e.Id);

        return new JsonArray([.. ordenadas.Select(static e =>
        {
            JsonObject item = SerializarExigenciaSemIdentidade(e);
            item.Insert(0, "exigenciaId", JsonValue.Create(e.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarExigenciaSemIdentidade(DocumentoExigido exigencia) => new()
    {
        ["tipoDocumentoOrigemId"] = exigencia.TipoDocumentoOrigemId,
        ["tipoDocumentoCodigo"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCodigo),
        ["tipoDocumentoNome"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoNome),
        ["tipoDocumentoCategoria"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCategoria),
        ["exigidoNaFaseId"] = exigencia.ExigidoNaFaseId,
        ["aplicabilidade"] = exigencia.Aplicabilidade.ToString(),
        ["obrigatorio"] = exigencia.Obrigatorio,
        ["consequenciaIndeferimento"] = exigencia.ConsequenciaIndeferimento is { } consequencia
            ? HashCanonicalComputer.NormalizeNfc(consequencia)
            : null,
        ["grupoSatisfacaoId"] = exigencia.GrupoSatisfacaoId,
        ["condicaoGatilho"] = SerializarCondicaoGatilho(exigencia.Condicoes),
        ["basesLegais"] = SerializarBasesLegais(exigencia.BasesLegaisResolvidas()),
        ["idadeMaximaEmissao"] = exigencia.IdadeMaximaEmissao is { } idade ? SerializarIdadeMaximaEmissao(idade) : null,
        ["formatosPermitidos"] = SerializarFormatosPermitidos(exigencia.FormatosPermitidos),
        ["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes,
    };

    /// <summary>
    /// <see cref="FormatosPermitidos"/> (Story #918) substitui o campo singular
    /// <c>formatoPermitido</c> — objeto sempre presente (o VO é obrigatório), com
    /// <c>lista</c> nula ⟺ <c>qualquer</c> verdadeiro.
    /// </summary>
    private static JsonObject SerializarFormatosPermitidos(FormatosPermitidos formatosPermitidos) => new()
    {
        ["qualquer"] = formatosPermitidos.Qualquer,
        ["lista"] = formatosPermitidos.Lista is { } lista
            ? OrdenarPorConteudo(lista.Select(static e => new JsonObject
            {
                ["formato"] = e.Formato.ToCodigo(),
                ["tamanhoMaximoBytesMax"] = e.TamanhoMaximoBytesMax,
            }))
            : null,
    };

    /// <summary>
    /// O predicado DNF do gatilho de uma exigência (PR #896, ADR-0111) — a mesma forma
    /// OU-de-cláusulas/E-de-condições de <see cref="SerializarDnf"/>, para a coleção TIPADA
    /// de <see cref="CondicaoGatilho"/>. Delega direto, projetando os quatro campos para a
    /// mesma tupla que os demais chamadores (pré-condição de fato, regra de derivação) já
    /// montam — uma regra de ordenação, um lugar só (issue #1068): antes desta consolidação
    /// o método tinha a mesma lógica copiada, e corrigir só um dos dois teria deixado a
    /// outra metade divergente.
    /// </summary>
    private static JsonArray? SerializarCondicaoGatilho(IReadOnlyCollection<CondicaoGatilho> condicoes) =>
        SerializarDnf(condicoes.Select(static c => (c.Clausula, c.Fato, c.Operador, c.Valor)));

    /// <summary>
    /// Só bases legais <c>RESOLVIDO</c> (PR #898, issue #549) — uma <c>PENDENTE</c> não é
    /// evidência jurídica ainda, e o gate de publicação (<c>ValidadorBaseLegalExigencias</c>)
    /// já provou que existe ao menos uma resolvida por exigência que determina resultado
    /// antes deste ponto.
    /// </summary>
    private static JsonArray SerializarBasesLegais(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            // Wire format canônico (ToCodigo/FromCodigo, não ToString — convenção do
            // repo para enums de comando/envelope estabelecida a partir da PR #898/PR #900,
            // que criaram TipoAbrangenciaCodigo/StatusBaseLegalCodigo/etc. como fonte
            // única do token; a exigencias[] é a primeira consumidora deles no envelope).
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            // Sempre RESOLVIDO — só bases resolvidas chegam aqui (BasesLegaisResolvidas()
            // já filtrou). Mantido explícito por paridade estrutural, mesmo raciocínio de
            // "aprovada" em obrigatoriedades[]: o campo não pode vir diferente num
            // snapshot real, mas omiti-lo esconderia essa garantia do próprio documento.
            ["status"] = b.Status.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    private static JsonObject SerializarIdadeMaximaEmissao(IdadeMaximaEmissao idade) => new()
    {
        ["valor"] = idade.Valor,
        ["unidade"] = idade.Unidade.ToCodigo(),
        ["referenciaTipo"] = idade.ReferenciaTipo.ToCodigo(),
        ["data"] = idade.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["referenciaFaseId"] = idade.ReferenciaFaseId,
    };

    /// <summary>
    /// Ordenação determinística pela CHAVE DE CONTEÚDO (ADR-0109 D9) — os bytes canônicos do
    /// item inteiro, <b>não</b> o <c>RegraId</c> (Guid v7, cronológico). Ordenar pelo Guid era
    /// exatamente o vazamento que a chave de conteúdo existe para impedir: duas avaliações que
    /// aprovam o mesmo conjunto de regras, feitas em execuções distintas (Guids v7 novos a cada
    /// avaliação), produziriam envelopes diferentes para a mesma configuração. Só regras
    /// aprovadas chegam aqui: o gate já recusou a transição antes de canonicalizar se qualquer
    /// uma reprovasse (§3.4) — o campo <c>aprovada</c> é mantido por paridade estrutural, não
    /// porque possa vir falso num snapshot real.
    /// </summary>
    private static JsonArray SerializarObrigatoriedades(ResultadoConformidade? conformidade)
    {
        if (conformidade is null)
        {
            return [];
        }

        return OrdenarPorConteudo(conformidade.Regras.Select(static regra => new JsonObject
        {
            ["regraId"] = regra.RegraId,
            ["regraCodigo"] = HashCanonicalComputer.NormalizeNfc(regra.RegraCodigo),
            ["categoria"] = regra.Categoria.ToString(),
            ["tipoProcessoCodigoAvaliado"] = HashCanonicalComputer.NormalizeNfc(regra.TipoProcessoCodigoAvaliado),
            ["predicado"] = SerializarPredicadoObrigatoriedade(regra.Predicado),
            ["aprovada"] = regra.Aprovada,
            ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(regra.BaseLegal),
            ["atoNormativoUrl"] = regra.AtoNormativoUrl is { } url ? HashCanonicalComputer.NormalizeNfc(url) : null,
            ["portariaInterna"] = regra.PortariaInterna is { } portaria ? HashCanonicalComputer.NormalizeNfc(portaria) : null,
            ["descricaoHumana"] = HashCanonicalComputer.NormalizeNfc(regra.DescricaoHumana),
            ["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["hash"] = regra.Hash,
        }));
    }

    /// <summary>
    /// A variante em <c>args</c> é decidida pelo TIPO da regra ($tipo), mesma técnica de
    /// <see cref="SerializarArgsCriterioDesempate"/> e <see cref="SerializarArgsRegraEliminacao"/>
    /// — nunca um discriminador JSON solto.
    /// </summary>
    private static JsonObject SerializarPredicadoObrigatoriedade(PredicadoObrigatoriedade predicado)
    {
        (string tipo, JsonObject args) = predicado switch
        {
            EtapaObrigatoria p => ("etapaObrigatoria", new JsonObject
            {
                ["tipoEtapaCodigo"] = HashCanonicalComputer.NormalizeNfc(p.TipoEtapaCodigo),
            }),
            ModalidadesMinimas p => ("modalidadesMinimas", new JsonObject
            {
                ["codigos"] = OrdenarPorConteudo(p.Codigos.Select(static c => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(c))!)),
            }),
            DesempateDeveIncluir p => ("desempateDeveIncluir", new JsonObject
            {
                ["criterio"] = HashCanonicalComputer.NormalizeNfc(p.Criterio),
            }),
            DocumentoObrigatorioParaModalidade p => ("documentoObrigatorioParaModalidade", new JsonObject
            {
                ["modalidade"] = HashCanonicalComputer.NormalizeNfc(p.Modalidade),
                ["tipoDocumento"] = HashCanonicalComputer.NormalizeNfc(p.TipoDocumento),
            }),
            AtendimentoDisponivel p => ("atendimentoDisponivel", new JsonObject
            {
                ["necessidades"] = OrdenarPorConteudo(p.Necessidades.Select(static n => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(n))!)),
            }),
            ConcorrenciaDuplaObrigatoria => ("concorrenciaDuplaObrigatoria", []),
            Customizado p => ("customizado", new JsonObject
            {
                ["parametros"] = JsonNode.Parse(p.Parametros.GetRawText()),
            }),
            _ => throw new InvalidOperationException(
                $"Variante de {nameof(PredicadoObrigatoriedade)} não reconhecida: {predicado.GetType()}."),
        };

        return new JsonObject
        {
            ["tipo"] = tipo,
            ["args"] = args,
        };
    }

    /// <summary>
    /// O cronograma de fases (Story #851): <c>origemCandidatos</c> — atributo de raiz do
    /// agregado, chave-irmã dentro deste bloco (o envelope não tem bloco genérico de
    /// metadados do processo, ADR-0100 item 10) — e o array <c>fases</c>, ordenado
    /// deterministicamente por <c>OrderBy(Ordem).ThenBy(Id)</c> (a ordem entra no hash e o
    /// repositório não aplica <c>ORDER BY</c> aos <c>Include</c>).
    /// </summary>
    private static JsonObject SerializarCronogramaFases(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronograma(processo),
    };

    private static JsonArray SerializarFasesCronograma(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        IOrderedEnumerable<FaseCronograma> ordenadas = processo.CronogramaFases
            .OrderBy(static f => f.Ordem)
            .ThenBy(static f => f.Id);
        foreach (FaseCronograma fase in ordenadas)
        {
            array.Add(new JsonObject
            {
                // Story #554 (PR #903, bump 1.2), achado de revisão: id congelado para que
                // exigidoNaFaseId/referenciaTemporalFatos.faseId resolvam mesmo quando a
                // sombra de verificação (RestauradorDeConfiguracao) reidrata sem nenhuma
                // fase viva rastreada para reconciliar por Ordem. Só a 1.2 escreve esta
                // chave — o encoder 1.1 congelado (EnvelopeCodecV11) não a tinha.
                ["id"] = fase.Id,
                ["ordem"] = fase.Ordem,
                ["faseCanonicaOrigemId"] = fase.FaseCanonicaOrigemId,
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(fase.Codigo),
                ["donoInstitucional"] = HashCanonicalComputer.NormalizeNfc(fase.DonoInstitucional),
                ["origemData"] = fase.OrigemData.ToString(),
                ["agrupaEtapas"] = fase.AgrupaEtapas,
                ["permiteComplementacao"] = fase.PermiteComplementacao,
                ["produzResultado"] = fase.ProduzResultado,
                ["resultadoDefinitivo"] = fase.ResultadoDefinitivo,
                ["coletaInscricao"] = fase.ColetaInscricao,
                ["inicio"] = fase.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null,
                ["fim"] = fase.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null,
                ["atoProduzidoCodigo"] = fase.AtoProduzidoCodigo is { } atoCodigo ? HashCanonicalComputer.NormalizeNfc(atoCodigo) : null,
                ["atoProduzidoEfeitoIrreversivel"] = fase.AtoProduzidoEfeitoIrreversivel,
                ["bancasRequeridas"] = SerializarBancasRequeridas(fase),
                ["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFase(regraRecurso) : null,
            });
        }

        return array;
    }

    private static JsonArray SerializarBancasRequeridas(FaseCronograma fase)
    {
        IOrderedEnumerable<BancaRequerida> ordenadas = fase.BancasRequeridas
            .OrderBy(static b => b.TipoBancaOrigemId)
            .ThenBy(static b => b.Codigo, StringComparer.Ordinal);

        return new JsonArray([.. ordenadas.Select(static b => (JsonNode)new JsonObject
        {
            ["tipoBancaOrigemId"] = b.TipoBancaOrigemId,
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(b.Codigo),
        })]);
    }

    private static JsonObject SerializarRegraRecursoFase(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = SerializarReferenciaRegra(regraRecurso.Regra),
        ["args"] = SerializarArgsRegraPrazoRecurso(regraRecurso.Args),
    };

    private static JsonObject SerializarArgsRegraPrazoRecurso(ArgsRegraPrazoRecurso args) => new()
    {
        ["prazoValor"] = HashCanonicalComputer.SerializeDecimalCanonical(args.PrazoValor, EscalaPadrao),
        ["prazoUnidade"] = args.PrazoUnidade.ToString(),
        ["atoAncoraCodigo"] = HashCanonicalComputer.NormalizeNfc(args.AtoAncoraCodigo),
        ["suspensividadePrimeiraInstanciaValor"] = args.SuspensividadePrimeiraInstanciaValor is { } v1
            ? HashCanonicalComputer.SerializeDecimalCanonical(v1, EscalaPadrao)
            : null,
        ["suspensividadePrimeiraInstanciaUnidade"] = args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
        ["suspensividadeSegundaInstanciaValor"] = args.SuspensividadeSegundaInstanciaValor is { } v2
            ? HashCanonicalComputer.SerializeDecimalCanonical(v2, EscalaPadrao)
            : null,
        ["suspensividadeSegundaInstanciaUnidade"] = args.SuspensividadeSegundaInstanciaUnidade?.ToString(),
    };

    /// <summary>
    /// Ordena um array pela <b>chave de conteúdo</b> de cada item — os seus
    /// próprios bytes canônicos (ADR-0109 D9). Usado onde a coleção não tem
    /// chave de negócio natural: sem isso, a identidade técnica da linha (um
    /// Guid) vazaria para dentro do hash e duas configurações equivalentes
    /// produziriam envelopes distintos.
    /// </summary>
    private static JsonArray OrdenarPorConteudo(IEnumerable<JsonObject> itens)
    {
        IOrderedEnumerable<JsonObject> ordenados = itens.OrderBy(
            static item => PerfilAtual.Serializar(item),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    /// <summary>
    /// A mesma chave de conteúdo (ADR-0109 D9) para os sete conjuntos que são arrays de
    /// TEXTO puro — <c>criteriosCumulativos</c>, <c>ocorrenciasEsperadas</c>,
    /// <c>valoresDominio</c>, os dois <c>args</c> de predicado de obrigatoriedade
    /// (<c>codigos</c>, <c>necessidades</c>), as alternativas de um átomo sob operador de
    /// pertencimento (<see cref="SerializarValorDeAtomo"/>, issue #1068) e
    /// <c>divulgacao.camposPublicos</c> (issue #563). <see cref="PerfilCanonicoV1.SerializarChave"/>
    /// aceita <see cref="JsonNode"/>, não só <see cref="JsonObject"/> — é o que permite ordenar o
    /// escalar pelos MESMOS bytes canônicos (NFC incluído) que a emissão final grava, sem
    /// duplicar a normalização aqui. <b>Internal</b>, não <c>private</c>: o decoder
    /// (<see cref="EnvelopeCodec.LerDivulgacao"/>) reusa esta MESMA política para conferir que
    /// <c>camposPublicos</c> chegou já ordenado — nunca reimplementa um comparador próprio.
    /// </summary>
    /// <remarks>
    /// Um item nulo é invariante quebrada, não entrada tolerável: nenhum dos sete vocabulários
    /// admite <c>null</c> como elemento — <see cref="LeitorEnvelope.Textos"/> já o recusa na
    /// leitura para os seis primeiros, e <see cref="Services.PredicadoDnfValidador"/> já
    /// recusa alternativa não-string (logo nunca nula) antes da publicação para o sétimo. Um
    /// envelope que o emitisse produziria um documento que o próprio decoder da mesma versão
    /// rejeitaria. Falha alto aqui, com mensagem própria, em vez de deixar o
    /// <see langword="null"/> estourar mais adiante dentro da serialização com uma
    /// <see cref="NullReferenceException"/> sem contexto.
    /// </remarks>
    internal static JsonArray OrdenarPorConteudo(IEnumerable<JsonValue> itens)
    {
        List<JsonValue> lista = [];
        foreach (JsonValue? item in itens)
        {
            if (item is null)
            {
                throw new InvalidOperationException(
                    "Um array de texto do envelope recebeu um elemento nulo antes da ordenação — nenhum dos " +
                    "conjuntos de texto simples (criteriosCumulativos, ocorrenciasEsperadas, valoresDominio, " +
                    "codigos, necessidades, alternativas de átomo EM/NAO_EM) admite null como item.");
            }

            lista.Add(item);
        }

        IOrderedEnumerable<JsonValue> ordenados = lista.OrderBy(
            static item => PerfilCanonicoV1.SerializarChave(item),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    /// <summary>
    /// Ordena um array de <b>cláusulas já canonicalizadas</b> pela chave de conteúdo de cada
    /// cláusula inteira — os bytes canônicos do array de átomos, não de um átomo isolado
    /// (issue #1068, D2). É o nível externo do predicado DNF: o <c>GroupBy</c> por
    /// <c>Clausula</c> em <see cref="SerializarDnf"/> decide QUAIS átomos formam a mesma
    /// conjunção (permanece intocado — é semântico, não ordem); esta sobrecarga só decide EM
    /// QUE ORDEM as cláusulas resultantes aparecem no array externo, pelo mesmo critério de
    /// bytes que todo o resto do envelope usa para conjuntos sem chave natural.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Os átomos de cada cláusula têm de estar na ordem canônica ANTES de chegar aqui — a
    /// chave de uma cláusula só é estável se o conteúdo dela já estiver na forma final.
    /// Ordenar cláusulas cujos átomos ainda estão na ordem de entrada produziria uma chave
    /// que depende da ordem em que o cliente enviou os átomos, o mesmo problema que esta
    /// correção existe para eliminar — só um nível acima.
    /// </para>
    /// <para>
    /// Assinatura explícita para <see cref="JsonArray"/>, não um <see cref="JsonNode"/>
    /// genérico — a mesma disciplina da sobrecarga escalar acima, que aceita só
    /// <see cref="JsonValue"/>: ampliar a política de ordenação por acidente para qualquer
    /// nó não é o objetivo desta correção.
    /// </para>
    /// <para>
    /// O chamador acumula as cláusulas num <see cref="List{T}"/> — nunca num
    /// <see cref="JsonArray"/> provisório: <c>System.Text.Json.Nodes</c> recusa anexar um nó
    /// que já tem pai, e um array provisório atribuiria pai a cada cláusula antes da
    /// ordenação, impedindo a reinserção delas no array final.
    /// </para>
    /// </remarks>
    private static JsonArray OrdenarPorConteudo(IEnumerable<JsonArray> clausulas)
    {
        IOrderedEnumerable<JsonArray> ordenadas = clausulas.OrderBy(
            static clausula => PerfilCanonicoV1.SerializarChave(clausula),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenadas.Select(static clausula => (JsonNode)clausula)]);
    }

    /// <summary>
    /// O valor de um átomo <c>{fato, operador, valor}</c>, decidido PELO OPERADOR, não pela
    /// forma do JSON recebido (issue #1068, D3): só <see cref="Operador.Em"/> e
    /// <see cref="Operador.NaoEm"/> carregam um conjunto de alternativas sem posição própria
    /// entre elas; qualquer outro operador carrega um escalar, preservado tal como chegou. Um
    /// array sob outro operador não tem garantia de ser conjunto e não é tocado — a decisão
    /// não pode vir da forma do valor (um array também seria a forma de um escalar JSON
    /// opaco em outro contexto), só da matriz operador × domínio que
    /// <see cref="ValueObjects.CondicaoDnf"/> já valida.
    /// </summary>
    /// <remarks>
    /// Cada alternativa é analisada isoladamente, a partir do <see cref="JsonElement"/> bruto
    /// de origem — nunca via <c>JsonNode.Parse(valor.GetRawText()).AsArray()</c>: os filhos
    /// que aquele array devolveria já pertenceriam a ELE, e <c>System.Text.Json.Nodes</c>
    /// recusa reatribuir um nó com pai a outro container (aqui, o array ordenado). Analisar
    /// cada <see cref="JsonElement"/> item por item, isoladamente, produz nós novos e sem pai,
    /// aptos a entrar em <see cref="OrdenarPorConteudo(IEnumerable{JsonValue})"/>.
    /// </remarks>
    private static JsonNode? SerializarValorDeAtomo(Operador operador, JsonElement valor)
    {
        if (operador is not (Operador.Em or Operador.NaoEm))
        {
            return JsonNode.Parse(valor.GetRawText());
        }

        List<JsonValue> alternativas = [];
        foreach (JsonElement item in valor.EnumerateArray())
        {
            if (JsonNode.Parse(item.GetRawText()) is not JsonValue alternativa)
            {
                throw new InvalidOperationException(
                    $"O operador {operador.ToCodigo()} carrega uma alternativa que não é um valor escalar — " +
                    "PredicadoDnfValidador já deveria ter recusado a condição antes da publicação.");
            }

            alternativas.Add(alternativa);
        }

        return OrdenarPorConteudo(alternativas);
    }

    private static JsonObject SerializarReferenciaRegra(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };

    /// <summary>
    /// Os fatos que o processo coleta do candidato (Story #928, §7.4), ordenados pela <c>Ordem</c>
    /// de coleta (total e única — a mesma que dá sentido a "fato anterior"). A pré-condição de cada
    /// fato é a mesma forma DNF de <see cref="SerializarCondicaoGatilho"/> — <c>null</c> quando o
    /// fato é coletado incondicionalmente. <c>rotulo</c>/<c>tipoRenderizacao</c>/<c>obrigatorio</c>
    /// são a apresentação do campo no formulário de inscrição (Story #559); <c>valoresSelecionaveis</c>
    /// (issue #1059, UNI-REQ-0072) são as opções que o candidato pode escolher.
    /// </summary>
    /// <remarks>
    /// <c>valoresSelecionaveis</c> obedece a bicondicional com <c>tipoRenderizacao</c>:
    /// <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c> ⟺ array com cardinalidade mínima 1
    /// (issue #1077: nunca vazio — um seletor publicado sem opção nenhuma não é respondível);
    /// <c>BOOLEANO</c>/<c>NUMERO</c> ⟺ <see langword="null"/>. <paramref name="valoresSelecionaveisCongelados"/>
    /// não é revalidado contra o catálogo — quem garante que ele tem uma entrada para todo fato de
    /// seleção, ANTES de canonicalizar, é o handler (<c>ResolvedorValoresSelecionaveisCongelados</c>);
    /// a ausência de entrada para um fato de seleção é erro de PROGRAMAÇÃO, não lista vazia — este
    /// método LANÇA nesse caso, nunca emite <c>[]</c> por omissão (sem isso, um handler que
    /// esquecesse de passar o dicionário publicaria um seletor vazio e a prova de round-trip
    /// passaria mesmo assim).
    /// </remarks>
    internal static JsonArray SerializarFatosColetados(
        IEnumerable<FatoColetado> fatos,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados)
    {
        JsonArray array = [];
        foreach (FatoColetado fato in fatos.OrderBy(static f => f.Ordem))
        {
            bool ehFatoDeSelecao = fato.TipoRenderizacao is TipoRenderizacao.SelecaoUnica or TipoRenderizacao.SelecaoMultipla;
            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato = null;
            bool temEntrada = valoresSelecionaveisCongelados?.TryGetValue(fato.FatoCodigo, out valoresDoFato) ?? false;

            if (ehFatoDeSelecao && (!temEntrada || valoresDoFato is null))
            {
                throw new InvalidOperationException(
                    $"O fato coletado '{fato.FatoCodigo}' é de seleção ({fato.TipoRenderizacao}), mas o dicionário " +
                    "de valores selecionáveis recebido pelo canonicalizador não tem entrada para ele — a ausência é " +
                    "erro de programação do handler, nunca lista vazia por omissão.");
            }

            if (ehFatoDeSelecao && valoresDoFato is { Count: 0 })
            {
                // issue #1077: o gate de pré-canonicalização (ProcessoSeletivo.
                // PendenciaDeFatoColetadoSemValoresOfertados) já deveria ter recusado a
                // publicação antes deste ponto — chegar aqui é erro de programação do handler
                // (esqueceu o gate ou o resolvedor), nunca lista vazia legítima.
                throw new InvalidOperationException(
                    $"O fato coletado '{fato.FatoCodigo}' é de seleção ({fato.TipoRenderizacao}), mas o dicionário " +
                    "de valores selecionáveis traz uma lista VAZIA para ele — o canonicalizador nunca serializa " +
                    "um seletor sem opção nenhuma.");
            }

            if (!ehFatoDeSelecao && temEntrada && valoresDoFato is not null)
            {
                throw new InvalidOperationException(
                    $"O fato coletado '{fato.FatoCodigo}' não é de seleção ({fato.TipoRenderizacao}), mas o " +
                    "dicionário de valores selecionáveis traz um array para ele — a bicondicional exige null.");
            }

            array.Add(new JsonObject
            {
                ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(fato.FatoCodigo),
                ["ordem"] = fato.Ordem,
                ["rotulo"] = HashCanonicalComputer.NormalizeNfc(fato.Rotulo),
                ["tipoRenderizacao"] = fato.TipoRenderizacao.ToCodigo(),
                ["obrigatorio"] = fato.Obrigatorio,
                ["precondicao"] = SerializarDnf(fato.Precondicoes.Select(
                    static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
                ["valoresSelecionaveis"] = ehFatoDeSelecao
                    ? new JsonArray([.. valoresDoFato!
                        .OrderBy(static v => v.Ordem)
                        .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                        .Select(static v => (JsonNode)new JsonObject
                        {
                            ["valorCodigo"] = HashCanonicalComputer.NormalizeNfc(v.Codigo),
                            ["descricao"] = v.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                            ["ordem"] = v.Ordem,
                        })])
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// As regras de derivação dos fatos derivados do processo (Story #928, §7.4), ordenadas pelo
    /// <c>codigoFato</c> derivado (chave de negócio única no processo). As regras de cada fato saem
    /// pela sua <c>Ordem</c> (total e única na configuração); a regra âncora (incondicional) tem
    /// <c>quando</c> nulo — nunca um predicado vazio, que avaliaria falso.
    /// </summary>
    internal static JsonArray SerializarRegrasDerivacao(IEnumerable<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            JsonArray regras = [];
            foreach (RegraDerivacaoConfigurada regra in config.Regras.OrderBy(static r => r.Ordem))
            {
                regras.Add(new JsonObject
                {
                    ["ordem"] = regra.Ordem,
                    ["contribui"] = HashCanonicalComputer.NormalizeNfc(regra.Contribui),
                    ["quando"] = SerializarDnf(regra.Condicoes.Select(
                        static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
                });
            }

            array.Add(new JsonObject
            {
                ["codigoFato"] = HashCanonicalComputer.NormalizeNfc(config.CodigoFato),
                ["regras"] = regras,
            });
        }

        return array;
    }

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> — OU de cláusulas, E de condições
    /// dentro de cada uma. <c>Clausula</c> é o ordinal que AGRUPA os átomos na mesma
    /// conjunção (é o que o <c>GroupBy</c> abaixo usa, e é o único papel dele) — não é chave
    /// de ordenação: quem envia a configuração escolhe o ordinal livremente, então o mesmo
    /// predicado enviado com ordinais diferentes não pode produzir bytes diferentes (issue
    /// #1068). As cláusulas resultantes são ordenadas pela CHAVE DE CONTEÚDO do array de
    /// átomos já canonicalizado (D9, mesmo critério do resto do envelope para conjuntos sem
    /// chave natural); as condições dentro de cada cláusula, que tampouco têm ordinal
    /// próprio, usam a mesma chave um nível abaixo. <see langword="null"/> quando não há
    /// condição nenhuma.
    /// </summary>
    private static JsonArray? SerializarDnf(
        IEnumerable<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes)
    {
        List<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> lista = [.. condicoes];
        if (lista.Count == 0)
        {
            return null;
        }

        // Acumulador em List<JsonArray>, não em JsonArray provisório: um array provisório
        // atribuiria pai a cada cláusula assim que ela fosse adicionada, e a sobrecarga de
        // ordenação abaixo recusaria reinseri-las no array externo final.
        List<JsonArray> clausulas = [];
        foreach (IGrouping<int, (int Clausula, string Fato, Operador Operador, JsonElement Valor)> clausula
            in lista.GroupBy(static c => c.Clausula))
        {
            clausulas.Add(OrdenarPorConteudo(clausula.Select(static c => new JsonObject
            {
                ["fato"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
                ["operador"] = c.Operador.ToCodigo(),
                ["valor"] = SerializarValorDeAtomo(c.Operador, c.Valor),
            })));
        }

        return OrdenarPorConteudo(clausulas);
    }

    /// <summary>
    /// O grafo de dependência conjunto (Story #928, §6/§7.4) congelado como testemunho: nós, arestas
    /// e ordem topológica total. É recomputado da configuração viva (<see cref="ProcessoSeletivo.ConstruirGrafoDependencia"/>);
    /// um ciclo é invariante quebrada — o gate de publicação já o recusou antes de canonicalizar
    /// (mesma disciplina de <see cref="SerializarAtendimento"/>: nunca se congela um grafo cíclico
    /// em silêncio).
    /// </summary>
    private static JsonObject SerializarGrafoDependencia(ProcessoSeletivo processo)
    {
        Result<GrafoDependenciaConjunta> grafo = processo.ConstruirGrafoDependencia();
        if (grafo.IsFailure)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo cujo grafo de dependência conjunto forma um ciclo — o gate de "
                + $"publicação deveria tê-lo recusado antes deste ponto: {grafo.Error!.Message}");
        }

        return SerializarGrafoDependencia(grafo.Value!);
    }

    /// <summary>
    /// A projeção pura do grafo conjunto num <see cref="JsonObject"/> — reusada pelo decoder para
    /// recomputar o testemunho a partir das partes reidratadas e comparar bytes com o congelado. Os
    /// nós, arestas e a ordem topológica saem na ordem canônica que <see cref="GrafoDependenciaConjunta"/>
    /// já devolve (por ordem efetiva de coleta, depois <c>(Classe, Codigo)</c>) — não se reordena aqui.
    /// Cada nó é <c>tipoDeNo/codigo</c> — a identidade escopada ao processo <b>por construção</b> (o
    /// envelope pertence a um único processo, <c>VersaoConfiguracao.ProcessoSeletivoId</c>), sem repetir
    /// o Id da raiz em cada nó.
    /// </summary>
    internal static JsonObject SerializarGrafoDependencia(GrafoDependenciaConjunta grafo) => new()
    {
        ["nos"] = new JsonArray([.. grafo.Nos.Select(no => (JsonNode)new JsonObject
        {
            ["idCanonico"] = RotuloCanonico(no),
        })]),
        ["arestas"] = new JsonArray([.. grafo.Arestas.Select(aresta => (JsonNode)new JsonObject
        {
            ["tipo"] = aresta.Tipo.ToCodigo(),
            ["origem"] = RotuloCanonico(aresta.Origem),
            ["destino"] = RotuloCanonico(aresta.Destino),
        })]),
        ["ordemTopologica"] = new JsonArray(
            [.. grafo.OrdemTopologica.Select(no => (JsonNode?)JsonValue.Create(RotuloCanonico(no)))]),
    };

    private static string RotuloCanonico(NoGrafoDependencia no) => $"{no.Classe.ToCodigo()}/{no.Codigo}";

    /// <summary>
    /// O conjunto de códigos de modalidade ofertados pelo processo (Story #928, §7.4) — o domínio
    /// usado na interseção do fato derivado <c>MODALIDADE</c>. Normalizado NFC antes do <c>Distinct</c>
    /// e ordenado ordinal (para códigos ASCII atuais coincide, mas fecha o contrato declarado).
    /// </summary>
    internal static JsonArray SerializarModalidadesOfertadas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        IEnumerable<string> codigos = distribuicao
            .SelectMany(static d => d.Modalidades)
            .Select(static m => HashCanonicalComputer.NormalizeNfc(m.Codigo))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static c => c, StringComparer.Ordinal);

        return new JsonArray([.. codigos.Select(static c => (JsonNode?)JsonValue.Create(c))]);
    }
}
