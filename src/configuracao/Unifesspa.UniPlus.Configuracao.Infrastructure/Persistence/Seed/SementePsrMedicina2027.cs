namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;
using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed.SqlDaSemente;

/// <summary>
/// A configuração do Processo Seletivo Regular Unificado de Medicina 2027 que existe ao subir o
/// sistema: o tipo de processo, os termos de consentimento da inscrição e os fatos do candidato que
/// os formulários dele coletam e que o catálogo de sistema não tem.
/// </summary>
/// <remarks>
/// <para>
/// É dado administrado — depois de semeado, edita-se pelas telas de administração —, gravado direto
/// no banco por decisão do líder técnico (uniplus-api#1802), para o processo real estar disponível
/// sem cadastro manual em cada ambiente. Cada linha é construída pelas factories do domínio: o que o
/// domínio recusaria derruba a migration em vez de gravar dado que a API não aceita.
/// </para>
/// <para>
/// Os identificadores são fixos, para a inserção ser idempotente e as chaves estrangeiras baterem
/// em todo ambiente; o prefixo é próprio, distinto do <c>fa70…</c> dos fatos de sistema. O
/// conteúdo que o PO ainda não confirmou — valores dos domínios e textos dos termos — é proposta
/// listada na issue, e a confirmação chega pela tela.
/// </para>
/// </remarks>
public static class SementePsrMedicina2027
{
    /// <summary>O tipo de processo do edital de Medicina.</summary>
    public const string TipoProcessoCodigo = "PSR";

    /// <summary>O identificador fixo do tipo de processo semeado.</summary>
    public static Guid TipoProcessoId => Id(TabelaTipoProcesso, 1);

    private const string PontoInscricao = "INSCRICAO";
    private const string PontoHabilitacao = "HABILITACAO";

    /// <summary>Quem consta como autor da promoção dos termos semeados, que não passaram por pessoa.</summary>
    private const string AutorDaSemente = "semente-psr-medicina-2027";

    /// <summary>Como as recusas do domínio nomeiam esta semente.</summary>
    private const string NomeDaSemente = "do PSR Medicina 2027";

    private const HipoteseLegalTratamento HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    private static readonly DateTimeOffset Instante = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    private const string FinalidadeDoFator =
        "Concessão do Fator de Atenuação das Desigualdades de Acesso previsto no edital do processo seletivo.";
    private const string FinalidadeDoAtendimento = FatoCandidatoSeed.FinalidadeAtendimento;
    private const string FinalidadeDaEscolaridade = FatoCandidatoSeed.FinalidadeReservaDeVagas;
    private const string FinalidadeDaHabilitacao = FatoCandidatoSeed.FinalidadeRequisitos;
    private const string FinalidadeDaFamilia = FatoCandidatoSeed.FinalidadeComposicaoFamiliar;

    /// <summary>
    /// O identificador fixo da linha <paramref name="n"/> da tabela <paramref name="tabela"/>; cada
    /// tabela tem o seu segmento, para uma identidade nunca se repetir entre tabelas.
    /// </summary>
    private static Guid Id(int tabela, int n) =>
        Guid.Parse($"5e3d{tabela:D4}-0000-7000-8000-{n:D12}", CultureInfo.InvariantCulture);

    private const int TabelaTipoProcesso = 1;
    private const int TabelaTermo = 2;
    private const int TabelaVersaoDoTermo = 3;
    private const int TabelaFato = 4;
    private const int TabelaValor = 5;
    private const int TabelaModelo = 6;

    /// <summary>O modelo do formulário de inscrição de Medicina.</summary>
    public const string ModeloDeInscricao = "PSR_MEDICINA_2027_INSCRICAO";

    /// <summary>O modelo do formulário de habilitação de Medicina.</summary>
    public const string ModeloDeHabilitacao = "PSR_MEDICINA_2027_HABILITACAO";

    /// <summary>
    /// O código da condição de atendimento "outra condição específica" do vocabulário do INEP, que o
    /// processo que aplica o modelo de inscrição oferta e cuja escolha pede a descrição da condição.
    /// </summary>
    public const string CondicaoForaDaLista = "OUTRA_CONDICAO";

    /// <summary>
    /// O caminho do Fator de Atenuação de quem concluiu o ensino médio por certificação: o município
    /// em que cursou pelo menos um ano do ensino médio, ou o município em que residiu no período que o
    /// edital fixa.
    /// </summary>
    public const string CaminhoPeloEnsinoMedio = "ENSINO_MEDIO";

    /// <inheritdoc cref="CaminhoPeloEnsinoMedio"/>
    public const string CaminhoPelaResidencia = "RESIDENCIA";

