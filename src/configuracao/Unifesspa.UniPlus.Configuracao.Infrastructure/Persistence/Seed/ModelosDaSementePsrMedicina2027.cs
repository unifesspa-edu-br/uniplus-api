namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed.SementePsrMedicina2027;

/// <summary>
/// O conteúdo dos modelos de formulário do edital de Medicina 2027: a inscrição, com e sem prova, que
/// partem das mesmas partes e diferem só pelo atendimento especializado, e a habilitação. É construído
/// pelos records do domínio; <see cref="ModeloFormulario.Criar"/> o confere ao gravar.
/// </summary>
internal static class ModelosDaSementePsrMedicina2027
{
    private const string Enem = "ENEM";
    private const string Escolaridade = "ESCOLARIDADE";
    private const string ReservaDeVagas = "RESERVA_DE_VAGAS";
    private const string Fator = "FATOR_ATENUACAO";
    private const string Atendimento = "ATENDIMENTO_ESPECIALIZADO";
    private const string Conclusao = "CONCLUSAO_ENSINO_MEDIO";
    private const string Familia = "COMPOSICAO_FAMILIAR";

    private const string SolicitaFator = "SOLICITA_FATOR_ATENUACAO";
    private const string CaminhoFator = "CAMINHO_FATOR_ATENUACAO";
    private const string Indigena = "INDIGENA";
    private const string Quilombola = "QUILOMBOLA";
    private const string BaixaRenda = "BAIXA_RENDA";
    private const string ConcorrerRenda = "CONCORRER_RENDA";
    private const string SolicitaAtendimento = "SOLICITA_ATENDIMENTO_ESPECIALIZADO";
    private const string CondicaoAtendimento = "CONDICAO_ATENDIMENTO";

    private const string CorRaca = "COR_RACA";
    private const string CorIndigena = "INDIGENA";

    private const string Lactante = "LACTANTE";
    private const string PessoaIdosa = "PESSOA_IDOSA";
    private const string RemoveVantagem = "REMOVE_VANTAGEM";
    private const string Federal = "FEDERAL";
    private const string Resolvido = "RESOLVIDO";
    private const string ReferenciaDeConcurso = "Norma de concurso público adotada como referência; o edital exige o documento na inscrição.";

    /// <summary>As cores ou raças que, com a autodeclaração, dão acesso à cota de pretos, pardos e indígenas.</summary>
    private static IReadOnlyList<string> CoresPretaOuParda { get; } = ["PRETA", "PARDA"];

    /// <summary>A opção pelas cotas da lei, que toda pergunta delas exige.</summary>
    private static CondicaoDnf Optou => Condicao(OpcaoPelasCotasDaLei.Fato, Operador.Igual, true);

    /// <summary>As formas de conclusão de quem cursou o ensino médio, e não o concluiu por certificação.</summary>
    private static IReadOnlyList<string> ConclusoesPorCurso { get; } = [OrigemEscolar.Regular, OrigemEscolar.Eja];

    /// <summary>
    /// O conteúdo da inscrição. O atendimento especializado serve à realização da prova (UNI-REQ-0012):
    /// só o processo com prova pergunta por ele e exige os documentos que o comprovam.
    /// </summary>
    internal static ConteudoDoModelo Inscricao(bool comProva) => new(
        "Inscrição no Processo Seletivo Regular Unificado",
        EtapasDaInscricao(comProva),
        CamposDaInscricao(comProva),
        [Termo(Termos[0], 1), Termo(Termos[1], 2)],
        [],
        [],
        comProva ? DocumentosDoAtendimentoEspecializado() : []);

