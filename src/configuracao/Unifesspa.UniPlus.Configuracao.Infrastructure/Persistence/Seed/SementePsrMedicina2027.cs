namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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
/// domínio recusaria derruba a subida do banco em vez de gravar dado que a API não aceita.
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
    internal const string NomeDaSemente = "do PSR Medicina 2027";

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

    /// <summary>
    /// O modelo do formulário de inscrição de processo PSR sem prova, como o de Medicina, que classifica
    /// pela nota do ENEM: não pergunta por atendimento especializado, que serve à realização da prova.
    /// </summary>
    public const string ModeloDeInscricaoSemProva = "PSR_INSCRICAO_SEM_PROVA";

    /// <summary>
    /// O modelo do formulário de inscrição de processo PSR com prova: traz o atendimento especializado
    /// nas etapas presenciais e os documentos que o comprovam.
    /// </summary>
    public const string ModeloDeInscricaoComProva = "PSR_INSCRICAO_COM_PROVA";

    /// <summary>O modelo do formulário de habilitação de Medicina.</summary>
    public const string ModeloDeHabilitacao = "PSR_MEDICINA_2027_HABILITACAO";

    /// <summary>
    /// O código da condição de atendimento "outra condição específica" do vocabulário do INEP, que o
    /// processo que aplica o modelo de inscrição oferta e cuja escolha pede a descrição da condição.
    /// </summary>
    public const string CondicaoForaDaLista = "OUTRA_CONDICAO";

    /// <summary>
    /// O máximo de caracteres da descrição da condição de atendimento que não consta da lista: ela diz
    /// qual é a condição, e o atendimento de que se precisa vai para o laudo.
    /// </summary>
    public const int LimiteDaDescricaoDaOutraCondicao = 120;

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
                ClassificacaoDeProtecao.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(13, _ => FatoCandidato.CriarDoAdministrador(
                "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO", "Município de residência para o Fator de Atenuação", "O município da área de abrangência do edital em que o candidato residiu no período que o edital fixa, entre os que o edital lista.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.MunicipiosBonus, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(16, _ => FatoCandidato.CriarDoAdministrador(
                "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "Município do ensino médio para o Fator de Atenuação", "O município da área de influência do edital em que o candidato cursou pelo menos um ano do ensino médio, entre os que o edital lista.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.MunicipiosBonus, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDoFator, HipoteseLegal),
            Sem()),
        new(17, _ => FatoCandidato.CriarDoAdministrador(
                "CAMINHO_FATOR_ATENUACAO", "Caminho do Fator de Atenuação", "Por qual vínculo com a área do edital pede o Fator de Atenuação quem concluiu o ensino médio por certificação: o ensino médio cursado ou a residência no período.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.Global, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDoFator, HipoteseLegal),
            [(CaminhoPeloEnsinoMedio, "Cursei pelo menos um ano do ensino médio na área"),
             (CaminhoPelaResidencia, "Residi na área no período do edital")]),

        // Inscrição: o pedido de atendimento especializado nas etapas presenciais, que abre as
        // perguntas da condição e do recurso, e a descrição da condição que não consta da lista.
        // A saúde que a resposta pode revelar faz dos dois um dado sensível.
        new(14, _ => FatoCandidato.CriarDoAdministrador(
                "SOLICITA_ATENDIMENTO_ESPECIALIZADO", "Solicita atendimento especializado", "Se o candidato pede atendimento especializado em alguma etapa presencial do processo seletivo.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Sensivel, FinalidadeDoAtendimento, HipoteseLegal),
            Sem()),
        new(15, _ => FatoCandidato.CriarDoAdministrador(
                "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "Descrição da outra condição de atendimento", "A condição específica, que não consta da lista, pela qual o candidato pede atendimento especializado.", DominioFato.Texto,
                CardinalidadeFato.Escalar, null, FormatoTexto.Livre, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Sensivel, FinalidadeDoAtendimento, HipoteseLegal),
            Sem()),

        // Habilitação: o que o edital pede de quem é convocado (itens 9.16, 9.21 e 9.24).
        new(6, _ => FatoCandidato.CriarDoAdministrador(
                "CERTIFICADO_EM_EMITIDO", "Certificado de conclusão do ensino médio já emitido", "Se o certificado de conclusão do ensino médio já foi emitido. Sem ele, a habilitação pede a declaração de conclusão e o termo de responsabilidade.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),
        new(7, _ => FatoCandidato.CriarDoAdministrador(
                "HABILITACAO_POR_PROCURADOR", "Habilitação feita por procurador", "Se a habilitação é feita por procurador, que apresenta a procuração e o próprio documento de identificação.", DominioFato.Booleano,
                CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),
        new(8, _ => FatoCandidato.CriarDoAdministrador(
                "VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", "Vínculo com outra instituição pública de ensino superior ou com o ProUni", "Se o candidato tem matrícula ativa em outra instituição pública de ensino superior ou é bolsista do ProUni.",
                DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, PontoHabilitacao, EscopoFato.Candidato,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDaHabilitacao, HipoteseLegal),
            Sem()),

        // Habilitação: a categoria de renda de cada membro da composição familiar (edital, item
        // 9.39.2) e as categorias presentes na família, que os documentos de renda citam.
        new(9, _ => FatoCandidato.CriarDoAdministrador(
                "CATEGORIA_RENDA", "Categoria de renda", "As fontes de renda do membro da composição familiar, que definem os documentos de renda que ele apresenta.", DominioFato.Categorico, CardinalidadeFato.Multivalorado,
                FonteValoresFato.Global, null, PontoHabilitacao, EscopoFato.MembroGrupo,
                ClassificacaoDeProtecao.Pessoal, FinalidadeDaFamilia, HipoteseLegal),
            [("ASSALARIADO", "Assalariado"), ("ATIVIDADE_RURAL", "Atividade rural"),
             ("APOSENTADO_OU_PENSIONISTA", "Aposentado ou pensionista"),
             ("AUTONOMO_OU_PROFISSIONAL_LIBERAL", "Autônomo ou profissional liberal"),
             ("ALUGUEL_OU_ARRENDAMENTO", "Renda de aluguel ou arrendamento"), ("SEM_RENDA", "Sem renda"),
             ("DESEMPREGADO", "Desempregado")]),
        new(10, catalogo => FatoCandidato.CriarAgregadoDoAdministrador(
                "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda presentes na família", "As categorias de renda que aparecem entre os membros da composição familiar, calculadas a partir das respostas de cada membro.", "CATEGORIA_RENDA", catalogo,
                PontoHabilitacao, ClassificacaoDeProtecao.Pessoal, FinalidadeDaFamilia, HipoteseLegal),
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
    internal static IReadOnlyList<string> ComandosDosFatos(IReadOnlyList<FatoDaSemente> fatos) =>
    [
        .. Construir(fatos).SelectMany(static c =>
            new[] { ComandoDoFato(Id(TabelaFato, c.Semente.N), c.Fato) }.Concat(c.Fato.ValoresDominioDeclarados.Select((valor, i) =>
                ComandoDoValor(Id(TabelaValor, (c.Semente.N * 100) + i + 1), Id(TabelaFato, c.Semente.N), valor)))),
    ];

    /// <summary>Os fatos construídos pelas factories do domínio, na ordem, cada um com os valores dele.</summary>
    private static List<(FatoDaSemente Semente, FatoCandidato Fato)> Construir(IReadOnlyList<FatoDaSemente> fatos)
    {
        List<(FatoDaSemente, FatoCandidato)> construidos = [];
        foreach (FatoDaSemente semente in fatos)
        {
            CatalogoDeFatos catalogo = new([.. construidos.Select(static c => c.Item2)], []);
            FatoCandidato fato = Exigir(semente.Construir(catalogo), $"fato {semente.N}");
            AdicionarValores(fato, semente.Valores);
            construidos.Add((semente, fato));
        }

        return construidos;
    }

    private static void AdicionarValores(FatoCandidato fato, IReadOnlyList<(string Codigo, string Descricao)> valores)
    {
        for (int ordem = 0; ordem < valores.Count; ordem++)
        {
            (string codigo, string descricao) = valores[ordem];
            Exigir(fato.AdicionarValorDominio(codigo, descricao, ordem, ativo: true), $"valor {codigo} de {fato.Codigo}");
        }
    }

    /// <summary>
    /// Os comandos que gravam os modelos, ativos: a inscrição sem prova e com prova, que partem das
    /// mesmas partes e diferem só pelo atendimento especializado, e a habilitação. O conteúdo é construído pelos records do
    /// domínio e conferido por <see cref="ModeloFormulario.Criar"/> — estrutura, blocos por finalidade
    /// e grafo das regras —, e serializado pelo mesmo conversor que a aplicação usa para ler.
    /// </summary>
    /// <remarks>
    /// O modelo de inscrição é gravado com a seção dos dados básicos, mesclada como a escrita do
    /// modelo a mescla, para as regras dele poderem citar os dados básicos, como a cor ou raça.
    /// </remarks>
    public static IReadOnlyList<string> ComandosDosModelos() =>
    [
        ComandoDoModelo(4, ModeloDeInscricaoSemProva, "Inscrição — PSR sem prova",
            "Formulário de inscrição do Processo Seletivo Regular Unificado sem aplicação de prova, que classifica pela nota do ENEM.",
            FinalidadeFormulario.Inscricao, ConjuntoBasicoDoModelo.Mesclar(ModelosDaSementePsrMedicina2027.Inscricao(comProva: false), FormatosDoCatalogo()), DerivacoesDaInscricao),
        ComandoDoModelo(2, ModeloDeHabilitacao, "Habilitação — Medicina 2027",
            "Formulário de habilitação dos convocados no Processo Seletivo Regular Unificado de Medicina 2027.",
            FinalidadeFormulario.Habilitacao, ModelosDaSementePsrMedicina2027.Habilitacao(),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)),
        ComandoDoModelo(3, ModeloDeInscricaoComProva, "Inscrição — PSR com prova",
            "Formulário de inscrição do Processo Seletivo Regular Unificado com aplicação de prova, com o pedido de atendimento especializado nas etapas presenciais.",
            FinalidadeFormulario.Inscricao, ConjuntoBasicoDoModelo.Mesclar(ModelosDaSementePsrMedicina2027.Inscricao(comProva: true), FormatosDoCatalogo()), DerivacoesDaInscricao),
    ];

    /// <summary>
    /// A inscrição cita o egresso de escola pública, que deriva da origem escolar, e a opção pelas
    /// cotas da lei, que deriva do egresso e da opção pelas vagas de escola pública.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> DerivacoesDaInscricao { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [OrigemEscolar.FatoEgresso] = [.. OrigemEscolar.DependenciasDoEgresso],
            [OpcaoPelasCotasDaLei.Fato] = [.. OpcaoPelasCotasDaLei.DependenciasDaOpcao],
        };

    /// <summary>
    /// O formato da resposta de cada fato de texto que o modelo de inscrição coleta, os de sistema e os
    /// desta semente, como a escrita do modelo o lê do catálogo.
    /// </summary>
    private static Dictionary<string, string> FormatosDoCatalogo() =>
        FatoCandidatoSeed.Itens
            .Where(static f => f.Formato is { } formato && formato != FormatoTexto.Nenhum)
            .ToDictionary(static f => f.Codigo, static f => FormatosTexto.ParaTokenCanonico(f.Formato!.Value), StringComparer.Ordinal)
            .Concat(VocabularioDoCatalogo.Formatos(Construir(Fatos).Select(static c => c.Fato)))
            .ToDictionary(StringComparer.Ordinal);

    private static string ComandoDoModelo(
        int n, string codigo, string nome, string descricao, FinalidadeFormulario finalidade, ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        SqlDaSemente.ComandoDoModelo(
            Id(TabelaModelo, n), codigo, nome, descricao, finalidade, TipoProcessoCodigo, conteudo, derivacoes, Instante, NomeDaSemente);

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
        Inserir("rol_de_fatos_candidato", "id", [.. ColunasDoFato(id, fato), .. ColunasDaAprovacao(fato.Aprovacao)]);

    private static (string Coluna, string Valor)[] ColunasDoFato(Guid id, FatoCandidato fato) =>
    [
        ("id", Uuid(id)), ("codigo", Texto(fato.Codigo)), ("nome", Texto(fato.Nome)), ("descricao", Texto(fato.Descricao)),
        ("dominio", Texto(DominiosFato.ParaTokenCanonico(fato.Dominio))),
        ("origem", Texto(OrigensFato.ParaTokenCanonico(fato.Origem))),
        ("cardinalidade", Texto(CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade))),
        ("fonte_valores", Texto(fato.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null)),
        ("formato", Texto(fato.Formato is { } formato ? FormatosTexto.ParaTokenCanonico(formato) : null)),
        ("ponto_resolucao", Texto(fato.PontoResolucao)), ("binding", Texto(fato.Binding)),
        ("escopo", Texto(EscoposFato.ParaTokenCanonico(fato.Escopo))),
        ("classificacao_protecao", Texto(ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao))),
        ("finalidade_tratamento", Texto(fato.FinalidadeTratamento)),
        ("hipotese_legal", Texto(HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal))),
        ("sistema", Logico(fato.Sistema)), ("ativo", Logico(fato.Ativo)), ("regras_padrao", "'[]'::jsonb"),
        ("dependencias", ListaDeTexto(fato.Dependencias)), ("created_at", Momento(Instante)),
    ];

    private static (string Coluna, string Valor)[] ColunasDaAprovacao(AprovacaoDaProtecao aprovacao) =>
    [
        ("protecao_situacao", Texto(SituacoesDaAprovacao.ParaTokenCanonico(aprovacao.Situacao))),
        ("protecao_ciclo", aprovacao.Ciclo.Numero.ToString(CultureInfo.InvariantCulture)),
        ("protecao_manifestacao_desfavoravel", Logico(aprovacao.ManifestacaoDesfavoravelVigente)),
        ("protecao_manifestacoes_no_ciclo", aprovacao.ManifestacoesNoCiclo.ToString(CultureInfo.InvariantCulture)),
    ];

    private static string ComandoDoValor(Guid id, Guid fatoId, FatoValorDominio valor) =>
        Inserir("fato_valor_dominio", "id",
            ("id", Uuid(id)), ("fato_candidato_id", Uuid(fatoId)), ("codigo", Texto(valor.Codigo)),
            ("descricao", Texto(valor.Descricao)), ("ordem", valor.Ordem.ToString(CultureInfo.InvariantCulture)),
            ("ativo", Logico(valor.Ativo)), ("created_at", Momento(Instante)));

    private static T Exigir<T>(Result<T> resultado, string oQue) => SqlDaSemente.Exigir(resultado, NomeDaSemente, oQue);

    private static void Exigir(Result resultado, string oQue) => SqlDaSemente.Exigir(resultado, NomeDaSemente, oQue);
}