    /// <summary>Um termo da inscrição: o código pelo qual o modelo o cita, o nome, o texto e a base legal.</summary>
    public sealed record TermoDaSemente(int N, string Codigo, string Nome, string Texto, string BaseLegal)
    {
        /// <summary>O identificador do termo.</summary>
        public Guid TermoId => Id(TabelaTermo, N);

        /// <summary>O identificador da versão promovida, que o modelo cita.</summary>
        public Guid VersaoId => Id(TabelaVersaoDoTermo, N);
    }

    /// <summary>Os termos que a inscrição de Medicina exige (edital, item 1.10). Os textos são proposta.</summary>
    public static IReadOnlyList<TermoDaSemente> Termos { get; } =
    [
        new(1, "USO_DAS_NOTAS_DO_ENEM", "Uso das notas do ENEM e dos dados da inscrição",
            "Autorizo a Unifesspa a utilizar meus resultados no Exame Nacional do Ensino Médio (ENEM), nas edições admitidas pelo edital, e os dados que informo nesta inscrição, exclusivamente para as finalidades deste processo seletivo.",
            "Lei nº 13.709/2018 (LGPD), art. 7º, inciso I; edital do processo seletivo."),
        new(2, "NORMAS_DO_EDITAL", "Concordância com as normas do edital",
            "Declaro que li e concordo com as normas do edital deste processo seletivo e que as informações que presto são verdadeiras, sob as penas da lei.",
            "Edital do processo seletivo."),
        new(3, "CONSULTA_AO_BANCO_CENTRAL", "Autorização de consulta ao Banco Central",
            "Autorizo a Unifesspa a consultar, junto ao Banco Central do Brasil, as informações financeiras em meu nome e no dos membros da minha composição familiar, para a verificação da renda que declaro ao concorrer às vagas reservadas por critério de renda.",
            "Lei nº 12.711/2012; Lei nº 13.709/2018 (LGPD), art. 7º, inciso I; edital do processo seletivo."),
    ];

    /// <summary>
    /// O que constrói um fato pelas factories do domínio, com o identificador fixo e os valores dele.
    /// O agregado recebe o catálogo dos fatos já construídos, porque confere o fato de membro.
    /// </summary>
    internal sealed record FatoDaSemente(
        int N,
        Func<CatalogoDeFatos, Result<FatoCandidato>> Construir,
        IReadOnlyList<(string Codigo, string Descricao)> Valores);

    private static (string, string)[] Sem() => [];