    /// <summary>
    /// O conteúdo da habilitação. Pressuposto aceita só fato Declarado, e MODALIDADE_CONVOCACAO
    /// (UNI-REQ-0147) é Derivado: CONCORRER_RENDA é o superconjunto declarado, porque todo convocado em
    /// cota de renda o respondeu antes.
    /// </summary>
    internal static ConteudoDoModelo Habilitacao() => new(
        "Habilitação no Processo Seletivo Regular Unificado de Medicina 2027",
        EtapasDaHabilitacao(),
        CamposDaHabilitacao(),
        [Termo(Termos[2], 1, exibicao: Se(ConcorrerRenda, Operador.Igual, true))],
        [OrigemEscolar.FatoFormaDeConclusao, ConcorrerRenda],
        [GrupoDaComposicaoFamiliar()]);

    private static ItemDoModelo[] CamposDaInscricao(bool comProva) =>
    [
        .. CamposDoEnemEDaEscolaridade(),
        .. CamposDoPedidoDoFator(),
        .. CamposDoMunicipioDoFator(),
        .. CamposDeDeficiencia(),
        .. CamposDoPertencimento(),
        .. CamposDeConcorrenciaPorCota(),
        .. CamposDeRenda(),
        .. comProva ? CamposDoAtendimento() : Array.Empty<ItemDoModelo>(),
    ];

    private static EtapaDoModelo[] EtapasDaInscricao(bool comProva) =>
    [
        Secao(Enem, 1, "ENEM", "A edição do ENEM cujas notas você vai usar neste processo seletivo."),
        Secao(Escolaridade, 2, "Ensino médio", "Como você concluiu o ensino médio e onde o cursou."),
        Secao(Fator, 3, "Fator de Atenuação das Desigualdades de Acesso", "Pedido do Fator de Atenuação das Desigualdades de Acesso previsto no edital."),
        Secao(ReservaDeVagas, 4, "Reserva de vagas", "Condições que permitem concorrer às vagas reservadas da Lei nº 12.711/2012 e às ações afirmativas do edital."),
        .. comProva ? [SecaoDoAtendimento()] : Array.Empty<EtapaDoModelo>(),
        Bloco("MODALIDADES", 6, BlocoSistema.ModalidadesCalculadas, "Modalidades de concorrência"),
        Bloco("DOCUMENTOS", 7, BlocoSistema.ComprovacaoDocumental, "Documentos"),
        Bloco("REVISAO", 8, BlocoSistema.RevisaoEAceite, "Revisão e aceite"),
    ];

    private static ItemDoModelo[] CamposDoEnemEDaEscolaridade() =>
    [
        Campo(EdicaoDoEnem.Fato, 100, Enem, "Qual edição do ENEM você vai usar?", TipoRenderizacao.SelecaoUnica,
            "Só aparecem as edições que o edital aceita."),
        Campo(OrigemEscolar.FatoFormaDeConclusao, 101, Escolaridade, "Como você concluiu o ensino médio?", TipoRenderizacao.SelecaoUnica, null),
        Campo(OrigemEscolar.FatoOndeCursou, 102, Escolaridade, "Onde você cursou o ensino médio?", TipoRenderizacao.SelecaoUnica,
            "A resposta define se você pode concorrer às vagas para quem cursou o ensino médio em escola pública.")
            .ComRestricoes([OpcoesDeOndeCursou()]),
    ];

    /// <summary>
    /// Quem cursou o ensino médio regular ou na EJA pede pelo município em que o cursou; quem concluiu
    /// por certificação escolhe entre esse caminho e o da residência no período.
    /// </summary>
    private static ItemDoModelo[] CamposDoPedidoDoFator() =>
    [
        Campo(SolicitaFator, 103, Fator, "Deseja solicitar o Fator de Atenuação das Desigualdades de Acesso?", TipoRenderizacao.Booleano,
            "Para quem cursou pelo menos um ano do ensino médio num dos municípios da área que o edital lista ou, se concluiu por certificação, residiu num deles no período do edital. A comprovação é pedida na habilitação."),
        Campo(CaminhoFator, 104, Fator, "Por qual vínculo com a área você solicita o Fator?", TipoRenderizacao.SelecaoUnica,
            "Como você concluiu o ensino médio por certificação, pode usar o ensino médio cursado na área ou a residência na área no período do edital.",
            exibicao: SeAlguma([Condicao(SolicitaFator, Operador.Igual, true), Condicao(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, OrigemEscolar.Certificacoes)])),
    ];

