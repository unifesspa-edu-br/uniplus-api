namespace Unifesspa.UniPlus.Selecao.ArchTests;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As entradas do manifesto do edital, dos snapshots, dos argumentos de regra e do cronograma de fases.</summary>
internal static class ManifestoDeEditalSnapshotsECronograma
{
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas = new()
    {
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
            ["OrigemId", "Codigo", "Nome", "AdmitePontuacao", "AdmiteEliminacao", "NotaDeOrigemNoEnem", "ExigeCorrecaoIndependente"],
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
            ["Codigo", "CursoNome", "CursoGrau", "UnidadeOfertanteNome", "Turnos"],
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
                ("TiposDeDecisao", "Ainda não entra no envelope congelado nem no hash."),
                ("RegraDeHomologacao", "Ainda não entra no envelope congelado nem no hash."),
            ]),

        // Regra de homologação das inscrições declarada pela fase: ainda fora do envelope congelado.
        [typeof(RegraDeHomologacaoDasInscricoes)] = (
            [],
            [
                ("ResultadoAtoCodigo", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
                ("Alternativas", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
                ("HomologaTodaInscricaoEnviada", "Derivada de Alternativas."),
            ]),
        [typeof(AlternativaDeCondicoes)] = ([], [("Condicoes", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash.")]),
        [typeof(CondicaoDeHomologacao)] = (
            [],
            [
                ("Tipo", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
                ("EdicaoDoExame", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
                ("MotivoDesfavoravelCodigo", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
                ("Enquadramento", "A regra de homologação das inscrições ainda não entra no envelope congelado nem no hash."),
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
            ["Regra", "ProdutoAncoraId", "Args", "Procedimentos"],
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
    };
}
