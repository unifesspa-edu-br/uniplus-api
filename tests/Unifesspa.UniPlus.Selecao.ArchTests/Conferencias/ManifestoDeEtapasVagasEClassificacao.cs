namespace Unifesspa.UniPlus.Selecao.ArchTests.Conferencias;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As entradas do manifesto das etapas, do atendimento, das vagas, do FADA e da classificação.</summary>
internal static class ManifestoDeEtapasVagasEClassificacao
{
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas = new()
    {
        [typeof(AvaliacaoDeclarada)] = (["Etapas", "CriteriosDesempate", "Classificacao"], []),
        [typeof(VagasDeclaradas)] = (
            ["DistribuicaoVagas", "NumeroDeOpcoesDeCurso", "Fada", "Cascata", "AtendimentoEspecializado"],
            [("AplicaFada", "A configuração publicada deriva o sinalizador da presença do FADA congelado."),
             ("HaVagasOfertadas", "Derivada das quantidades de vagas declaradas nas ofertas.")]),

        [typeof(BancaDaEtapa)] = (
            ["Chave", "Tipo", "PosicaoNoTipo", "ConsequenciaDaAusencia", "RegraDeDeliberacao"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(RecursoDaEtapa)] = (
            ["Ancora", "Regra", "Parametros", "ProdutoAncoraId", "Procedimentos"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        // Os procedimentos de recurso do período, iguais na etapa e na fase. A coleção guarda a ordem canônica e cada
        // procedimento é valor do período que o declara: sem Id no envelope.
        [typeof(ProcedimentosDeRecurso)] = (
            ["Itens"],
            []),

        [typeof(ProcedimentoDeRecursoDeclarado)] = (
            ["TipoDeDecisao", "Alvo", "Regra"],
            []),

        [typeof(AlvoDoProcedimentoDeRecurso)] = (
            ["Tipo", "Valor"],
            []),

        [typeof(ProdutoDaEtapa)] = (
            ["AtoCodigo", "Papel"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(EtapaProcesso)] = (
            ["Chave", "Nome", "Carater", "TipoEtapa", "Peso", "NotaMinima", "NotaMaxima", "PassoDoLancamento", "FormaDeAvaliacao", "RegraConsolidacao", "ItensDeAvaliacao", "EscalaDeAtendimento", "Ordem", "FaseCodigo", "Produtos",
             "Inicio", "Fim", "EmiteParecerIndividual", "Bancas", "Recursos", "TiposDeDecisao"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("ProduzResultado", "Derivada de Produtos — congelá-la duplicaria a fonte de verdade."),
                ("ComponeNota", "Derivada de Carater + Peso — congelá-la duplicaria a fonte de verdade."),
                ("DeclaraNotaDoEnem", "Derivada de TipoEtapa.NotaDeOrigemNoEnem — o atributo congelado já é a declaração."),
                ("TipoEtapaOrigemId", "Derivada de TipoEtapa.OrigemId (issue #1071) — congelá-la duplicaria a fonte de verdade."),
            ]),

        // O id do item ENTRA no envelope, como o da etapa: o lançamento e o recurso apontam para ele.
        [typeof(ItemDeAvaliacao)] = (
            ["Chave", "Ordem", "Rotulo", "PontuacaoMaxima"],
            [("EtapaProcessoId", "FK interna — reconstruída junto com o grafo.")]),

        [typeof(NivelDeAtendimento)] = (["Chave", "Ordem", "Rotulo", "Fracao"], []),

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
                ("Quadro", "Derivada das vagas ofertadas e das colunas do critério — recomputada, nunca persistida."),
            ]),

        [typeof(ParametrosReduzirDe)] = (["ModalidadeCodigo"], []),
        [typeof(ParametrosReduzirProporcionalEm)] = (["ModalidadeCodigos"], []),

        [typeof(VagasDaModalidade)] = (["ModalidadeCodigo", "Quantidade"], []),

        [typeof(QuadroDeVagas)] = (
            [],
            [
                ("Vagas", "Derivada das vagas ofertadas — o envelope congela as vagas de cada modalidade, não o quadro."),
                ("TotalDeVagasDoEdital", "Congelado na própria configuração; o quadro apenas o expõe."),
            ]),

        [typeof(ModalidadeSelecionada)] = (
            [
                "ModalidadeOrigemId", "Codigo", "Nome", "Descricao", "NaturezaLegal", "ComposicaoVagas",
                "ComposicaoOrigemCodigo", "RegraRemanejamento", "RemanejamentoDestino", "RemanejamentoPar",
                "RemanejamentoFallback", "RegraDeAplicabilidade", "AcaoQuandoIndeferido", "BaseLegal",
                "QuantidadeDeclarada",
            ],
            [("ConfiguracaoDistribuicaoVagasId", "FK interna."), ("CodigoENome", "Texto derivado do código e do nome; não é gravado.")]),

        // ADR-0115 — o quadro de vagas: output derivado, materializado dentro da
        // mesma factory que os insumos (não um comando separado).
        [typeof(VagaOfertada)] = (
            ["ModalidadeOrigemId", "ModalidadeCodigo", "Quantidade"],
            [("ConfiguracaoDistribuicaoVagasId", "FK interna.")]),

        // O FADA referencia um cadastro tipado de Base Legal
        // do FADA (Configuração) — o snapshot congelado no momento da
        // gravação dispensa campos de texto livre.
        [typeof(ConfiguracaoDoFada)] = (
            ["Regra", "Fator", "Teto", "FatoElegibilidadeCodigo", "BaseLegalId", "BaseLegalCodigo", "TipoInstrumento", "Identificacao", "Descricao", "Municipios"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(MunicipioDoFada)] = (
            ["CodigoIbge", "Nome", "Uf"],
            [("ConfiguracaoDoFadaId", "FK interna.")]),

        // Divulgação pública (UNI-REQ-0050) — a última dimensão provisória do
        // envelope. A regra de abreviação (regraNomeAbreviado) NÃO é propriedade desta
        // entidade — é derivada no congelamento a partir de RegrasDeNomeAbreviado.Vigente
        // (ADR-0109 D6), então não aparece aqui como campo
        // excluído: não existe para o manifesto excluir.
        [typeof(ConfiguracaoDivulgacao)] = (
            ["CamposPublicos", "Justificativa"],
            [("ProcessoSeletivoId", "FK interna.")]),

        // Taxa de inscrição e isenção — diferente de ConfiguracaoDoFada/
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

        // A cascata de remanejamento das cotas federais (cascata de remanejamento de cotas, UNI-REQ-0056).
        [typeof(ConfiguracaoCascataRemanejamento)] = (
            ["Regra", "FallbackCodigo", "Destinos"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(DestinoRemanejamento)] = (
            ["ModalidadeOrigemCodigo", "Ordem", "ModalidadeDestinoCodigo"],
            [("ConfiguracaoCascataRemanejamentoId", "FK interna.")]),

        [typeof(CriterioDesempate)] = (
            ["Ordem", "Regra", "Parametros"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(ConfiguracaoClassificacao)] = (
            [
                "RegraCalculo", "RegraArredondamento", "CasasArredondamento",
                "RegraOrdemAlocacao", "RegrasEliminacao", "BaseadoEmEnem",
                "ResolucaoPesoAreaEnem", "QuadroPesoAreaEnem", "EscalaNotaFinal",
            ],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(RegraEliminacao)] = (
            ["Regra", "Parametros"],
            [("ConfiguracaoClassificacaoId", "FK interna.")]),

        // Quadro de pesos por área do ENEM congelado na classificação: a cópia por valor da
        // resolução declarada, que a nota do candidato vai usar.
        [typeof(GrupoPesoAreaEnemCongelado)] = (
            ["GrupoAreaEnem", "BaseLegal", "Areas"],
            [("ConfiguracaoClassificacaoId", "FK interna.")]),

        [typeof(AreaPesoAreaEnemCongelada)] = (
            ["Codigo", "Rotulo", "Peso", "Corte"],
            [("GrupoPesoAreaEnemCongeladoId", "FK interna.")]),
    };
}
