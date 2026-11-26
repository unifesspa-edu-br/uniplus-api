namespace Unifesspa.UniPlus.Selecao.ArchTests.Conferencias;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As entradas do manifesto do edital, dos snapshots, dos parâmetros de regra e do cronograma de fases.</summary>
internal static class ManifestoDeEditalSnapshotsECronograma
{
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas = new()
    {
        [typeof(CronogramaDeclarado)] = (["Localidade", "AlgoritmoContagemPrazo", "CronogramaFases"], []),
        [typeof(DivulgacaoETaxaDeclaradas)] = (["ConfiguracaoDivulgacao", "ConfiguracaoTaxaInscricao"], []),

        // Prova presencial: a declaração do processo e os municípios de prova, com o nome copiado do Geo na publicação.
        [typeof(DeclaracaoDaProvaPresencial)] = (["TemProvaPresencial", "Municipios"], []),
        [typeof(MunicipioDeProva)] = (["CodigoIbge", "Uf", "Nome", "Posicao"], []),

        // Cadastro de reserva por chamada: as chamadas declaradas pelo processo, no bloco cadastroDeReserva.
        [typeof(CadastroDeReservaPorChamada)] = (["Chamadas"], []),
        [typeof(CadastroDeReservaDaChamada)] = (["Chamada", "PessoasDaReservaPorVaga", "Participacao"], []),
        [typeof(NumeroDaChamada)] = (["Valor"], []),

        // Motivo de decisão escolhido pelo processo, copiado por valor no bloco motivosDeDecisao.
        [typeof(MotivoDeDecisaoDeclarado)] = (
            ["Tipo", "Codigo", "Lugar", "Descricao", "Subclassificacao", "Sentido", "VersaoDeEntrada", "JustificativaDaInclusao"],
            []),
        [typeof(LugarDaDecisao)] = (["FaseCanonicaOrigemId", "EtapaProcessoId"], []),

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

        // Snapshot de tipo de etapa, mesmo padrão de cópia por valor de
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
        [typeof(CopiaDaOfertaDeCurso)] = (
            ["Codigo", "CursoNome", "CursoGrau", "UnidadeOfertanteNome", "Turnos"],
            []),

        // As variantes polimórficas. Elas são persistidas INTEIRAS como JSON, e o codec as
        // lê e escreve em dois `switch` sobre o código da regra — duas listas manuais, num
        // ponto em que o envelope nem sequer carrega discriminador (MAIOR-IDADE e
        // ZERO-EM-AREA serializam como `{}`). Uma propriedade nova aqui, esquecida nos dois
        // switches, é invisível: o envelope não muda, a fixture não muda, o serialização e leitura fica
        // verde — e o campo some no primeiro descarte.
        [typeof(ParametrosDesempateMaiorNotaEtapa)] = (["EtapaId"], []),
        [typeof(ParametrosDesempateMaiorIdade)] = ([], []),
        [typeof(ParametrosDesempateIdoso)] = (["IdadeMinima"], []),
        [typeof(ParametrosDesempatePredicadoFato)] = (["Condicao"], []),
        [typeof(ParametrosDesempateMaiorNotaAreaEnem)] = (["Areas"], []),
        // CondicaoDnf (ADR-0111): átomo tipado { Fato, Operador, Valor }
        // reusado literalmente pela variante acima — mesmo cuidado de propriedade
        // esquecida se aplica aqui.
        [typeof(CondicaoDnf)] = (["Fato", "Operador", "Valor"], []),
        [typeof(ParametrosEliminacaoPorNotaMinimaNaEtapa)] = (["EtapaId", "NotaMinima"], []),
        [typeof(ParametrosEliminacaoPorCorteEmAreaDoEnem)] = (["AreaCodigo", "Minimo"], []),
        [typeof(ParametrosEliminacaoPorZeroEmAreaDoEnem)] = ([], []),
        [typeof(ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem)] = ([], []),
        [typeof(ParametrosEliminacaoPorAusenciaNaEtapa)] = (["EtapaId"], []),

        // Cronograma de fases — o eixo temporal do certame.
        [typeof(ChaveLocal)] = (["Valor"], []),

        [typeof(FaseCronograma)] = (
            [
                "Chave", "Ordem", "NoMesmoMomentoDaAnterior", "FaseCanonicaOrigemId", "Codigo", "OrigemData",
                "AgrupaEtapas", "PermiteComplementacao",
                "ColetaInscricao", "ColetaSolicitacaoIsencao", "Inicio", "Fim",
                "Produtos", "EmiteParecerIndividual",
                "BancasRequeridas", "RegraRecurso", "UnidadeResponsavel", "TiposDeDecisao",
            ],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("TipoFaseNome", "Nome copiado do cadastro, ainda não incluído no documento congelado."),
                ("ProduzResultado", "Derivada de Produtos — a fase produz resultado quando declara ao menos um produto com papel; congelá-la duplicaria a fonte de verdade."),
                ("RegraDeHomologacao", "Ainda não entra no envelope congelado nem no hash."),
            ]),

