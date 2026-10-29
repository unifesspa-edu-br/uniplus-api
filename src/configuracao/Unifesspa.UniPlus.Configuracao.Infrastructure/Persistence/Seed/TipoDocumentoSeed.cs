namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Fonte única do seed do catálogo de tipos de documento (UNI-REQ-0013) — os
/// setenta tipos consolidados dos dois sistemas legados e os vinte e dois que o edital
/// de Medicina 2027 acrescenta (UNI-REQ-0151). Consumida tanto pela
/// configuração EF Core (que materializa as linhas via <c>HasData</c> na
/// migration) quanto pelos testes (que conferem o seed do banco contra esta
/// lista), garantindo uma única definição por tipo.
/// </summary>
/// <remarks>
/// <para>O tipo de documento é cadastro <b>CRUD-administrado e seed-governado</b>,
/// no mesmo molde de <see cref="PrecedenciaFaseSeed"/> e
/// <see cref="CategoriaDocumentoSeed"/>: o catálogo é conhecido e não depende de
/// ato operacional pós-deploy para existir, enquanto o CRUD admin continua
/// disponível para acrescentar, editar e remover.</para>
/// <para><b>O nome nomeia o documento, e só.</b> Finalidade, competência temporal
/// e critério de aplicabilidade ficaram de fora de propósito: eles pertencem à
/// exigência documental do edital, não ao cadastro classificatório. É por isso
/// que o legado <c>Certidão de quitação com o Serviço Militar (candidato do sexo
/// masculino maiores de 18 anos)</c> entra aqui como
/// <c>QUITACAO_SERVICO_MILITAR</c> — o recorte por sexo e idade é gatilho da
/// exigência, escrito em condição, não em rótulo.</para>
/// <para>Os <see cref="Guid"/> são fixos determinísticos (não
/// <c>Guid.CreateVersion7</c>) porque seed precisa de identidade estável entre
/// ambientes — o mesmo molde de <see cref="ModalidadeSeed"/> e
/// <see cref="CategoriaDocumentoSeed"/>.</para>
/// <para>A categoria <c>OUTROS</c> nasce sem tipo algum: é escape para o que não
/// se enquadrar, e categoria de escape vazia é sinal de bom recorte.</para>
/// </remarks>
public static class TipoDocumentoSeed
{
    /// <summary>
    /// A hipótese de tratamento de todo documento semeado: o edital e a Lei nº 12.711/2012 regem a
    /// exigência de cada um (LGPD, art. 7º, II, e art. 11, II, a).
    /// </summary>
    public const HipoteseLegalTratamento HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    public const string FinalidadeTratamento =
        "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.";

    // Prefixo determinístico próprio do catálogo de tipos de documento (distinto
    // dos prefixos de categoria, modalidade, fato do candidato, valor de domínio e
    // precedência de fase, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"d0c00000-0000-7000-8000-{n:D12}");

