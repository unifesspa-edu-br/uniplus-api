namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Globalization;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

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

    private const HipoteseLegalTratamento HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    private static readonly DateTimeOffset Instante = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    private const string FinalidadeDoEndereco = FatoCandidatoSeed.FinalidadeResidencia;
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

    /// <summary>Os tipos de endereço em que o formulário pede o nome da comunidade.</summary>
    private static readonly string[] LocalidadesComComunidade = ["ALDEIA", "COMUNIDADE", "QUILOMBO"];

    /// <summary>O modelo do formulário de inscrição de Medicina.</summary>
    public const string ModeloDeInscricao = "PSR_MEDICINA_2027_INSCRICAO";

    /// <summary>O modelo do formulário de habilitação de Medicina.</summary>
    public const string ModeloDeHabilitacao = "PSR_MEDICINA_2027_HABILITACAO";

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
        // Inscrição: o endereço do candidato fora da zona urbana e a comunidade, para quem mora em
        // aldeia, comunidade tradicional ou quilombo.
        new(1, _ => FatoCandidato.CriarDoAdministrador(
                "TIPO_ENDERECO", "Tipo de endereço", "Onde fica o endereço de residência: zona urbana, zona rural, aldeia, comunidade tradicional, quilombo, vila ou outro.", DominioFato.Categorico, CardinalidadeFato.Escalar,
                FonteValoresFato.Global, null, PontoInscricao, EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal,
                FinalidadeDoEndereco, HipoteseLegal),
            [("URBANO", "Urbano"), ("RURAL", "Rural"), ("ALDEIA", "Aldeia"), ("COMUNIDADE", "Comunidade tradicional"),
             ("QUILOMBO", "Quilombo"), ("VILA", "Vila"), ("OUTRO", "Outro")]),
        new(2, _ => FatoCandidato.CriarDoAdministrador(
                "NOME_COMUNIDADE", "Nome da aldeia, comunidade ou quilombo", "O nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside.", DominioFato.Texto, CardinalidadeFato.Escalar,
                null, FormatoTexto.Livre, PontoInscricao, EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal,
                FinalidadeDoEndereco, HipoteseLegal),
            Sem()),

        // Inscrição: o bônus regional, pedido pelo candidato e conferido pelo município da área do bônus.
        new(3, _ => FatoCandidato.CriarDoAdministrador(
                "SOLICITA_BONUS_REGIONAL", "Solicita o bônus regional", "Se o candidato pede o bônus regional previsto no edital.", DominioFato.Booleano, CardinalidadeFato.Escalar,
                null, null, PontoInscricao, EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal,
                FinalidadeDoEndereco, HipoteseLegal),
            Sem()),
        new(4, _ => FatoCandidato.CriarDoAdministrador(
                "MUNICIPIO_EM_AREA_BONUS", "Município da área do bônus regional", "O município da área do bônus regional em que o candidato reside, entre os que o edital lista.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.MunicipiosBonus, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDoEndereco, HipoteseLegal),
            Sem()),

        // Inscrição: como o candidato concluiu o ensino médio (UNI-REQ-0148). Fica como fato do
        // administrador até a origem escolar ser decidida (uniplus-api#1298).
        new(5, _ => FatoCandidato.CriarDoAdministrador(
                "FORMA_CONCLUSAO_EM", "Forma de conclusão do ensino médio", "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.", DominioFato.Categorico,
                CardinalidadeFato.Escalar, FonteValoresFato.Global, null, PontoInscricao, EscopoFato.Candidato,
                ClassificacaoProtecaoDado.Pessoal, FinalidadeDaEscolaridade, HipoteseLegal),
            [("REGULAR", "Ensino médio regular"), ("EJA", "Educação de Jovens e Adultos (EJA)"),
             ("ENCCEJA", "Certificação pelo ENCCEJA"), ("PROFICIENCIA", "Exame de proficiência"),
             ("ENEM", "Certificação pelo ENEM")]),

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
            FinalidadeFormulario.Inscricao, ConteudoDaInscricao()),
        ComandoDoModelo(2, ModeloDeHabilitacao, "Habilitação — Medicina 2027",
            "Formulário de habilitação dos convocados no Processo Seletivo Regular Unificado de Medicina 2027.",
            FinalidadeFormulario.Habilitacao, ConteudoDaHabilitacao()),
    ];

    /// <summary>Os comandos que apagam os modelos semeados, para o <c>Down</c> da migration.</summary>
    public static IReadOnlyList<string> ComandosDeRemocaoDosModelos() =>
    [
        $"DELETE FROM configuracao.modelos_formulario WHERE id::text LIKE '5e3d{TabelaModelo:D4}-%';",
    ];

    private static ConteudoDoModelo ConteudoDaInscricao()
    {
        const string endereco = "ENDERECO";
        const string escolaridade = "ESCOLARIDADE";
        const string cotas = "RESERVA_DE_VAGAS";
        const string bonus = "BONUS_REGIONAL";

        return new(
            "Inscrição no Processo Seletivo Regular Unificado de Medicina 2027",
            [
                Secao(endereco, 1, "Endereço", "Informações sobre o local de residência."),
                Secao(escolaridade, 2, "Ensino médio", "Como você concluiu o ensino médio."),
                Secao(cotas, 3, "Reserva de vagas", "Condições que permitem concorrer às vagas reservadas da Lei nº 12.711/2012 e às ações afirmativas do edital."),
                Secao(bonus, 4, "Bônus regional", "Pedido do bônus regional previsto no edital."),
                Bloco("MODALIDADES", 5, BlocoSistema.ModalidadesCalculadas, "Modalidades de concorrência"),
                Bloco("DOCUMENTOS", 6, BlocoSistema.ComprovacaoDocumental, "Documentos"),
                Bloco("REVISAO", 7, BlocoSistema.RevisaoEAceite, "Revisão e aceite"),
            ],
            [
                Campo("TIPO_ENDERECO", 101, endereco, "Tipo de endereço", TipoRenderizacao.SelecaoUnica,
                    "Indique se o endereço de residência fica em zona urbana, zona rural, aldeia, comunidade tradicional, quilombo, vila ou outro tipo de localidade."),
                Campo("NOME_COMUNIDADE", 102, endereco, "Nome da aldeia, comunidade ou quilombo", TipoRenderizacao.Texto,
                    "Informe o nome da aldeia, da comunidade tradicional ou do quilombo em que você reside.",
                    exibicao: Se("TIPO_ENDERECO", Operador.Em, LocalidadesComComunidade), formato: "LIVRE"),
                Campo("FORMA_CONCLUSAO_EM", 103, escolaridade, "Forma de conclusão do ensino médio", TipoRenderizacao.SelecaoUnica,
                    "Escolha como você concluiu o ensino médio: curso regular, Educação de Jovens e Adultos (EJA), ENCCEJA, exame de proficiência ou ENEM."),
                Campo("PCD", 104, cotas, "Pessoa com deficiência", TipoRenderizacao.Booleano,
                    "Responda sim se você é pessoa com deficiência. A condição é comprovada por laudo, conforme o edital."),
                Campo("TIPO_DEFICIENCIA", 105, cotas, "Tipo de deficiência", TipoRenderizacao.SelecaoUnica,
                    "Indique o tipo de deficiência, entre os que o processo seletivo considera.",
                    exibicao: Se("PCD", Operador.Igual, true)),
                Campo("CONCORRER_PCD", 106, cotas, "Deseja concorrer às vagas para pessoas com deficiência", TipoRenderizacao.Booleano,
                    "Responda sim para concorrer às vagas reservadas a pessoas com deficiência.",
                    exibicao: Se("PCD", Operador.Igual, true)),
                Campo("EGRESSO_ESCOLA_PUBLICA", 107, cotas, "Cursou todo o ensino médio em escola pública", TipoRenderizacao.Booleano,
                    "Responda sim se você cursou integralmente o ensino médio em escola pública."),
                Campo("CONCORRER_EP", 108, cotas, "Deseja concorrer às vagas para egressos de escola pública", TipoRenderizacao.Booleano,
                    "Responda sim para concorrer às vagas reservadas a quem cursou o ensino médio em escola pública.",
                    exibicao: Se("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, true)),
                Campo("CONCORRER_PPI", 109, cotas, "Deseja concorrer às vagas para pretos, pardos e indígenas", TipoRenderizacao.Booleano,
                    "Para quem se autodeclara preto, pardo ou indígena. Responda sim para concorrer às vagas reservadas a esse grupo."),
                Campo("QUILOMBOLA", 110, cotas, "Quilombola", TipoRenderizacao.Booleano,
                    "Responda sim se você se autodeclara quilombola."),
                Campo("CONCORRER_Q", 111, cotas, "Deseja concorrer às vagas para quilombolas", TipoRenderizacao.Booleano,
                    "Responda sim para concorrer às vagas reservadas a quilombolas.",
                    exibicao: Se("QUILOMBOLA", Operador.Igual, true)),
                Campo("BAIXA_RENDA", 112, cotas, "Renda familiar bruta mensal por pessoa de até um salário mínimo", TipoRenderizacao.Booleano,
                    "Responda sim se a renda familiar bruta mensal por pessoa é igual ou inferior a um salário mínimo. A renda é comprovada na habilitação."),
                Campo("CONCORRER_RENDA", 113, cotas, "Deseja concorrer às vagas com critério de renda", TipoRenderizacao.Booleano,
                    "Responda sim para concorrer às vagas reservadas por critério de renda.",
                    exibicao: Se("BAIXA_RENDA", Operador.Igual, true)),
                Campo("CONDICAO_ATENDIMENTO", 114, cotas, "Atendimento especializado", TipoRenderizacao.SelecaoMultipla,
                    "Se precisar de atendimento especializado para realizar as etapas do processo, escolha os recursos necessários. Deixe em branco se não precisar.",
                    obrigatoriedade: Obrigatoriedade.Nunca),
                Campo("SOLICITA_BONUS_REGIONAL", 115, bonus, "Deseja solicitar o bônus regional", TipoRenderizacao.Booleano,
                    "Responda sim para pedir o bônus regional previsto no edital."),
                Campo("MUNICIPIO_EM_AREA_BONUS", 116, bonus, "Município da área do bônus regional", TipoRenderizacao.SelecaoUnica,
                    "Escolha o município da área do bônus regional em que você reside, entre os que o edital lista.",
                    exibicao: Se("SOLICITA_BONUS_REGIONAL", Operador.Igual, true)),
            ],
            [
                Termo(Termos[0], 1),
                Termo(Termos[1], 2),
                Termo(Termos[2], 3, exibicao: Se("CONCORRER_RENDA", Operador.Igual, true)),
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
                Campo("CERTIFICADO_EM_EMITIDO", 1, conclusao, "O certificado de conclusão do ensino médio já foi emitido", TipoRenderizacao.Booleano,
                    "Responda não se a escola ainda não emitiu o certificado. Nesse caso, apresente a declaração de conclusão e o termo de responsabilidade.",
                    exibicao: Se("FORMA_CONCLUSAO_EM", Operador.Igual, "REGULAR")),
                Campo("HABILITACAO_POR_PROCURADOR", 2, conclusao, "A habilitação é feita por procurador", TipoRenderizacao.Booleano,
                    "Responda sim se outra pessoa fará a habilitação em seu nome. O procurador apresenta a procuração e o próprio documento de identificação."),
                Campo("VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", 3, conclusao, "Tem matrícula em outra instituição pública de ensino superior ou bolsa do ProUni", TipoRenderizacao.Booleano,
                    "Responda sim se você tem matrícula ativa em outra instituição pública de ensino superior ou é bolsista do ProUni."),
            ],
            [],
            ["FORMA_CONCLUSAO_EM"],
            [
                new GrupoDoModelo(
                    familia, 4, familia, "Membros da composição familiar", Minimo: 1, Maximo: null, Exibicao: null,
                    Obrigatoriedade.Sempre,
                    [
                        Campo(CandidatoComoMembro.FatoParentesco, 1, null, "Parentesco com você", TipoRenderizacao.SelecaoUnica,
                            "Para quem foi convocado em vaga com critério de renda. Indique o parentesco deste membro com você; para você mesmo, escolha \"O próprio candidato\"."),
                        Campo("CATEGORIA_RENDA", 2, null, "Categorias de renda", TipoRenderizacao.SelecaoMultipla,
                            "Escolha todas as fontes de renda deste membro. Elas definem os documentos de renda a apresentar."),
                        Campo("SOB_GUARDA", 3, null, "Menor sob guarda", TipoRenderizacao.Booleano,
                            "Responda sim se este membro é menor de idade sob a sua guarda ou a de alguém do grupo familiar."),
                    ],
                    IncluiCandidato: true),
            ]);
    }

    private static EtapaDoModelo Secao(string codigo, int ordem, string titulo, string descricao) =>
        new(codigo, ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, titulo, descricao, null, null);

    private static EtapaDoModelo Bloco(string codigo, int ordem, BlocoSistema bloco, string titulo) =>
        new(codigo, ordem, TipoEtapaFormulario.Bloco, bloco, titulo, null, null, null);

    private static ItemDoModelo Campo(
        string fato, int ordem, string? etapa, string rotulo, TipoRenderizacao tipo, string ajuda,
        PredicadoDnf? exibicao = null, Obrigatoriedade? obrigatoriedade = null, string? formato = null) =>
        new(fato, ordem, etapa, rotulo, tipo, formato, ajuda, obrigatoriedade ?? Obrigatoriedade.Sempre, exibicao, [], PedirConfirmacao: false);

    private static TermoDoModelo Termo(TermoDaSemente termo, int ordem, PredicadoDnf? exibicao = null) =>
        new(termo.Codigo, ordem, termo.TermoId, termo.VersaoId, exibicao, Obrigatoriedade.Sempre);

    /// <summary>A condição única "fato operador valor", como predicado de uma cláusula só.</summary>
    private static PredicadoDnf Se<T>(string fato, Operador operador, T valor)
    {
        CondicaoDnf condicao = Exigir(CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)), $"condição sobre {fato}");
        return Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas([(1, condicao)]), $"predicado sobre {fato}");
    }

    private static string ComandoDoModelo(
        int n, string codigo, string nome, string descricao, FinalidadeFormulario finalidade, ConteudoDoModelo conteudo)
    {
        ModeloFormulario modelo = Exigir(
            ModeloFormulario.Criar(codigo, nome, descricao, finalidade, TipoProcessoCodigo, conteudo,
                new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)),
            $"modelo {codigo}");
        Exigir(modelo.Ativar(), $"ativação do modelo {codigo}");

        return Inserir("modelos_formulario", "id",
            ("id", Uuid(Id(TabelaModelo, n))), ("codigo", Texto(modelo.Codigo)), ("nome", Texto(modelo.Nome)),
            ("descricao", Texto(modelo.Descricao)), ("finalidade", Texto(EstruturaFormulario.ParaToken(modelo.Finalidade))),
            ("tipo_processo_codigo", Texto(modelo.TipoProcessoCodigo)),
            ("conteudo", $"{Texto(ConteudoDoModeloJson.Serializar(modelo.Conteudo))}::jsonb"),
            ("ativo", Logico(modelo.Ativo)), ("created_at", Momento(Instante)));
    }

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

    private static IEnumerable<string> ComandosDoTermo(TermoDaSemente semente)
    {
        TermoConsentimento termo = Exigir(
            TermoConsentimento.Criar(semente.Nome, semente.Texto, semente.BaseLegal,
                FormasAceite.ParaTokenCanonico(FormaAceite.RegistroDigitalComLogIp)),
            $"termo {semente.Codigo}");
        Exigir(termo.MarcarRevisado(AutorDaSemente, Instante), $"revisão do termo {semente.Codigo}");
        TermoConsentimentoVersao versao = Exigir(termo.Promover(AutorDaSemente, Instante), $"promoção do termo {semente.Codigo}");

        yield return Inserir("termo_consentimento", "id",
            ("id", Uuid(semente.TermoId)), ("nome", Texto(termo.Nome)), ("texto_rascunho", Texto(termo.TextoRascunho)),
            ("base_legal_rascunho", Texto(termo.BaseLegalRascunho)),
            ("forma_aceite_rascunho", Texto(FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho))),
            ("revisado", Logico(termo.Revisado)), ("is_deleted", Logico(false)), ("created_at", Momento(Instante)));
        yield return Inserir("termo_consentimento_versao", "id",
            ("id", Uuid(semente.VersaoId)), ("termo_consentimento_id", Uuid(semente.TermoId)),
            ("texto", Texto(versao.Texto)), ("base_legal", Texto(versao.BaseLegal)),
            ("forma_aceite", Texto(FormasAceite.ParaTokenCanonico(versao.FormaAceite))), ("hash", Texto(versao.Hash)),
            ("promovida_em", Momento(versao.PromovidaEm)), ("promovida_por", Texto(versao.PromovidaPor)));
    }

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

    private static T Exigir<T>(Result<T> resultado, string oQue) =>
        resultado.IsSuccess ? resultado.Value! : throw Recusa(resultado.Errors, oQue);

    private static void Exigir(Result resultado, string oQue)
    {
        if (resultado.IsFailure)
        {
            throw Recusa(resultado.Errors, oQue);
        }
    }

    private static InvalidOperationException Recusa(IEnumerable<FieldError> erros, string oQue) =>
        new($"A semente do PSR Medicina 2027 tem {oQue} que o domínio recusa: "
            + string.Join("; ", erros.Select(static e => $"{e.Field}: {e.Error.Message}")));

    /// <summary>
    /// A inserção que ignora a linha já gravada com a mesma chave <paramref name="chave"/>. Para os
    /// fatos, os valores e os termos a chave é o identificador fixo: um código já ocupado por outro
    /// cadastro do ambiente falha pelo índice único, com o nome do código, em vez de ser ignorado e
    /// deixar os filhos apontando para o fato que não entrou.
    /// </summary>
    private static string Inserir(string tabela, string chave, params (string Coluna, string Valor)[] colunas) =>
        $"INSERT INTO configuracao.{tabela} ({string.Join(", ", colunas.Select(static c => c.Coluna))}) "
        + $"VALUES ({string.Join(", ", colunas.Select(static c => c.Valor))}) ON CONFLICT ({chave}) DO NOTHING;";

    private static string Uuid(Guid id) => $"'{id}'::uuid";

    private static string Texto(string? texto) => texto is null ? "NULL" : $"'{texto.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string Logico(bool valor) => valor ? "TRUE" : "FALSE";

    private static string Momento(DateTimeOffset instante) =>
        $"'{instante.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}+00'::timestamptz";

    private static string ListaDeTexto(IReadOnlyList<string> itens)
    {
        StringBuilder sql = new("ARRAY[");
        sql.Append(string.Join(", ", itens.Select(Texto)));
        sql.Append("]::text[]");
        return sql.ToString();
    }
}