    private static ItemDoModelo[] CamposDoMunicipioDoFator() =>
    [
        Campo("MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", 105, Fator, "Em qual município da área de influência você cursou o ensino médio? Se foi em mais de um, informe aquele em que estudou mais tempo.", TipoRenderizacao.SelecaoUnica,
            "Vale o município em que você cursou pelo menos um ano do ensino médio, em escola pública ou privada. Só aparecem os municípios que o edital lista.",
            exibicao: SeAlguma(
                [Condicao(SolicitaFator, Operador.Igual, true), Condicao(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, ConclusoesPorCurso)],
                [Condicao(SolicitaFator, Operador.Igual, true), Condicao(CaminhoFator, Operador.Igual, CaminhoPeloEnsinoMedio)])),
        Campo("MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO", 106, Fator, "Em qual município da área de abrangência você residiu no período? Se foi em mais de um, informe aquele em que residiu mais tempo.", TipoRenderizacao.SelecaoUnica,
            "Vale ter residido pelo menos um ano na área entre outubro de 2024 e outubro de 2026, conforme o edital.",
            exibicao: SeAlguma([Condicao(SolicitaFator, Operador.Igual, true), Condicao(CaminhoFator, Operador.Igual, CaminhoPelaResidencia)])),
    ];

    private static ItemDoModelo[] CamposDeDeficiencia() =>
    [
        Campo("PCD", 107, ReservaDeVagas, "Você é pessoa com deficiência?", TipoRenderizacao.Booleano,
            "A deficiência é comprovada por laudo, conforme o edital."),
        Campo("TIPO_DEFICIENCIA", 108, ReservaDeVagas, "Tipo de deficiência", TipoRenderizacao.SelecaoUnica, null,
            exibicao: Se("PCD", Operador.Igual, true)),
        Campo("CONCORRER_PCD", 109, ReservaDeVagas, "Deseja concorrer às vagas para pessoas com deficiência?", TipoRenderizacao.Booleano, null,
            exibicao: Se("PCD", Operador.Igual, true)),
        Campo(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 110, ReservaDeVagas, "Deseja concorrer às vagas para quem cursou o ensino médio em escola pública?", TipoRenderizacao.Booleano, null,
            exibicao: Se(OrigemEscolar.FatoEgresso, Operador.Igual, true)),
    ];

    /// <summary>
    /// As perguntas das cotas da lei só se fazem a quem optou por elas (UNI-REQ-0073), e a elegibilidade
    /// vem antes da opção por concorrer: o pertencimento indígena, de quem se declarou indígena na cor ou
    /// raça, e a autodeclaração quilombola, de quem tem qualquer outra cor ou raça, vêm antes da PPI e da
    /// cota de quilombolas.
    /// </summary>
    private static ItemDoModelo[] CamposDoPertencimento() =>
    [
        Campo(Indigena, 111, ReservaDeVagas, "Você pertence a um povo indígena?", TipoRenderizacao.Booleano,
            "O pertencimento é comprovado na habilitação, conforme o edital.",
            exibicao: SeAlguma([Optou, Condicao(CorRaca, Operador.Igual, CorIndigena)])),
        Campo(Quilombola, 112, ReservaDeVagas, "Você se autodeclara quilombola?", TipoRenderizacao.Booleano,
            "Quem é quilombola pode ter qualquer cor ou raça, exceto a raça indígena. As vagas para pretos e pardos são para quem informou essa cor ou raça em Dados do candidato.",
            exibicao: SeAlguma([Optou, Condicao(CorRaca, Operador.Diferente, CorIndigena)])),
    ];

