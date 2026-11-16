namespace Unifesspa.UniPlus.Selecao.ArchTests;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As entradas do manifesto do envelope: a declaração, por tipo do grafo, das propriedades que entram
/// no documento com peso jurídico e das que ficam de fora, cada uma com o motivo.
/// </summary>
internal static class ManifestoDoEnvelope
{
    /// <summary>
    /// O manifesto. Cada propriedade de negócio de cada entidade do grafo aparece aqui —
    /// como <b>congelada</b> (entra no envelope) ou <b>excluída</b> (com o motivo).
    /// </summary>
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas = new()
    {
        [typeof(BancaDaEtapa)] = (
            ["TipoBancaOrigemId", "Codigo", "Nome", "RegraDeDeliberacao"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(RecursoDaEtapa)] = (
            ["Ancora", "Regra", "Args", "ProdutoAncoraId"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(ProdutoDaEtapa)] = (
            ["AtoCodigo", "Papel"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(EtapaProcesso)] = (
            ["Nome", "Carater", "TipoEtapa", "Peso", "NotaMinima", "NotaMaxima", "PassoDoLancamento", "FormaDeAvaliacao", "RegraConsolidacao", "ItensDeAvaliacao", "EscalaDeAtendimento", "Ordem", "FaseCodigo", "Produtos",
             "Inicio", "Fim", "EmiteParecerIndividual", "Bancas", "Recursos"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("ProduzResultado", "Derivada de Produtos — congelá-la duplicaria a fonte de verdade."),
                ("ComponeNota", "Derivada de Carater + Peso — congelá-la duplicaria a fonte de verdade."),
                ("DeclaraNotaDoEnem", "Derivada de TipoEtapa.NotaDeOrigemNoEnem — o atributo congelado já é a declaração."),
                ("TipoEtapaOrigemId", "Derivada de TipoEtapa.OrigemId (issue #1071) — congelá-la duplicaria a fonte de verdade."),
            ]),

        // O id do item ENTRA no envelope, como o da etapa: o lançamento e o recurso apontam para ele.
        [typeof(ItemDeAvaliacao)] = (
            ["Rotulo", "PontuacaoMaxima"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(NivelDeAtendimento)] = (["Rotulo", "Fracao"], []),

        [typeof(AtendimentoEspecializado)] = (
            ["Condicoes", "Recursos", "TiposDeficiencia"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(CondicaoOfertada)] = (
            ["CondicaoOrigemId", "CondicaoCodigo", "CondicaoNome"],
            [("AtendimentoEspecializadoId", "FK interna.")]),

        [typeof(RecursoOfertado)] = (
            ["RecursoOrigemId", "RecursoCodigo", "RecursoNome"],
            [("AtendimentoEspecializadoId", "FK interna.")]),

        [typeof(DeficienciaAtendida)] = (
            ["TipoDeficienciaOrigemId", "TipoDeficienciaCodigo", "TipoDeficienciaNome"],
            [("AtendimentoEspecializadoId", "FK interna.")]),

        [typeof(ConfiguracaoDistribuicaoVagas)] = (
            [
                "OfertaCursoOrigemId", "VagasOfertadasBase", "PercentualDaReservaLegal", "RegraDistribuicao", "RegraAjuste", "AjusteDoQuadro", "ReferenciaDemografica",
                "GrupoAreaEnem", "OfertaCurso", "Modalidades", "ReservaLegalNominal", "ReservaLegalEfetiva", "ExcedenteSobreReservaLegal", "VagasCortadasPorEscassez",
                "VagasRetiradasNoAjuste", "TotalDeVagasDoEdital", "VagasOfertadas",
            ],
            [
                ("ProcessoSeletivoId", "FK interna."),
                ("Quadro", "Derivada das vagas ofertadas e das colunas do ramo — recomputada, nunca persistida."),
            ]),

        [typeof(ArgsReduzirDe)] = (["ModalidadeCodigo"], []),
        [typeof(ArgsReduzirProporcionalEm)] = (["ModalidadeCodigos"], []),

        [typeof(VagasDaModalidade)] = (["ModalidadeCodigo", "Quantidade"], []),

        [typeof(QuadroDeVagas)] = (
            [],
            [
                ("Vagas", "Derivada das vagas ofertadas — o envelope congela as vagas de cada modalidade, não o quadro."),
                ("TotalDeVagasDoEdital", "Congelado na própria configuração; o quadro apenas o expõe."),
            ]),

        [typeof(ModalidadeSelecionada)] = (
            [
                "ModalidadeOrigemId", "Codigo", "Descricao", "NaturezaLegal", "ComposicaoVagas",
                "ComposicaoOrigemCodigo", "RegraRemanejamento", "RemanejamentoDestino", "RemanejamentoPar",
                "RemanejamentoFallback", "RegraDeAplicabilidade", "AcaoQuandoIndeferido", "BaseLegal",
                "QuantidadeDeclarada",
            ],
            [("ConfiguracaoDistribuicaoVagasId", "FK interna.")]),

        // issue #848/ADR-0115 — o quadro de vagas: output derivado, materializado dentro da
        // mesma factory que os insumos (não um comando separado).
        [typeof(VagaOfertada)] = (
            ["ModalidadeOrigemId", "ModalidadeCodigo", "Quantidade"],
            [("ConfiguracaoDistribuicaoVagasId", "FK interna.")]),

        // Story #1466: o bônus regional passa a referenciar um cadastro tipado de Base Legal
        // de Bônus Regional (Configuração, Story #1465) — o snapshot congelado no momento da
        // gravação substitui os dois campos de texto livre.
        [typeof(ConfiguracaoBonusRegional)] = (
            ["Regra", "Fator", "Teto", "FatoElegibilidadeCodigo", "BaseLegalBonusRegionalId", "BaseLegalBonusRegionalCodigo", "TipoInstrumento", "Identificacao", "Descricao", "Municipios"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(ConfiguracaoBonusRegionalMunicipio)] = (
            ["CodigoIbge", "Nome", "Uf"],
            [("ConfiguracaoBonusRegionalId", "FK interna.")]),

        // Divulgação pública (UNI-REQ-0050, issue #563) — a última dimensão provisória do
        // envelope. A regra de abreviação (regraNomeAbreviado) NÃO é propriedade desta
        // entidade — é derivada no congelamento a partir de RegrasDeNomeAbreviado.Vigente
        // (ADR-0109 D6, decisão D3 do plano da issue), então não aparece aqui como campo
        // excluído: não existe para o manifesto excluir.
        [typeof(ConfiguracaoDivulgacao)] = (
            ["CamposPublicos", "Justificativa"],
            [("ProcessoSeletivoId", "FK interna.")]),

        // Taxa de inscrição e isenção (issue #1112) — diferente de ConfiguracaoBonusRegional/
        // ConfiguracaoDivulgacao, ausência aqui NÃO é estado publicável (CA-01).
        [typeof(ConfiguracaoTaxaInscricao)] = (
            ["Cobra", "Valor", "Fundamentos"],
            [("ProcessoSeletivoId", "FK interna.")]),

        // Localidade regente (UNI-REQ-0111) — o município cujo calendário rege a contagem dos
        // prazos. Congela os três campos: o código é o valor normativo, e nome e UF viajam como
        // cache de exibição da versão, que não participa de cálculo. O fuso aplicado vai no mesmo
        // bloco do envelope, mas não é propriedade deste tipo — é constante institucional, e o
        // envelope o carrega para que a recanonicalização use o congelado, não o vigente.
        [typeof(LocalidadeRegente)] = (
            ["CodigoIbge", "Nome", "Uf"],
            []),

        // Story #575 — a cascata de remanejamento das cotas federais (cascata de remanejamento de cotas, UNI-REQ-0056).
        [typeof(ConfiguracaoCascataRemanejamento)] = (
            ["Regra", "FallbackCodigo", "Destinos"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(DestinoRemanejamento)] = (
            ["ModalidadeOrigemCodigo", "Ordem", "ModalidadeDestinoCodigo"],
            [("ConfiguracaoCascataRemanejamentoId", "FK interna.")]),

        [typeof(CriterioDesempate)] = (
            ["Ordem", "Regra", "Args"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(ConfiguracaoClassificacao)] = (
            [
                "RegraCalculo", "RegraArredondamento", "CasasArredondamento",
                "RegraOrdemAlocacao", "RegrasEliminacao", "BaseadoEmEnem",
                "ResolucaoPesoAreaEnem", "QuadroPesoAreaEnem", "EscalaNotaFinal",
            ],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(RegraEliminacao)] = (
            ["Regra", "Args"],
            [("ConfiguracaoClassificacaoId", "FK interna.")]),

        // Quadro de pesos por área do ENEM congelado na classificação: a cópia por valor da
        // resolução declarada, que a nota do candidato vai usar.
        [typeof(GrupoPesoAreaEnemCongelado)] = (
            ["GrupoAreaEnem", "BaseLegal", "Areas"],
            [("ConfiguracaoClassificacaoId", "FK interna.")]),

        [typeof(AreaPesoAreaEnemCongelada)] = (
            ["Codigo", "Rotulo", "Peso", "Corte"],
            [("GrupoPesoAreaEnemCongeladoId", "FK interna.")]),

        [typeof(DadosEdital)] = (
            ["Numero", "PeriodoInscricaoInicio", "PeriodoInscricaoFim", "DocumentoEditalId", "Anexos"],
            []),

        // Anexo do edital: copiado por valor do documento confirmado, no bloco hashesEdital.
        [typeof(AnexoDoEdital)] = (
            ["Id", "DocumentoId", "NomeArquivo", "Formato", "TamanhoBytes", "HashSha256"],
            [("TipoDeMidia", "Derivada de Formato — congelá-la duplicaria a fonte de verdade.")]),

        [typeof(ReferenciaRegra)] = (
            ["Codigo", "Versao", "Hash"],
            []),

        // Identificador legível — o endereço público do certame, congelado com a versão como texto.
        [typeof(IdentificadorLegivel)] = (
            ["Valor"],
            []),

        // Issue #1071 — snapshot de tipo de etapa, mesmo padrão de cópia por valor de
        // ReferenciaRegra acima.
        [typeof(TipoEtapaSnapshot)] = (
            ["OrigemId", "Codigo", "Nome", "AdmitePontuacao", "AdmiteEliminacao", "NotaDeOrigemNoEnem"],
            []),

        [typeof(ReferenciaReservaDemograficaSnapshot)] = (
            ["OrigemId", "CensoReferencia", "PpiPercentual", "QuilombolaPercentual", "PcdPercentual", "BaseLegal"],
            []),

        // Grupo de área do ENEM da oferta: código e rótulo congelados por valor.
        [typeof(GrupoAreaEnemSnapshot)] = (
            ["Codigo", "Rotulo"],
            []),

        // O que nomeia a oferta para o candidato, congelado por valor; vai no bloco público de ofertas.
        [typeof(OfertaCursoSnapshot)] = (
            ["CursoNome", "CursoGrau", "UnidadeOfertanteNome", "Turnos"],
            []),

        // As variantes polimórficas. Elas são persistidas INTEIRAS como JSON, e o codec as
        // lê e escreve em dois `switch` sobre o código da regra — duas listas manuais, num
        // ponto em que o envelope nem sequer carrega discriminador (MAIOR-IDADE e
        // ZERO-EM-AREA serializam como `{}`). Uma propriedade nova aqui, esquecida nos dois
        // switches, é invisível: o envelope não muda, a fixture não muda, o round-trip fica
        // verde — e o campo some no primeiro descarte.
        [typeof(ArgsDesempateMaiorNotaEtapa)] = (["EtapaRef"], []),
        [typeof(ArgsDesempateMaiorIdade)] = ([], []),
        [typeof(ArgsDesempateIdoso)] = (["IdadeMinima"], []),
        [typeof(ArgsDesempatePredicadoFato)] = (["Condicao"], []),
        [typeof(ArgsDesempateMaiorNotaAreaEnem)] = (["Areas"], []),
        // CondicaoDnf (ADR-0111, Story #847): átomo tipado { Fato, Operador, Valor }
        // reusado literalmente pela variante acima — mesmo cuidado de propriedade
        // esquecida se aplica aqui.
        [typeof(CondicaoDnf)] = (["Fato", "Operador", "Valor"], []),
        [typeof(ArgsElimNotaMinimaEtapa)] = (["EtapaRef", "NotaMinima"], []),
        [typeof(ArgsElimCorteEmArea)] = (["AreaCodigo", "Minimo"], []),
        [typeof(ArgsElimZeroEmArea)] = ([], []),
        [typeof(ArgsElimFaltaEmDiaDeProvaEnem)] = ([], []),
        [typeof(ArgsElimAusenciaNaEtapa)] = (["EtapaRef"], []),

        // Cronograma de fases (Story #851) — o eixo temporal do certame.
        [typeof(FaseCronograma)] = (
            [
                "Ordem", "FaseCanonicaOrigemId", "Codigo", "OrigemData",
                "AgrupaEtapas", "PermiteComplementacao",
                "ColetaInscricao", "ColetaSolicitacaoIsencao", "Inicio", "Fim",
                "Produtos", "FaseConcluinteCodigo", "EmiteParecerIndividual",
                "BancasRequeridas", "RegraRecurso", "UnidadeResponsavel",
            ],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("UnidadeResponsavelOrigemId", "Coluna que materializa UnidadeResponsavel, congelada pelo value object."),
                ("UnidadeResponsavelSigla", "Coluna que materializa UnidadeResponsavel, congelada pelo value object."),
                ("UnidadeResponsavelNome", "Coluna que materializa UnidadeResponsavel, congelada pelo value object."),
                ("ProduzResultado", "Derivada de Produtos — a fase produz resultado quando declara ao menos um produto com papel; congelá-la duplicaria a fonte de verdade."),
            ]),

        // Cópia da unidade responsável pela fase (ADR-0061), congelada na publicação.
        [typeof(UnidadeResponsavelSnapshot)] = (["OrigemId", "Sigla", "Nome"], []),

        // O produto que a fase publica. O Id ENTRA no envelope, ao contrário do das demais
        // filhas do cronograma: é por ele que o ato publicado resolverá, de volta, a
        // configuração de recurso que lhe corresponde.
        [typeof(ProdutoDaFase)] = (
            ["AtoCodigo", "Papel"],
            [("FaseCronogramaId", "FK interna.")]),

        [typeof(BancaRequerida)] = (
            ["TipoBancaOrigemId", "Codigo", "Nome", "RecorteDeCompetencia", "RegraDeDeliberacao"],
            [("FaseCronogramaId", "FK interna.")]),

        // A regra de deliberação da banca, igual na banca da fase e na da etapa. Sem Id no
        // envelope: é valor da banca que a carrega.
        [typeof(RegraDeDeliberacao)] = (
            ["Modo", "Integrantes", "Suplentes", "Quorum", "Criterio", "Desempate", "Encerramento", "SemUnanimidade"],
            [("QuorumExigido", "Derivada de Quorum — o ato individual exige um titular; congelá-la duplicaria a fonte de verdade.")]),

        // O recorte de competência — o que distingue duas bancas do MESMO tipo na mesma
        // fase. Sem Id no envelope, como a banca que a carrega: nada aponta para a linha de
        // fora dela.
        [typeof(CategoriaJulgada)] = (
            ["CategoriaDocumentoOrigemId", "Codigo"],
            [("BancaRequeridaId", "FK interna.")]),

        // RegraRecursoFase.Args NÃO é polimórfico (única variante — CA-02 recusa
        // qualquer regra que não seja RECURSO-PRAZO-ANCORADO-EM-ATO), por isso não
        // entra em UnioesDiscriminadas: é alcançado diretamente como ArgsRegraPrazoRecurso.
        [typeof(RegraRecursoFase)] = (
            ["Regra", "ProdutoAncoraId", "Args"],
            [("FaseCronogramaId", "FK interna.")]),

        [typeof(ArgsRegraPrazoRecurso)] = (
            [
                "PrazoValor", "PrazoUnidade",
                "SuspensividadePrimeiraInstanciaValor", "SuspensividadePrimeiraInstanciaUnidade",
                "AdmiteInstanciaSuperior",
                "SuspensividadeSegundaInstanciaValor", "SuspensividadeSegundaInstanciaUnidade",
            ],
            []),

        // Conformidade legal (Story #853) — não pende de GrafoConfiguracao (não é reposta no
        // agregado vivo); alcançada a partir de EnvelopeReidratado.Conformidade, semeada
        // manualmente no traversal como DadosEdital já era.
        [typeof(ResultadoConformidade)] = (
            ["Regras"],
            [("Avisos", "Diagnóstico transitório da avaliação (ex.: uso de Customizado), não evidência " +
                "jurídica — reconstruído ao reavaliar, não precisa sobreviver ao congelamento.")]),

        [typeof(RegraAvaliada)] = (
            [
                "RegraId", "RegraCodigo", "Categoria", "TipoProcessoCodigoAvaliado", "Predicado",
                "Aprovada", "BaseLegal", "AtoNormativoUrl", "PortariaInterna", "DescricaoHumana",
                "VigenciaInicio", "VigenciaFim", "Hash",
            ],
            [("Motivo", "Razão nomeada da reprovação (CA-02/CA-03/CA-09) — diagnóstico transitório, " +
                "não evidência forense: só regras APROVADAS são congeladas (§3.4), logo é sempre nulo " +
                "em qualquer entrada que chegue ao envelope.")]),

        // As 7 variantes de PredicadoObrigatoriedade (ADR-0058, ADR-0114) — união discriminada
        // pelo campo `tipo` no envelope (o próprio predicado já é a forma que o domínio
        // serializa, ao contrário de ArgsCriterioDesempate/ArgsRegraEliminacao, cuja variante
        // vem do código da regra externa). BonusObrigatorio, oitava variante original, foi
        // descartada (ADR-0114, executado pela #853): ConfiguracaoBonusRegional é global ao
        // processo, sem lista de modalidades.
        [typeof(EtapaObrigatoria)] = (["TipoEtapaCodigo"], []),
        [typeof(ModalidadesMinimas)] = (["Codigos"], []),
        [typeof(DesempateDeveIncluir)] = (["Criterio"], []),
        [typeof(DocumentoObrigatorioParaModalidade)] = (["Modalidade", "TipoDocumento"], []),
        [typeof(AtendimentoDisponivel)] = (["Necessidades"], []),
        [typeof(ConcorrenciaDuplaObrigatoria)] = ([], []),
        [typeof(Customizado)] = (["Parametros"], []),

        // Documentos exigidos (Story #554, PR #903) — exigencias[] real, CA-09 (exigenciaId
        // estável). Aplicabilidade entra porque é o que o resolvedor (PR #903, fora deste
        // bloco) consome para correlacionar apresentação↔exigência.
        [typeof(DocumentoExigido)] = (
            [
                "ExigidoNaFaseId", "ExigidoNaEtapaId", "Finalidade", "TipoDocumentoOrigemId", "TipoDocumentoCodigo", "TipoDocumentoNome",
                "TipoDocumentoCategoria", "ClassificacaoProtecao", "FinalidadeTratamento", "HipoteseLegal", "FatosComprovados",
                "Aplicabilidade", "Obrigatorio", "ConsequenciaIndeferimento",
                "Condicoes", "BasesLegais", "IdadeMaximaEmissao", "FormatosPermitidos", "Modelo",
                "TamanhoMaximoBytes",
            ],
            [("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2).")]),

        [typeof(CondicaoGatilho)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("DocumentoExigidoId", "FK interna.")]),

        [typeof(DocumentoExigidoBaseLegal)] = (
            ["Referencia", "Abrangencia", "Status", "Observacao"],
            [("DocumentoExigidoId", "FK interna.")]),

        // Árvore de satisfação (Story #920, #921, #922) — substituiu o grupo de satisfação
        // plano, que era correlação por Guid solto na exigência. Story #923 (bump 1.4):
        // congelada por inteiro no bloco de topo `arvoreSatisfacao` — cada folha referencia
        // sua exigência pelo MESMO `exigenciaId` já congelado em
        // `documentosExigidos.exigencias[]` (por isso `DocumentoExigido` — a navegação — não
        // é uma chave própria do envelope: é `DocumentoExigidoId` que vira `exigenciaId`).
        [typeof(NoExigencia)] = (
            [
                "NoPaiId", "Ordem", "Tipo", "DocumentoExigidoId", "QuantidadeMinima", "Consequencia", "Filhos",
                "BasesLegais", "ChaveDistincao", "DataReferencia", "OcorrenciasEsperadas", "RepetePorEntidade",
            ],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("DocumentoExigido", "Navegação da folha — o envelope congela DocumentoExigidoId (como `exigenciaId`, referenciando documentosExigidos.exigencias[]), não o objeto/conteúdo duplicado."),
            ]),

        [typeof(NoExigenciaBaseLegal)] = (
            ["Referencia", "Abrangencia", "Status", "Observacao"],
            [("NoExigenciaId", "FK interna.")]),

        [typeof(IdadeMaximaEmissao)] = (
            ["Valor", "Unidade", "ReferenciaTipo", "Data", "ReferenciaFaseId"],
            []),

        // Formatos permitidos (Story #918) — substitui o campo singular FormatoPermitido?
        // (enum, fora do alcance deste manifesto — EDoDominio exclui enums).
        [typeof(FormatosPermitidos)] = (["Qualquer", "Lista"], []),
        [typeof(ModeloDaExigencia)] = (["ModeloId", "NomeArquivo", "Formato", "HashSha256"], []),
        [typeof(FormatoPermitidoEntry)] = (["Formato", "TamanhoMaximoBytesMax"], []),

        // A POLÍTICA crua (B-03) — insumo de ProcessoSeletivo.ResolverDataReferenciaFatos.
        // A DateOnly resolvida (dataReferenciaFatos) é output derivado, não uma propriedade
        // de entidade — não passa por este manifesto (é lida diretamente do método).
        [typeof(ReferenciaTemporalFatos)] = (
            ["Tipo", "Data", "FaseId"],
            []),

        // Coleta de fatos + derivação (Story #928, §7.4) — congeladas nos blocos de topo
        // `fatosColetados`/`regrasDerivacao` e testemunhadas no grafo conjunto (`grafoDependencia`).
        // Rotulo/TipoRenderizacao/Obrigatorio (Story #559) — a apresentação do campo no
        // formulário de inscrição, congelada junto do fato no mesmo bloco `fatosColetados`.
        [typeof(FatoColetado)] = (
            ["FatoCodigo", "Finalidade", "EtapaCodigo", "Ordem", "Rotulo", "TipoRenderizacao", "Obrigatoriedade", "Ajuda", "PedirConfirmacao", "Restricoes", "Impedimento", "OrigemValores", "Formato", "ClassificacaoProtecao", "HipoteseLegal", "FinalidadeTratamento", "Descricao", "ComponenteExibicao", "Precondicoes"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("GrupoColetadoId", "FK interna do campo de grupo — o campo é congelado dentro do grupo, em `gruposColetados`."),
                ("SemPrecondicao", "Derivada de Precondicoes — congelá-la duplicaria a fonte de verdade."),
                ("FatosCitados", "Derivada das Precondicoes, da Obrigatoriedade e das Restricoes — a lista de citações é recomputada, nunca persistida."),
                ("Condicoes", "Derivada das Precondicoes, da Obrigatoriedade e das Restricoes — recomputada, nunca persistida."),
                ("OpcoesDoProcesso", "Derivada de OrigemValores — congelá-la duplicaria a fonte de verdade."),
                ("Exibicao", "Derivada das Precondicoes — o predicado é recomputado, nunca persistido."),
            ]),

        // Grupos repetíveis (UNI-REQ-0146) — congelados no bloco de topo `gruposColetados`, com os
        // campos de cada ocorrência na mesma forma dos itens de `fatosColetados`.
        [typeof(GrupoColetado)] = (
            ["Codigo", "Finalidade", "EtapaCodigo", "Ordem", "Rotulo", "Minimo", "Maximo", "IncluiCandidato", "Exibicao", "Obrigatoriedade", "Subitens"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("Condicoes", "Derivada das regras do grupo e dos campos — recomputada, nunca persistida."),
            ]),

        // Opções declaradas pelo processo (issue #1619) — congeladas nos valores selecionáveis do
        // fato coletado de fonte do processo, e reconstruídas deles pelo decoder.
        [typeof(OpcaoDeclaradaFato)] = (
            ["FatoCodigo", "Codigo", "Rotulo", "Ordem"],
            [("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2).")]),

        [typeof(FormularioProcesso)] = (
            ["Finalidade", "FaseId", "Titulo", "ModeloOrigemId", "ModeloOrigemCodigo", "Etapas"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("Estrutura", "Derivada das Etapas — a forma que a estrutura do formulário confere, recomputada."),
            ]),

        [typeof(EtapaFormulario)] = (
            ["Codigo", "Ordem", "Tipo", "Bloco", "Titulo", "Descricao", "Aviso", "Exibicao"],
            [
                ("FormularioProcessoId", "FK interna."),
                ("Estrutura", "Derivada dos campos da etapa — recomputada, nunca persistida."),
                ("FatosCitados", "Derivada da Exibicao — recomputada, nunca persistida."),
                ("Condicoes", "Derivada da Exibicao — recomputada, nunca persistida."),
            ]),

        [typeof(TermoExigidoFormulario)] = (
            ["Codigo", "Finalidade", "Ordem", "TermoId", "VersaoId", "Nome", "Texto", "BaseLegal", "FormaAceite", "HashVersao", "Exibicao", "Obrigatoriedade"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("Condicoes", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("SemFormaDeAceite", "Derivada de FormaAceite — congelá-la duplicaria a fonte de verdade."),
            ]),

        [typeof(CondicaoPrecondicaoFato)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("FatoColetadoId", "FK interna.")]),

        [typeof(ConfiguracaoDerivacaoFato)] = (
            ["CodigoFato", "Regras"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada das condições das regras — recomputada, nunca persistida."),
                ("Booleano", "Derivada das contribuições das regras — recomputada, nunca persistida."),
                ("CodigosContribuidos", "Derivada das contribuições das regras — recomputada, nunca persistida."),
            ]),

        [typeof(RegraDerivacaoConfigurada)] = (
            ["Ordem", "Contribui", "Condicoes"],
            [("ConfiguracaoDerivacaoFatoId", "FK interna.")]),

        [typeof(CondicaoRegraDerivacao)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("RegraDerivacaoConfiguradaId", "FK interna.")]),
    };
}