    /// <summary>Os fatos que os formulários de Medicina coletam e que o catálogo de sistema não tem.</summary>
    private static IReadOnlyList<FatoDaSemente> Fatos { get; } =
    [
        // Inscrição: o Fator de Atenuação das Desigualdades de Acesso, pedido pelo candidato que cursou
        // pelo menos um ano do ensino médio num município da área ou, se concluiu por certificação,
        // residiu num deles no período que o edital fixa (itens 1.5, 1.6 e 13). O endereço atual não
        // conta. Quem recebe o acréscimo é a configuração do processo que decide, citando o pedido.
        new(11, _ => FatoCandidato.CriarDoAdministrador(
                "SOLICITA_FATOR_ATENUACAO", "Solicita o Fator de Atenuação das Desigualdades de Acesso", "Se o candidato pede o Fator de Atenuação das Desigualdades de Acesso previsto no edital.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(13, _ => FatoCandidato.CriarDoAdministrador(
                "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO", "Município de residência para o Fator de Atenuação", "O município da área de abrangência do edital em que o candidato residiu no período que o edital fixa, entre os que o edital lista.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.MunicipiosBonus, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(16, _ => FatoCandidato.CriarDoAdministrador(
                "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "Município do ensino médio para o Fator de Atenuação", "O município da área de influência do edital em que o candidato cursou pelo menos um ano do ensino médio, entre os que o edital lista.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.MunicipiosBonus, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(17, _ => FatoCandidato.CriarDoAdministrador(
                "CAMINHO_FATOR_ATENUACAO", "Caminho do Fator de Atenuação", "Por qual vínculo com a área do edital pede o Fator de Atenuação quem concluiu o ensino médio por certificação: o ensino médio cursado ou a residência no período.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.Global, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDoFator, HipoteseLegal),
            [(CaminhoPeloEnsinoMedio, "Cursei pelo menos um ano do ensino médio na área"),
             (CaminhoPelaResidencia, "Residi na área no período do edital")]),

        // Inscrição: o pedido de atendimento especializado nas etapas presenciais, que abre as
        // perguntas da condição e do recurso, e a descrição da condição que não consta da lista.
        // A saúde que a resposta pode revelar faz dos dois um dado sensível.
        new(14, _ => FatoCandidato.CriarDoAdministrador(
                "SOLICITA_ATENDIMENTO_ESPECIALIZADO", "Solicita atendimento especializado", "Se o candidato pede atendimento especializado em alguma etapa presencial do processo seletivo.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Sensivel, FinalidadeDoAtendimento, HipoteseLegal),
            Sem()),
        new(15, _ => FatoCandidato.CriarDoAdministrador(
                "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "Descrição da outra condição de atendimento", "A condição específica, que não consta da lista, pela qual o candidato pede atendimento especializado.", DominioFato.Texto,
                CardinalidadeFato.Escalar, null, FormatoTexto.Livre, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Sensivel, FinalidadeDoAtendimento, HipoteseLegal),
            Sem()),

        // Habilitação: o que o edital pede de quem é convocado (itens 9.16, 9.21 e 9.24).
        new(6, _ => FatoCandidato.CriarDoAdministrador(
                "CERTIFICADO_EM_EMITIDO", "Certificado de conclusão do ensino médio já emitido", "Se o certificado de conclusão do ensino médio já foi emitido. Sem ele, a habilitação pede a declaração de conclusão e o termo de responsabilidade.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),
        new(7, _ => FatoCandidato.CriarDoAdministrador(
                "HABILITACAO_POR_PROCURADOR", "Habilitação feita por procurador", "Se a habilitação é feita por procurador, que apresenta a procuração e o próprio documento de identificação.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),
        new(8, _ => FatoCandidato.CriarDoAdministrador(
                "VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", "Vínculo com outra instituição pública de ensino superior ou com o ProUni", "Se o candidato tem matrícula ativa em outra instituição pública de ensino superior ou é bolsista do ProUni.",
                DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),

        // Habilitação: a categoria de renda de cada membro da composição familiar (edital, item
        // 9.39.2) e as categorias presentes na família, que os documentos de renda citam.
        new(9, _ => FatoCandidato.CriarDoAdministrador(
                "CATEGORIA_RENDA", "Categoria de renda", "As fontes de renda do membro da composição familiar, que definem os documentos de renda que ele apresenta.", DominioFato.Categorico, CardinalidadeFato.Multivalorado,
                FonteValoresFato.Global, null, PontoHabilitacao, EscopoFato.MembroGrupo,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDaFamilia, HipoteseLegal),
            [("ASSALARIADO", "Assalariado"), ("ATIVIDADE_RURAL", "Atividade rural"),
             ("APOSENTADO_OU_PENSIONISTA", "Aposentado ou pensionista"),
             ("AUTONOMO_OU_PROFISSIONAL_LIBERAL", "Autônomo ou profissional liberal"),
             ("ALUGUEL_OU_ARRENDAMENTO", "Renda de aluguel ou arrendamento"), ("SEM_RENDA", "Sem renda"),
             ("DESEMPREGADO", "Desempregado")]),
        new(10, catalogo => FatoCandidato.CriarAgregadoDoAdministrador(
                "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda presentes na família", "As categorias de renda que aparecem entre os membros da composição familiar, calculadas a partir das respostas de cada membro.", "CATEGORIA_RENDA", catalogo,
                PontoHabilitacao, ClassificacaoProtecaoDado.Pessoal, FinalidadeDaFamilia, HipoteseLegal),
            Sem()),
    ];

    /// <summary>
    /// Os comandos SQL que gravam o tipo de processo, os termos e os fatos, na ordem das chaves
    /// estrangeiras. Cada inserção ignora a linha que já existe: reaplicar não duplica nem altera.
    /// </summary>
    public static IReadOnlyList<string> ComandosDoTipoTermosEFatos()
    {
        List<string> comandos = [ComandoDoTipoProcesso()];
        comandos.AddRange(Termos.SelectMany(ComandosDoTermo));
        comandos.AddRange(ComandosDosFatos(Fatos));
        return comandos;
    }

    /// <summary>
    /// Os comandos que gravam os fatos e os valores deles, construídos pelas factories do domínio:
    /// o fato ou o valor que o domínio recusa lança, em vez de virar linha que a API não aceita.
    /// </summary>
    internal static IReadOnlyList<string> ComandosDosFatos(IReadOnlyList<FatoDaSemente> fatos)
    {
        List<string> comandos = [];
        List<FatoCandidato> construidos = [];
        foreach (FatoDaSemente semente in fatos)
        {
            FatoCandidato fato = Exigir(semente.Construir(new CatalogoDeFatos(construidos, [])), $"fato {semente.N}");
            for (int ordem = 0; ordem < semente.Valores.Count; ordem++)
            {
                (string codigo, string descricao) = semente.Valores[ordem];
                Exigir(fato.AdicionarValorDominio(codigo, descricao, ordem, ativo: true), $"valor {codigo} de {fato.Codigo}");
            }

            construidos.Add(fato);
            comandos.Add(ComandoDoFato(Id(TabelaFato, semente.N), fato));
            comandos.AddRange(fato.ValoresDominioDeclarados.Select((valor, i) =>
                ComandoDoValor(Id(TabelaValor, (semente.N * 100) + i + 1), Id(TabelaFato, semente.N), valor)));
        }

        return comandos;
    }

    /// <summary>
    /// Os comandos que gravam os dois modelos, ativos. O conteúdo é construído pelos records do
    /// domínio e conferido por <see cref="ModeloFormulario.Criar"/> — estrutura, blocos por finalidade
    /// e grafo das regras —, e serializado pelo mesmo conversor que a aplicação usa para ler.
    /// </summary>
    /// <remarks>
    /// O modelo de inscrição é gravado sem a seção dos dados básicos: a API a repõe ao editar o modelo
    /// e ao aplicá-lo ao processo. Por isso as regras do modelo não citam os dados básicos.
    /// </remarks>
    public static IReadOnlyList<string> ComandosDosModelos() =>
    [
        ComandoDoModelo(1, ModeloDeInscricao, "Inscrição — Medicina 2027",
            "Formulário de inscrição do Processo Seletivo Regular Unificado de Medicina 2027.",
            FinalidadeFormulario.Inscricao, ConteudoDaInscricao(), DerivacoesDaInscricao),
        ComandoDoModelo(2, ModeloDeHabilitacao, "Habilitação — Medicina 2027",
            "Formulário de habilitação dos convocados no Processo Seletivo Regular Unificado de Medicina 2027.",
            FinalidadeFormulario.Habilitacao, ConteudoDaHabilitacao(),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)),
    ];

    /// <summary>
    /// Os comandos que tiram do banco a forma de conclusão do ensino médio que a semente gravava como
    /// fato do administrador: ela passou a fato de sistema, com o mesmo código, ao lado de onde o
    /// candidato cursou o ensino médio.
    /// </summary>
    public static IReadOnlyList<string> ComandosQueRetiramAFormaDeConclusaoDaSemente() =>
    [
        $"DELETE FROM configuracao.fato_valor_dominio WHERE fato_candidato_id = {Uuid(Id(TabelaFato, 5))};",
        $"DELETE FROM configuracao.rol_de_fatos_candidato WHERE id = {Uuid(Id(TabelaFato, 5))};",
    ];

    /// <summary>
    /// Os comandos que tiram do banco os fatos do pedido do acréscimo que a semente gravava antes: dois
    /// pedidos e dois municípios viraram um pedido e o município de residência.
    /// </summary>
    public static IReadOnlyList<string> ComandosQueRetiramOsFatosAntigosDoFator() => ComandosQueRetiramOsFatos(3, 4, 11, 12);

    /// <summary>
    /// Os comandos que tiram do banco o município de residência do Fator, para ele ser gravado de novo
    /// com a residência no período do edital no lugar da residência atual.
    /// </summary>
    public static IReadOnlyList<string> ComandosQueRetiramOMunicipioDeResidenciaDoFator() => ComandosQueRetiramOsFatos(13);

    private static string[] ComandosQueRetiramOsFatos(params int[] fatos) =>
    [
        .. fatos.SelectMany(static n => new[]
        {
            $"DELETE FROM configuracao.fato_valor_dominio WHERE fato_candidato_id = {Uuid(Id(TabelaFato, n))};",
            $"DELETE FROM configuracao.rol_de_fatos_candidato WHERE id = {Uuid(Id(TabelaFato, n))};",
        }),
    ];

    /// <summary>Os comandos que apagam os modelos semeados, para o <c>Down</c> da migration.</summary>
    public static IReadOnlyList<string> ComandosDeRemocaoDosModelos() =>
    [
        $"DELETE FROM configuracao.modelos_formulario WHERE id::text LIKE '5e3d{TabelaModelo:D4}-%';",
    ];

    /// <summary>A inscrição cita o egresso de escola pública, que deriva da origem escolar.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> DerivacoesDaInscricao { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [OrigemEscolar.FatoEgresso] = [.. OrigemEscolar.DependenciasDoEgresso],
        };

    private static ConteudoDoModelo ConteudoDaInscricao()
    {
        const string enem = "ENEM";
        const string escolaridade = "ESCOLARIDADE";
        const string cotas = "RESERVA_DE_VAGAS";
        const string fator = "FATOR_ATENUACAO";
        const string solicitaFator = "SOLICITA_FATOR_ATENUACAO";
        const string caminhoFator = "CAMINHO_FATOR_ATENUACAO";
        const string atendimento = "ATENDIMENTO_ESPECIALIZADO";
        const string solicitaAtendimento = "SOLICITA_ATENDIMENTO_ESPECIALIZADO";
        const string condicaoAtendimento = "CONDICAO_ATENDIMENTO";

        return new(
            "Inscrição no Processo Seletivo Regular Unificado de Medicina 2027",
            [
                Secao(enem, 1, "ENEM", "A edição do ENEM cujas notas você vai usar neste processo seletivo."),
                Secao(escolaridade, 2, "Ensino médio", "Como você concluiu o ensino médio e onde o cursou."),
                Secao(fator, 3, "Fator de Atenuação das Desigualdades de Acesso", "Pedido do Fator de Atenuação das Desigualdades de Acesso previsto no edital."),
                Secao(cotas, 4, "Reserva de vagas", "Condições que permitem concorrer às vagas reservadas da Lei nº 12.711/2012 e às ações afirmativas do edital."),
                Secao(atendimento, 5, "Atendimento especializado", "Pedido de atendimento especializado nas etapas presenciais do processo seletivo."),
                Bloco("MODALIDADES", 6, BlocoSistema.ModalidadesCalculadas, "Modalidades de concorrência"),
                Bloco("DOCUMENTOS", 7, BlocoSistema.ComprovacaoDocumental, "Documentos"),
                Bloco("REVISAO", 8, BlocoSistema.RevisaoEAceite, "Revisão e aceite"),
            ],
            [
                Campo(EdicaoDoEnem.Fato, 100, enem, "Qual edição do ENEM você vai usar?", TipoRenderizacao.SelecaoUnica,
                    "Só aparecem as edições que o edital aceita."),
                Campo(OrigemEscolar.FatoFormaDeConclusao, 101, escolaridade, "Como você concluiu o ensino médio?", TipoRenderizacao.SelecaoUnica, null),
                Campo(OrigemEscolar.FatoOndeCursou, 102, escolaridade, "Onde você cursou o ensino médio?", TipoRenderizacao.SelecaoUnica,
                    "A resposta define se você pode concorrer às vagas para quem cursou o ensino médio em escola pública.",
                    restricoes: [OpcoesDeOndeCursou()]),
                // Quem cursou o ensino médio regular ou na EJA pede pelo município em que o cursou; quem
                // concluiu por certificação escolhe entre esse caminho e o da residência no período.
                Campo(solicitaFator, 103, fator, "Deseja solicitar o Fator de Atenuação das Desigualdades de Acesso?", TipoRenderizacao.Booleano,
                    "Para quem cursou pelo menos um ano do ensino médio num dos municípios da área que o edital lista ou, se concluiu por certificação, residiu num deles no período do edital. A comprovação é pedida na habilitação."),
                Campo(caminhoFator, 104, fator, "Por qual vínculo com a área você solicita o Fator?", TipoRenderizacao.SelecaoUnica,
                    "Como você concluiu o ensino médio por certificação, pode usar o ensino médio cursado na área ou a residência na área no período do edital.",
                    exibicao: SeAlguma([Condicao(solicitaFator, Operador.Igual, true), Condicao(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, OrigemEscolar.Certificacoes)])),
                Campo("MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", 105, fator, "Em qual município da área de influência você cursou o ensino médio? Se foi em mais de um, informe aquele em que estudou mais tempo.", TipoRenderizacao.SelecaoUnica,
                    "Vale o município em que você cursou pelo menos um ano do ensino médio, em escola pública ou privada. Só aparecem os municípios que o edital lista.",
                    exibicao: SeAlguma(
                        [Condicao(solicitaFator, Operador.Igual, true), Condicao(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, ConclusoesPorCurso)],
                        [Condicao(solicitaFator, Operador.Igual, true), Condicao(caminhoFator, Operador.Igual, CaminhoPeloEnsinoMedio)])),
                Campo("MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO", 106, fator, "Em qual município da área de abrangência você residiu no período? Se foi em mais de um, informe aquele em que residiu mais tempo.", TipoRenderizacao.SelecaoUnica,
                    "Vale ter residido pelo menos um ano na área entre outubro de 2024 e outubro de 2026, conforme o edital.",
                    exibicao: SeAlguma([Condicao(solicitaFator, Operador.Igual, true), Condicao(caminhoFator, Operador.Igual, CaminhoPelaResidencia)])),
                Campo("PCD", 107, cotas, "Você é pessoa com deficiência?", TipoRenderizacao.Booleano,
                    "A deficiência é comprovada por laudo, conforme o edital."),
                Campo("TIPO_DEFICIENCIA", 108, cotas, "Tipo de deficiência", TipoRenderizacao.SelecaoUnica, null,
                    exibicao: Se("PCD", Operador.Igual, true)),
                Campo("CONCORRER_PCD", 109, cotas, "Deseja concorrer às vagas para pessoas com deficiência?", TipoRenderizacao.Booleano, null,
                    exibicao: Se("PCD", Operador.Igual, true)),
                Campo("CONCORRER_EP", 110, cotas, "Deseja concorrer às vagas para quem cursou o ensino médio em escola pública?", TipoRenderizacao.Booleano, null,
                    exibicao: Se(OrigemEscolar.FatoEgresso, Operador.Igual, true)),
                Campo("CONCORRER_PPI", 111, cotas, "Deseja concorrer às vagas para pretos, pardos e indígenas?", TipoRenderizacao.Booleano,
                    "Para quem se autodeclara preto, pardo ou indígena."),
                Campo("INDIGENA", 112, cotas, "Você se autodeclara indígena?", TipoRenderizacao.Booleano, null),
                Campo("QUILOMBOLA", 113, cotas, "Você se autodeclara quilombola?", TipoRenderizacao.Booleano, null),
                Campo("CONCORRER_Q", 114, cotas, "Deseja concorrer às vagas para quilombolas?", TipoRenderizacao.Booleano, null,
                    exibicao: Se("QUILOMBOLA", Operador.Igual, true)),
                Campo("BAIXA_RENDA", 115, cotas, "A renda da sua família é de até um salário mínimo por pessoa?", TipoRenderizacao.Booleano,
                    "Some a renda bruta mensal de todos da família e divida pelo número de pessoas. A renda é comprovada na habilitação."),
                Campo("CONCORRER_RENDA", 116, cotas, "Deseja concorrer às vagas com critério de renda?", TipoRenderizacao.Booleano, null,
                    exibicao: Se("BAIXA_RENDA", Operador.Igual, true)),
                // Atendimento especializado não é só de quem tem deficiência (UNI-REQ-0026): quem pede
                // escolhe a condição e o recurso entre os que o processo oferta; a condição fora da
                // lista é descrita.
                Campo(solicitaAtendimento, 117, atendimento, "Você precisa de atendimento especializado em alguma etapa presencial do processo?", TipoRenderizacao.Booleano,
                    "Por exemplo, por deficiência, gestação, amamentação, idade ou transtorno de aprendizagem."),
                Campo(condicaoAtendimento, 118, atendimento, "Em quais condições você se enquadra?", TipoRenderizacao.SelecaoMultipla,
                    "Marque todas as que se aplicam a você.",
                    exibicao: Se(solicitaAtendimento, Operador.Igual, true)),
                Campo("DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", 119, atendimento, "Descreva a sua condição específica", TipoRenderizacao.Texto,
                    "Diga qual é a condição e de que atendimento você precisa por causa dela.",
                    exibicao: Se(condicaoAtendimento, Operador.Igual, CondicaoForaDaLista), formato: "LIVRE",
                    restricoes: [new TamanhoTexto(null, 500)]),
                Campo("RECURSO_ACESSIBILIDADE", 120, atendimento, "De quais recursos você vai precisar nas etapas presenciais?", TipoRenderizacao.SelecaoMultipla,
                    "Só aparecem os recursos que o processo oferece. Marque todos de que você precisa.",
                    exibicao: Se(solicitaAtendimento, Operador.Igual, true)),
            ],
            [
                Termo(Termos[0], 1),
                Termo(Termos[1], 2),
            ],
            [],
            []);
    }

    private static ConteudoDoModelo ConteudoDaHabilitacao()
    {
        const string conclusao = "CONCLUSAO_ENSINO_MEDIO";
        const string familia = "COMPOSICAO_FAMILIAR";

        return new(
            "Habilitação no Processo Seletivo Regular Unificado de Medicina 2027",
            [
                Secao(conclusao, 1, "Conclusão do ensino médio e vínculos", "Situação da conclusão do ensino médio e vínculos com outras instituições."),
                Secao(familia, 2, "Composição familiar", "Destina-se a quem foi convocado em vaga com critério de renda: informe as pessoas que compõem o seu grupo familiar, incluindo você."),
                Bloco("DOCUMENTOS", 3, BlocoSistema.ComprovacaoDocumental, "Documentos"),
                Bloco("REVISAO", 4, BlocoSistema.RevisaoEAceite, "Revisão e aceite"),
            ],
            [
                Campo("CERTIFICADO_EM_EMITIDO", 1, conclusao, "Seu certificado de conclusão do ensino médio já foi emitido?", TipoRenderizacao.Booleano,
                    "Se ainda não, apresente a declaração de conclusão e o termo de responsabilidade.",
                    exibicao: Se(OrigemEscolar.FatoFormaDeConclusao, Operador.Igual, OrigemEscolar.Regular)),
                Campo("HABILITACAO_POR_PROCURADOR", 2, conclusao, "Outra pessoa fará a habilitação em seu nome, por procuração?", TipoRenderizacao.Booleano,
                    "O procurador apresenta a procuração e o próprio documento de identificação."),
                Campo("VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", 3, conclusao, "Você tem matrícula ativa em outra instituição pública de ensino superior ou bolsa do ProUni?", TipoRenderizacao.Booleano, null),
            ],
            [
                // Pressuposto aceita só fato Declarado; MODALIDADE_CONVOCACAO (UNI-REQ-0147) é
                // Derivado. CONCORRER_RENDA é o superconjunto declarado: todo convocado em cota de
                // renda o respondeu antes.
                Termo(Termos[2], 1, exibicao: Se("CONCORRER_RENDA", Operador.Igual, true)),
            ],
            [OrigemEscolar.FatoFormaDeConclusao, "CONCORRER_RENDA"],
            [
                new GrupoDoModelo(
                    familia, 4, familia, "Membros da composição familiar", Minimo: 1, Maximo: null, Exibicao: null,
                    Obrigatoriedade.Sempre,
                    [
                        Campo(CandidatoComoMembro.FatoParentesco, 1, null, "Parentesco com você", TipoRenderizacao.SelecaoUnica, null),
                        Campo("CATEGORIA_RENDA", 2, null, "Fontes de renda desta pessoa", TipoRenderizacao.SelecaoMultipla,
                            "Marque todas. Elas definem os documentos de renda a apresentar."),
                        Campo("SOB_GUARDA", 3, null, "É menor de idade sob guarda?", TipoRenderizacao.Booleano,
                            "Sob a sua guarda ou a de alguém da família."),
                    ],
                    IncluiCandidato: true),
            ]);
    }

    private static EtapaDoModelo Secao(string codigo, int ordem, string titulo, string descricao) =>
        new(codigo, ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, titulo, descricao, null, null);

    private static EtapaDoModelo Bloco(string codigo, int ordem, BlocoSistema bloco, string titulo) =>
        new(codigo, ordem, TipoEtapaFormulario.Bloco, bloco, titulo, null, null, null);

    private static ItemDoModelo Campo(
        string fato, int ordem, string? etapa, string rotulo, TipoRenderizacao tipo, string? ajuda,
        PredicadoDnf? exibicao = null, Obrigatoriedade? obrigatoriedade = null, string? formato = null,
        IReadOnlyList<RestricaoValor>? restricoes = null) =>
        new(fato, ordem, etapa, rotulo, tipo, formato, ajuda, obrigatoriedade ?? Obrigatoriedade.Sempre, exibicao, restricoes ?? [], PedirConfirmacao: false);

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

    /// <summary>As formas de conclusão de quem cursou o ensino médio, e não o concluiu por certificação.</summary>
    private static IReadOnlyList<string> ConclusoesPorCurso { get; } = [OrigemEscolar.Regular, OrigemEscolar.Eja];

    /// <summary>A condição única "fato operador valor", como predicado de uma cláusula só.</summary>
    private static PredicadoDnf Se<T>(string fato, Operador operador, T valor) => SeAlguma([Condicao(fato, operador, valor)]);

    /// <summary>O predicado que vale quando todas as condições de alguma das cláusulas valem.</summary>
    private static PredicadoDnf SeAlguma(params CondicaoDnf[][] clausulas) =>
        Exigir(
            PredicadoDnf.CriarDeCondicoesAgrupadas([.. clausulas.SelectMany(static (condicoes, i) => condicoes.Select(c => (i + 1, c)))]),
            $"predicado sobre {string.Join(", ", clausulas.SelectMany(static c => c).Select(static c => c.Fato).Distinct(StringComparer.Ordinal))}");

    private static CondicaoDnf Condicao<T>(string fato, Operador operador, T valor) =>
        Exigir(CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)), $"condição sobre {fato}");

    private static string ComandoDoModelo(
        int n, string codigo, string nome, string descricao, FinalidadeFormulario finalidade, ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        SqlDaSemente.ComandoDoModelo(
            Id(TabelaModelo, n), codigo, nome, descricao, finalidade, TipoProcessoCodigo, conteudo, derivacoes, Instante, NomeDaSemente);

    /// <summary>
    /// Os comandos que tiram do banco o tipo de localidade e o nome da comunidade que a semente
    /// gravava como fatos do administrador — eles passaram aos dados básicos, como fatos de sistema —
    /// e o modelo de inscrição que os pedia, para ser gravado de novo sem eles.
    /// </summary>
    public static IReadOnlyList<string> ComandosQueRetiramALocalidadeDaSemente() =>
    [
        $"DELETE FROM configuracao.fato_valor_dominio WHERE fato_candidato_id IN ({Uuid(Id(TabelaFato, 1))}, {Uuid(Id(TabelaFato, 2))});",
        $"DELETE FROM configuracao.rol_de_fatos_candidato WHERE id IN ({Uuid(Id(TabelaFato, 1))}, {Uuid(Id(TabelaFato, 2))});",
        $"DELETE FROM configuracao.modelos_formulario WHERE id = {Uuid(Id(TabelaModelo, 1))};",
    ];

    /// <summary>Os identificadores que a semente grava, para o <c>Down</c> da migration apagá-los.</summary>
    public static IReadOnlyList<string> ComandosDeRemocao() =>
    [
        $"DELETE FROM configuracao.fato_valor_dominio WHERE id::text LIKE '5e3d{TabelaValor:D4}-%';",
        $"DELETE FROM configuracao.rol_de_fatos_candidato WHERE id::text LIKE '5e3d{TabelaFato:D4}-%';",
        $"DELETE FROM configuracao.termo_consentimento_versao WHERE id::text LIKE '5e3d{TabelaVersaoDoTermo:D4}-%';",
        $"DELETE FROM configuracao.termo_consentimento WHERE id::text LIKE '5e3d{TabelaTermo:D4}-%';",
        $"DELETE FROM configuracao.tipos_processo WHERE id::text LIKE '5e3d{TabelaTipoProcesso:D4}-%';",
    ];

    private static string ComandoDoTipoProcesso()
    {
        TipoProcesso tipo = Exigir(TipoProcesso.Criar(TipoProcessoCodigo, "Processo Seletivo Regular Unificado", null), "tipo de processo");
        // O tipo de processo é reaproveitado pelo código quando o ambiente já o tem: os modelos o
        // citam pelo código, e nada depende do identificador dele.
        return Inserir("tipos_processo", "codigo",
            ("id", Uuid(TipoProcessoId)), ("codigo", Texto(tipo.Codigo)), ("nome", Texto(tipo.Nome)),
            ("descricao", Texto(tipo.Descricao)), ("ativo", Logico(tipo.Ativo)), ("created_at", Momento(Instante)));
    }

    private static IEnumerable<string> ComandosDoTermo(TermoDaSemente semente) =>
        SqlDaSemente.ComandosDoTermo(
            semente.TermoId, semente.VersaoId, semente.Nome, semente.Texto, semente.BaseLegal, AutorDaSemente, Instante, NomeDaSemente);

    private static string ComandoDoFato(Guid id, FatoCandidato fato) =>
        Inserir("rol_de_fatos_candidato", "id",
            ("id", Uuid(id)), ("codigo", Texto(fato.Codigo)), ("nome", Texto(fato.Nome)), ("descricao", Texto(fato.Descricao)),
            ("dominio", Texto(DominiosFato.ParaTokenCanonico(fato.Dominio))),
            ("origem", Texto(OrigensFato.ParaTokenCanonico(fato.Origem))),
            ("cardinalidade", Texto(CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade))),
            ("fonte_valores", Texto(fato.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null)),
            ("formato", Texto(fato.Formato is { } formato ? FormatosTexto.ParaTokenCanonico(formato) : null)),
            ("ponto_resolucao", Texto(fato.PontoResolucao)), ("binding", Texto(fato.Binding)),
            ("escopo", Texto(EscoposFato.ParaTokenCanonico(fato.Escopo))),
            ("classificacao_protecao", Texto(ClassificacoesProtecaoDado.ParaTokenCanonico(fato.ClassificacaoProtecao))),
            ("finalidade_tratamento", Texto(fato.FinalidadeTratamento)),
            ("hipotese_legal", Texto(HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal))),
            ("sistema", Logico(fato.Sistema)), ("ativo", Logico(fato.Ativo)), ("regras_padrao", "'[]'::jsonb"),
            ("dependencias", ListaDeTexto(fato.Dependencias)), ("created_at", Momento(Instante)));

    private static string ComandoDoValor(Guid id, Guid fatoId, FatoValorDominio valor) =>
        Inserir("fato_valor_dominio", "id",
            ("id", Uuid(id)), ("fato_candidato_id", Uuid(fatoId)), ("codigo", Texto(valor.Codigo)),
            ("descricao", Texto(valor.Descricao)), ("ordem", valor.Ordem.ToString(CultureInfo.InvariantCulture)),
            ("ativo", Logico(valor.Ativo)), ("created_at", Momento(Instante)));

    private static T Exigir<T>(Result<T> resultado, string oQue) => SqlDaSemente.Exigir(resultado, NomeDaSemente, oQue);

    private static void Exigir(Result resultado, string oQue) => SqlDaSemente.Exigir(resultado, NomeDaSemente, oQue);
}