        // Regra de homologação das inscrições declarada pela fase.
        [typeof(RegraDeHomologacaoDasInscricoes)] = (
            ["ResultadoAtoCodigo", "Alternativas"],
            [
                ("HomologaTodaInscricaoEnviada", "Derivada de Alternativas."),
                ("TemConfirmacaoDoInep", "Derivada de Alternativas."),
            ]),
        [typeof(AlternativaDeCondicoes)] = (["Condicoes"], []),
        [typeof(CondicaoDeHomologacao)] = (
            ["Tipo", "EdicaoDoExame", "MotivoDesfavoravelCodigo", "Enquadramento"],
            []),

        // Cópia da unidade responsável pela fase (ADR-0061), congelada na publicação.
        [typeof(UnidadeResponsavelSnapshot)] = (["OrigemId", "Codigo", "Sigla", "Nome"], []),

        // O produto que a fase publica. O Id ENTRA no envelope, ao contrário do das demais
        // filhas do cronograma: é por ele que o ato publicado resolverá, de volta, a
        // configuração de recurso que lhe corresponde.
        [typeof(ProdutoDaFase)] = (
            ["AtoCodigo", "Papel"],
            [("FaseCronogramaId", "FK interna.")]),

        [typeof(BancaRequerida)] = (
            ["Chave", "Tipo", "PosicaoNoTipo", "CandidatosAtendidos", "ConsequenciaDaAusencia", "RegraDeDeliberacao"],
            [("FaseCronogramaId", "FK interna.")]),

        // A cópia do tipo de banca que a banca da fase e a da etapa guardam, congelada na publicação. Sem Id no
        // envelope: é valor da banca que a carrega, e a origem do tipo identifica de onde a cópia saiu.
        [typeof(TipoDeBancaCopiado)] = (
            ["OrigemId", "Codigo", "Nome", "Papel", "ExigeDuasOuMaisBancas", "EncerraRecursoNaPrimeiraInstancia", "TiposDeDecisaoJulgados"],
            [("AvaliaOMerito", "Derivada do papel — congelá-la duplicaria a fonte de verdade."),
             ("JulgaRecurso", "Derivada do papel — congelá-la duplicaria a fonte de verdade."),
             ("JulgaAPrimeiraInstancia", "Derivada do papel — congelá-la duplicaria a fonte de verdade."),
             ("JulgaASegundaInstancia", "Derivada do papel — congelá-la duplicaria a fonte de verdade.")]),

        // A regra de deliberação da banca, igual na banca da fase e na da etapa. Sem Id no
        // envelope: é valor da banca que a carrega.
        [typeof(RegraDeDeliberacao)] = (
            ["Modo", "Integrantes", "Suplentes", "Quorum", "Criterio", "Desempate", "Encerramento", "SemUnanimidade"],
            [("QuorumExigido", "Derivada de Quorum — sem votação exige um titular; congelá-la duplicaria a fonte de verdade."),
             ("AtoIndividualSemVotacao", "Derivada do modo e da votação declarados; congelá-la duplicaria a fonte de verdade.")]),

        // Os candidatos atendidos de cada banca. Sem Id no envelope: nada aponta para a linha de
        // fora da banca que a carrega.
        [typeof(CategoriaJulgada)] = (
            ["CategoriaDocumentoOrigemId", "Codigo", "Nome"],
            [("BancaRequeridaId", "FK interna.")]),

        // RegraRecursoFase.Parametros NÃO é polimórfico (única variante — CA-02 recusa
        // qualquer regra que não seja RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO), por isso não
        // entra em UnioesDiscriminadas: é alcançado diretamente como ParametrosRegraPrazoRecurso.
        [typeof(RegraRecursoFase)] = (
            ["Regra", "ProdutoAncoraId", "Parametros", "Procedimentos"],
            [("FaseCronogramaId", "FK interna.")]),

        [typeof(ParametrosRegraPrazoRecurso)] = (
            ["PrazoDeInterposicao", "AdmiteInstanciaSuperior", "PrazoDaSegundaInstancia", "EfeitoSuspensivo", "GuardaDaVaga"],
            []),

        [typeof(PrazoDoRecurso)] = (["Valor", "Unidade"], []),

        // Conformidade legal — não pende de ConfiguracaoPublicada (não é reposta no
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
        // serializa, ao contrário de ParametrosCriterioDesempate/ParametrosRegraEliminacao, cuja variante
        // vem do código da regra externa). FadaObrigatorio, oitava variante original, foi
        // descartada (ADR-0114): ConfiguracaoDoFada é global ao
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