    /// <summary>
    /// Os noventa e dois tipos, agrupados pela categoria que os classifica e na ordem em
    /// que o catálogo consolidado os apresenta.
    /// </summary>
    public static IReadOnlyList<TipoDocumentoSeedItem> Itens { get; } =
    [
        // IDENTIFICACAO
        new(SeedId(1), "RG", "RG", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(2), "CPF", "CPF", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(3), "TITULO_ELEITOR", "Título de eleitor", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(4), "QUITACAO_ELEITORAL", "Comprovante de quitação eleitoral", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(5), "QUITACAO_SERVICO_MILITAR", "Quitação com o serviço militar", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(6), "CERTIDAO_NASCIMENTO", "Certidão de nascimento", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(7), "CERTIDAO_CASAMENTO", "Certidão de casamento", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(8), "FOTO_3X4", "Foto 3x4", "IDENTIFICACAO", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(9), "FOTO_FRENTE", "Foto de frente", "IDENTIFICACAO", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(10), "FOTO_PERFIL_DIREITO", "Foto de perfil do lado direito", "IDENTIFICACAO", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(11), "FOTO_PERFIL_ESQUERDO", "Foto de perfil do lado esquerdo", "IDENTIFICACAO", ClassificacaoDeProtecao.Pessoal),

        // ESCOLARIDADE
        new(SeedId(12), "HISTORICO_ENSINO_FUNDAMENTAL", "Histórico do ensino fundamental", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(13), "HISTORICO_ENSINO_MEDIO", "Histórico do ensino médio", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(14), "CERTIFICADO_ENSINO_MEDIO", "Certificado de conclusão do ensino médio", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(15), "HISTORICO_GRADUACAO", "Histórico de graduação", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(16), "DIPLOMA_GRADUACAO", "Diploma de graduação", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(17), "DIPLOMA_POS_GRADUACAO", "Diploma de mestrado ou doutorado", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(18), "MATRIZ_CURRICULAR", "Matriz curricular", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(19), "DECLARACAO_VINCULO_INSTITUCIONAL", "Declaração de vínculo institucional", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),

        // RENDA
        new(SeedId(20), "CONTRACHEQUE", "Contracheque", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(21), "CARTEIRA_TRABALHO", "Carteira de trabalho", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(22), "EXTRATO_FGTS", "Extrato do FGTS", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(23), "EXTRATO_BANCARIO_PF", "Extrato bancário de pessoa física", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(24), "EXTRATO_BANCARIO_PJ", "Extrato bancário de pessoa jurídica", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(25), "EXTRATO_PAGAMENTO_BENEFICIO", "Extrato de pagamento de benefício", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(26), "NOTA_FISCAL_VENDAS", "Nota fiscal de vendas", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(27), "GUIA_RECOLHIMENTO_INSS", "Guia de recolhimento ao INSS", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(28), "CONTRATO_LOCACAO_ARRENDAMENTO", "Contrato de locação ou arrendamento", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(29), "REGISTRATO_BACEN", "Registrato do Banco Central", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(30), "CADASTRO_UNICO", "Cadastro Único", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(31), "DECLARACAO_IRPF", "Declaração de IRPF", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(32), "DECLARACAO_ISENCAO_IRPF", "Declaração de isenção de IRPF", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(33), "DECLARACAO_TRIBUTARIA_PJ", "Declaração tributária de pessoa jurídica", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(34), "DECLARACAO_TRABALHADOR_RURAL", "Declaração de trabalhador rural", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(35), "DECLARACAO_TRABALHADOR_AUTONOMO", "Declaração de trabalhador autônomo", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(36), "DECLARACAO_ATIVIDADE_DO_LAR", "Declaração de atividade do lar", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(37), "DECLARACAO_AUSENCIA_RENDIMENTOS", "Declaração de ausência de rendimentos", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(38), "DECLARACAO_PENSAO_ALIMENTICIA", "Declaração de recebimento de pensão alimentícia", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(39), "DECLARACAO_RENDIMENTO_ALUGUEL", "Declaração de rendimentos de aluguel", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(40), "DECLARACAO_SEM_CONTA_BANCARIA", "Declaração de que não possui conta bancária", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(41), "COMPROVANTE_PAGAMENTO_TAXA", "Comprovante de pagamento da taxa de inscrição", "RENDA", ClassificacaoDeProtecao.Pessoal),

        // RACA_ETNIA
        new(SeedId(42), "AUTODECLARACAO_ETNICO_RACIAL", "Autodeclaração étnico-racial justificada", "RACA_ETNIA", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(43), "AUTODECLARACAO_INDIGENA", "Autodeclaração indígena", "RACA_ETNIA", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(44), "AUTODECLARACAO_QUILOMBOLA", "Autodeclaração quilombola", "RACA_ETNIA", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(45), "DECLARACAO_PERTENCIMENTO_INDIGENA", "Declaração de pertencimento indígena", "RACA_ETNIA", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(46), "DECLARACAO_PERTENCIMENTO_QUILOMBOLA", "Declaração de pertencimento quilombola", "RACA_ETNIA", ClassificacaoDeProtecao.Sensivel),

        // SAUDE
        new(SeedId(47), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(48), "COMPROVANTE_VACINACAO", "Comprovante de vacinação", "SAUDE", ClassificacaoDeProtecao.Sensivel),

        // RESIDENCIA
        new(SeedId(49), "COMPROVANTE_RESIDENCIA", "Comprovante de residência", "RESIDENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(50), "DECLARACAO_RESIDENCIA", "Declaração de residência", "RESIDENCIA", ClassificacaoDeProtecao.Pessoal),

        // DOCUMENTO_PROCESSUAL
        new(SeedId(51), "REQUERIMENTO_INSCRICAO", "Requerimento de inscrição", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(52), "REQUERIMENTO_NOME_SOCIAL", "Requerimento de inclusão de nome social", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(53), "REQUERIMENTO_DESISTENCIA_VAGA", "Requerimento de desistência de vaga", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(54), "RECURSO_ADMINISTRATIVO", "Recurso administrativo", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(55), "PROCURACAO", "Procuração", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(56), "TERMO_ACEITE", "Termo de aceite", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(57), "TERMO_COMPROMISSO", "Termo de compromisso", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(58), "DECLARACAO_AUTENTICIDADE", "Declaração de autenticidade dos documentos", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(59), "DECLARACAO_DISPONIBILIDADE", "Declaração de disponibilidade", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(60), "FORMULARIO_OPCAO_ENTREVISTA", "Formulário de escolha do formato de entrevista", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),

        // TITULACAO_EXPERIENCIA
        new(SeedId(61), "DECLARACAO_FUNCIONAL_SIG", "Declaração funcional emitida pelo SIG", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(62), "COMPROVACAO_EXPERIENCIA_PROFISSIONAL", "Comprovação de experiência profissional", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(63), "COMPROVACAO_EXPERIENCIA_ESCOLA_CAMPO", "Comprovação de experiência profissional em escola do campo", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(64), "COMPROVACAO_ATIVIDADE_POVOS_TRADICIONAIS", "Comprovação de atividade junto a povos tradicionais", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(65), "COMPROVACAO_VINCULO_DOCENTE_REDE_PUBLICA", "Comprovação de vínculo docente na rede pública", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(66), "COMPROVACAO_ATUACAO_APOIO_EDUCACAO_ESPECIAL", "Comprovação de atuação como profissional de apoio da educação especial", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(67), "COMPROVACAO_LATTES_PESQUISA", "Comprovação de pesquisa concluída no currículo Lattes", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(68), "COMPROVACAO_LATTES_EXTENSAO_ENSINO", "Comprovação de extensão e ensino no currículo Lattes", "TITULACAO_EXPERIENCIA", ClassificacaoDeProtecao.Pessoal),

        // PRODUCAO_AVALIATIVA
        new(SeedId(69), "CARTA_INTENCAO", "Carta de intenção", "PRODUCAO_AVALIATIVA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(70), "RELATO_HISTORIA_VIDA", "Relato de história de vida", "PRODUCAO_AVALIATIVA", ClassificacaoDeProtecao.Pessoal),

        // Documentos que o edital de Medicina 2027 pede e o catálogo consolidado não tinha.
        new(SeedId(71), "CIN", "Carteira de Identidade Nacional", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador, TipoEquivalente: "RG"),
        new(SeedId(72), "CNH", "Carteira Nacional de Habilitação", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(73), "PASSAPORTE", "Passaporte", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(74), "RNM", "Registro Nacional Migratório", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
        new(SeedId(75), "BOLETIM_OCORRENCIA", "Boletim de ocorrência", "IDENTIFICACAO", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(76), "DOCUMENTO_JUDICIAL_GUARDA", "Documento judicial de guarda", "IDENTIFICACAO", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(77), "DECLARACAO_CONCLUSAO_ENSINO_MEDIO", "Atestado ou declaração de conclusão do ensino médio", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(78), "CNIS", "Extrato do Cadastro Nacional de Informações Sociais (CNIS)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(79), "DECLARACAO_IRPJ", "Declaração de IRPJ", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(80), "ECF", "Escrituração Contábil Fiscal (ECF)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(81), "DECORE", "Declaração Comprobatória de Percepção de Rendimentos (DECORE)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(82), "DEFIS", "Declaração de Informações Socioeconômicas e Fiscais (DEFIS)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(83), "DASN_SIMEI", "Declaração Anual do Simples Nacional do MEI (DASN-SIMEI)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(84), "DECLARACAO_INATIVIDADE_EMPRESA", "Declaração de inatividade da empresa", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(85), "DECLARACAO_RENDIMENTOS_AUFERIDOS", "Declaração de rendimentos auferidos", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(86), "DECLARACAO_AUSENCIA_RENDIMENTOS_ALUGUEL", "Declaração de ausência de rendimentos de aluguel", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(87), "EXTRATO_PREVIDENCIA_PRIVADA", "Extrato de benefício de previdência privada", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(88), "TRCT", "Termo de rescisão do contrato de trabalho (TRCT)", "RENDA", ClassificacaoDeProtecao.Pessoal),
        new(SeedId(89), "LAUDO_PSICOLOGICO", "Laudo psicológico", "SAUDE", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(90), "EXAME_AUDIOMETRIA", "Exame de audiometria", "SAUDE", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(91), "EXAME_OFTALMOLOGICO", "Exame oftalmológico com acuidade visual", "SAUDE", ClassificacaoDeProtecao.Sensivel),
        new(SeedId(92), "TERMO_RESPONSABILIDADE_CERTIFICADO", "Termo de responsabilidade pela entrega do certificado de conclusão", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
    ];
}

/// <summary>
/// Definição de um tipo do seed (fonte única), na forma da entidade
/// <c>TipoDocumento</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio — formato do código,
/// tamanho do nome, forma da categoria e proteção — é garantida por teste, que revalida cada
/// item pela própria factory.
/// </summary>
public sealed record TipoDocumentoSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    string Categoria,
    ClassificacaoDeProtecao ClassificacaoProtecao,
    string? TipoEquivalente = null);