    /// <summary>Concorre à PPI quem se declarou preto ou pardo, ou indígena com o pertencimento.</summary>
    private static ItemDoModelo[] CamposDeConcorrenciaPorCota() =>
    [
        Campo("CONCORRER_PPI", 113, ReservaDeVagas, "Deseja concorrer às vagas para pretos, pardos e indígenas?", TipoRenderizacao.Booleano,
            "Para quem se autodeclara preto, pardo ou indígena.",
            exibicao: SeAlguma(
                [Optou, Condicao(CorRaca, Operador.Em, CoresPretaOuParda)],
                [Optou, Condicao(CorRaca, Operador.Igual, CorIndigena), Condicao(Indigena, Operador.Igual, true)])),
        Campo("CONCORRER_Q", 114, ReservaDeVagas, "Deseja concorrer às vagas para quilombolas?", TipoRenderizacao.Booleano, null,
            exibicao: SeAlguma([Optou, Condicao(Quilombola, Operador.Igual, true)])),
    ];

    private static ItemDoModelo[] CamposDeRenda() =>
    [
        Campo(BaixaRenda, 115, ReservaDeVagas, "A renda da sua família é de até um salário mínimo por pessoa?", TipoRenderizacao.Booleano,
            "Some a renda bruta mensal de todos da família e divida pelo número de pessoas. A renda é comprovada na habilitação.",
            exibicao: SeAlguma([Optou])),
        Campo(ConcorrerRenda, 116, ReservaDeVagas, "Deseja concorrer às vagas com critério de renda?", TipoRenderizacao.Booleano, null,
            exibicao: SeAlguma([Optou, Condicao(BaixaRenda, Operador.Igual, true)])),
    ];

    private static EtapaDoModelo SecaoDoAtendimento() =>
        Secao(Atendimento, 5, "Atendimento especializado", "Pedido de atendimento especializado nas etapas presenciais do processo seletivo.");

    /// <summary>
    /// Atendimento especializado não é só de quem tem deficiência (UNI-REQ-0026): quem pede escolhe a
    /// condição e o recurso entre os que o processo oferta; a condição fora da lista é descrita.
    /// </summary>
    private static ItemDoModelo[] CamposDoAtendimento() =>
    [
        Campo(SolicitaAtendimento, 117, Atendimento, "Você precisa de atendimento especializado em alguma etapa presencial do processo?", TipoRenderizacao.Booleano,
            "Por exemplo, por deficiência, gestação, amamentação, idade ou transtorno de aprendizagem."),
        Campo(CondicaoAtendimento, 118, Atendimento, "Em quais condições você se enquadra?", TipoRenderizacao.SelecaoMultipla,
            "Marque todas as que se aplicam a você.",
            exibicao: Se(SolicitaAtendimento, Operador.Igual, true)),
        Campo("DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", 119, Atendimento, "Descreva a sua condição específica", TipoRenderizacao.Texto,
            "Diga qual é a condição, em poucas palavras.",
            exibicao: Se(CondicaoAtendimento, Operador.Igual, CondicaoForaDaLista))
            .ComFormato("LIVRE").ComRestricoes([new TamanhoTexto(null, LimiteDaDescricaoDaOutraCondicao)]),
        Campo("RECURSO_ACESSIBILIDADE", 120, Atendimento, "De quais recursos você vai precisar nas etapas presenciais?", TipoRenderizacao.SelecaoMultipla,
            "Só aparecem os recursos que o processo oferece. Marque todos de que você precisa.",
            exibicao: Se(SolicitaAtendimento, Operador.Igual, true)),
    ];

    /// <summary>
    /// O conjunto padrão de documentos da inscrição (UNI-REQ-0026, UNI-REQ-0170): o CEPS só concede o
    /// atendimento especializado a quem comprova a condição, e o documento comprova a condição, não o
    /// recurso. Toda condição que o processo oferece pede o laudo médico, menos a lactante, que apresenta
    /// a certidão de nascimento da criança, e a pessoa idosa, cuja idade vem do cadastro. Para quem
    /// solicita atendimento o documento é obrigatório: sem ele, a inscrição não é enviada; o documento
    /// enviado e indeferido na análise tira o atendimento e mantém a inscrição. As normas são de concurso
    /// público, adotadas como referência; exigir o documento já na inscrição é regra do edital.
    /// </summary>
    private static DocumentoExigidoDoModelo[] DocumentosDoAtendimentoEspecializado() =>
        [LaudoMedicoDeCadaCondicao(), CertidaoDeNascimentoDaLactante()];

    /// <summary>
    /// Um laudo por condição: o CEPS defere ou indefere cada comprovação por si, e quem declara mais de
    /// uma condição apresenta o laudo de cada uma. As condições vêm da oferta do processo, e a que o
    /// administrador cadastrar depois também pede o laudo.
    /// </summary>
    private static DocumentoExigidoDoModelo LaudoMedicoDeCadaCondicao() =>
        new("INSCRICAO", "LAUDO_MEDICO", Obrigatorio: true, SeAlguma([Condicao(SolicitaAtendimento, Operador.Igual, true)]), RemoveVantagem,
            BasesLegaisDoLaudo(), new ExigenciaPorOpcao(CondicaoAtendimento, [Lactante, PessoaIdosa]));

    private static DocumentoExigidoDoModelo CertidaoDeNascimentoDaLactante() =>
        new("INSCRICAO", "CERTIDAO_NASCIMENTO", Obrigatorio: true,
            SeAlguma([Condicao(SolicitaAtendimento, Operador.Igual, true), Condicao(CondicaoAtendimento, Operador.Igual, Lactante)]),
            RemoveVantagem, [new("Lei nº 13.872/2019, art. 2º, § 2º", Federal, Resolvido, ReferenciaDeConcurso)]);

    private static BaseLegalDoDocumentoDoModelo[] BasesLegaisDoLaudo() =>
    [
        new("Lei nº 13.146/2015 (Lei Brasileira de Inclusão)", Federal, Resolvido, null),
        new("Decreto nº 9.508/2018, na redação do Decreto nº 9.546/2018", Federal, Resolvido, ReferenciaDeConcurso),
        new("Decreto nº 3.298/1999", Federal, Resolvido, ReferenciaDeConcurso),
    ];

    private static EtapaDoModelo[] EtapasDaHabilitacao() =>
    [
        Secao(Conclusao, 1, "Conclusão do ensino médio e vínculos", "Situação da conclusão do ensino médio e vínculos com outras instituições."),
        Secao(Familia, 2, "Composição familiar", "Destina-se a quem foi convocado em vaga com critério de renda: informe as pessoas que compõem o seu grupo familiar, incluindo você."),
        Bloco("DOCUMENTOS", 3, BlocoSistema.ComprovacaoDocumental, "Documentos"),
        Bloco("REVISAO", 4, BlocoSistema.RevisaoEAceite, "Revisão e aceite"),
    ];

    private static ItemDoModelo[] CamposDaHabilitacao() =>
    [
        Campo("CERTIFICADO_EM_EMITIDO", 1, Conclusao, "Seu certificado de conclusão do ensino médio já foi emitido?", TipoRenderizacao.Booleano,
            "Se ainda não, apresente a declaração de conclusão e o termo de responsabilidade.",
            exibicao: Se(OrigemEscolar.FatoFormaDeConclusao, Operador.Igual, OrigemEscolar.Regular)),
        Campo("HABILITACAO_POR_PROCURADOR", 2, Conclusao, "Outra pessoa fará a habilitação em seu nome, por procuração?", TipoRenderizacao.Booleano,
            "O procurador apresenta a procuração e o próprio documento de identificação."),
        Campo("VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", 3, Conclusao, "Você tem matrícula ativa em outra instituição pública de ensino superior ou bolsa do ProUni?", TipoRenderizacao.Booleano, null),
    ];

    private static GrupoDoModelo GrupoDaComposicaoFamiliar() =>
        new(Familia, 4, Familia, "Membros da composição familiar", Minimo: 1, Maximo: null, Exibicao: null,
            Obrigatoriedade.Sempre,
            [
                Campo(CandidatoComoMembro.FatoParentesco, 1, null, "Parentesco com você", TipoRenderizacao.SelecaoUnica, null),
                Campo("CATEGORIA_RENDA", 2, null, "Fontes de renda desta pessoa", TipoRenderizacao.SelecaoMultipla,
                    "Marque todas. Elas definem os documentos de renda a apresentar."),
                Campo("SOB_GUARDA", 3, null, "É menor de idade sob guarda?", TipoRenderizacao.Booleano,
                    "Sob a sua guarda ou a de alguém da família."),
            ],
            IncluiCandidato: true);

    private static EtapaDoModelo Secao(string codigo, int ordem, string titulo, string descricao) =>
        new(codigo, ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, titulo, descricao, null, null);

    private static EtapaDoModelo Bloco(string codigo, int ordem, BlocoSistema bloco, string titulo) =>
        new(codigo, ordem, TipoEtapaFormulario.Bloco, bloco, titulo, null, null, null);

    private static ItemDoModelo Campo(
        string fato, int ordem, string? etapa, string rotulo, TipoRenderizacao tipo, string? ajuda, PredicadoDnf? exibicao = null) =>
        new(fato, ordem, etapa, rotulo, tipo, null, ajuda, Obrigatoriedade.Sempre, exibicao, [], PedirConfirmacao: false);

    private static ItemDoModelo ComFormato(this ItemDoModelo item, string formato) => item with { Formato = formato };

    private static ItemDoModelo ComRestricoes(this ItemDoModelo item, IReadOnlyList<RestricaoValor> restricoes) =>
        item with { Restricoes = restricoes };

    /// <summary>
    /// Onde cursou oferece "Concluí por certificação" só a quem respondeu que concluiu por
    /// certificação; as demais opções, a todos.
    /// </summary>
    private static OpcoesPermitidas OpcoesDeOndeCursou() =>
        new([
            new OpcoesCondicionadas(null, OrigemEscolar.OndeCursouSemCertificacao),
            new OpcoesCondicionadas(
                Se(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, OrigemEscolar.Certificacoes),
                [OrigemEscolar.ConcluiuPorCertificacao]),
        ]);

    private static TermoDoModelo Termo(TermoDaSemente termo, int ordem, PredicadoDnf? exibicao = null) =>
        new(termo.Codigo, ordem, termo.TermoId, termo.VersaoId, exibicao, Obrigatoriedade.Sempre);

    /// <summary>A condição única "fato operador valor", como predicado de uma cláusula só.</summary>
    private static PredicadoDnf Se<T>(string fato, Operador operador, T valor) => SeAlguma([Condicao(fato, operador, valor)]);

    /// <summary>O predicado que vale quando todas as condições de alguma das cláusulas valem.</summary>
    private static PredicadoDnf SeAlguma(params CondicaoDnf[][] clausulas) =>
        Exigir(
            PredicadoDnf.CriarDeCondicoesAgrupadas([.. clausulas.SelectMany(static (condicoes, i) => condicoes.Select(c => (i + 1, c)))]),
            $"predicado sobre {string.Join(", ", clausulas.SelectMany(static c => c).Select(static c => c.Fato).Distinct(StringComparer.Ordinal))}");

    private static CondicaoDnf Condicao<T>(string fato, Operador operador, T valor) =>
        Exigir(CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)), $"condição sobre {fato}");

    private static T Exigir<T>(Result<T> resultado, string oQue) => SqlDaSemente.Exigir(resultado, NomeDaSemente, oQue);
}
