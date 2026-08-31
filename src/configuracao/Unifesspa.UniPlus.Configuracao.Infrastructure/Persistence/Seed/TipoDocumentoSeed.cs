namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Fonte única do seed do catálogo de tipos de documento (UNI-REQ-0013) — os
/// setenta tipos consolidados dos dois sistemas legados. Consumida tanto pela
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
    // Prefixo determinístico próprio do catálogo de tipos de documento (distinto
    // dos prefixos de categoria, modalidade, fato do candidato, valor de domínio e
    // precedência de fase, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"d0c00000-0000-7000-8000-{n:D12}");

    /// <summary>
    /// Os setenta tipos, agrupados pela categoria que os classifica e na ordem em
    /// que o catálogo consolidado os apresenta.
    /// </summary>
    public static IReadOnlyList<TipoDocumentoSeedItem> Itens { get; } =
    [
        // IDENTIFICACAO
        new(SeedId(1), "RG", "RG", "IDENTIFICACAO"),
        new(SeedId(2), "CPF", "CPF", "IDENTIFICACAO"),
        new(SeedId(3), "TITULO_ELEITOR", "Título de eleitor", "IDENTIFICACAO"),
        new(SeedId(4), "QUITACAO_ELEITORAL", "Comprovante de quitação eleitoral", "IDENTIFICACAO"),
        new(SeedId(5), "QUITACAO_SERVICO_MILITAR", "Quitação com o serviço militar", "IDENTIFICACAO"),
        new(SeedId(6), "CERTIDAO_NASCIMENTO", "Certidão de nascimento", "IDENTIFICACAO"),
        new(SeedId(7), "CERTIDAO_CASAMENTO", "Certidão de casamento", "IDENTIFICACAO"),
        new(SeedId(8), "FOTO_3X4", "Foto 3x4", "IDENTIFICACAO"),
        new(SeedId(9), "FOTO_FRENTE", "Foto de frente", "IDENTIFICACAO"),
        new(SeedId(10), "FOTO_PERFIL_DIREITO", "Foto de perfil do lado direito", "IDENTIFICACAO"),
        new(SeedId(11), "FOTO_PERFIL_ESQUERDO", "Foto de perfil do lado esquerdo", "IDENTIFICACAO"),

        // ESCOLARIDADE
        new(SeedId(12), "HISTORICO_ENSINO_FUNDAMENTAL", "Histórico do ensino fundamental", "ESCOLARIDADE"),
        new(SeedId(13), "HISTORICO_ENSINO_MEDIO", "Histórico do ensino médio", "ESCOLARIDADE"),
        new(SeedId(14), "CERTIFICADO_ENSINO_MEDIO", "Certificado de conclusão do ensino médio", "ESCOLARIDADE"),
        new(SeedId(15), "HISTORICO_GRADUACAO", "Histórico de graduação", "ESCOLARIDADE"),
        new(SeedId(16), "DIPLOMA_GRADUACAO", "Diploma de graduação", "ESCOLARIDADE"),
        new(SeedId(17), "DIPLOMA_POS_GRADUACAO", "Diploma de mestrado ou doutorado", "ESCOLARIDADE"),
        new(SeedId(18), "MATRIZ_CURRICULAR", "Matriz curricular", "ESCOLARIDADE"),
        new(SeedId(19), "DECLARACAO_VINCULO_INSTITUCIONAL", "Declaração de vínculo institucional", "ESCOLARIDADE"),

        // RENDA
        new(SeedId(20), "CONTRACHEQUE", "Contracheque", "RENDA"),
        new(SeedId(21), "CARTEIRA_TRABALHO", "Carteira de trabalho", "RENDA"),
        new(SeedId(22), "EXTRATO_FGTS", "Extrato do FGTS", "RENDA"),
        new(SeedId(23), "EXTRATO_BANCARIO_PF", "Extrato bancário de pessoa física", "RENDA"),
        new(SeedId(24), "EXTRATO_BANCARIO_PJ", "Extrato bancário de pessoa jurídica", "RENDA"),
        new(SeedId(25), "EXTRATO_PAGAMENTO_BENEFICIO", "Extrato de pagamento de benefício", "RENDA"),
        new(SeedId(26), "NOTA_FISCAL_VENDAS", "Nota fiscal de vendas", "RENDA"),
        new(SeedId(27), "GUIA_RECOLHIMENTO_INSS", "Guia de recolhimento ao INSS", "RENDA"),
        new(SeedId(28), "CONTRATO_LOCACAO_ARRENDAMENTO", "Contrato de locação ou arrendamento", "RENDA"),
        new(SeedId(29), "REGISTRATO_BACEN", "Registrato do Banco Central", "RENDA"),
        new(SeedId(30), "CADASTRO_UNICO", "Cadastro Único", "RENDA"),
        new(SeedId(31), "DECLARACAO_IRPF", "Declaração de IRPF", "RENDA"),
        new(SeedId(32), "DECLARACAO_ISENCAO_IRPF", "Declaração de isenção de IRPF", "RENDA"),
        new(SeedId(33), "DECLARACAO_TRIBUTARIA_PJ", "Declaração tributária de pessoa jurídica", "RENDA"),
        new(SeedId(34), "DECLARACAO_TRABALHADOR_RURAL", "Declaração de trabalhador rural", "RENDA"),
        new(SeedId(35), "DECLARACAO_TRABALHADOR_AUTONOMO", "Declaração de trabalhador autônomo", "RENDA"),
        new(SeedId(36), "DECLARACAO_ATIVIDADE_DO_LAR", "Declaração de atividade do lar", "RENDA"),
        new(SeedId(37), "DECLARACAO_AUSENCIA_RENDIMENTOS", "Declaração de ausência de rendimentos", "RENDA"),
        new(SeedId(38), "DECLARACAO_PENSAO_ALIMENTICIA", "Declaração de recebimento de pensão alimentícia", "RENDA"),
        new(SeedId(39), "DECLARACAO_RENDIMENTO_ALUGUEL", "Declaração de rendimentos de aluguel", "RENDA"),
        new(SeedId(40), "DECLARACAO_SEM_CONTA_BANCARIA", "Declaração de que não possui conta bancária", "RENDA"),
        new(SeedId(41), "COMPROVANTE_PAGAMENTO_TAXA", "Comprovante de pagamento da taxa de inscrição", "RENDA"),

        // RACA_ETNIA
        new(SeedId(42), "AUTODECLARACAO_ETNICO_RACIAL", "Autodeclaração étnico-racial justificada", "RACA_ETNIA"),
        new(SeedId(43), "AUTODECLARACAO_INDIGENA", "Autodeclaração indígena", "RACA_ETNIA"),
        new(SeedId(44), "AUTODECLARACAO_QUILOMBOLA", "Autodeclaração quilombola", "RACA_ETNIA"),
        new(SeedId(45), "DECLARACAO_PERTENCIMENTO_INDIGENA", "Declaração de pertencimento indígena", "RACA_ETNIA"),
        new(SeedId(46), "DECLARACAO_PERTENCIMENTO_QUILOMBOLA", "Declaração de pertencimento quilombola", "RACA_ETNIA"),

        // SAUDE
        new(SeedId(47), "LAUDO_MEDICO", "Laudo médico", "SAUDE"),
        new(SeedId(48), "COMPROVANTE_VACINACAO", "Comprovante de vacinação", "SAUDE"),

        // RESIDENCIA
        new(SeedId(49), "COMPROVANTE_RESIDENCIA", "Comprovante de residência", "RESIDENCIA"),
        new(SeedId(50), "DECLARACAO_RESIDENCIA", "Declaração de residência", "RESIDENCIA"),

        // DOCUMENTO_PROCESSUAL
        new(SeedId(51), "REQUERIMENTO_INSCRICAO", "Requerimento de inscrição", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(52), "REQUERIMENTO_NOME_SOCIAL", "Requerimento de inclusão de nome social", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(53), "REQUERIMENTO_DESISTENCIA_VAGA", "Requerimento de desistência de vaga", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(54), "RECURSO_ADMINISTRATIVO", "Recurso administrativo", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(55), "PROCURACAO", "Procuração", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(56), "TERMO_ACEITE", "Termo de aceite", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(57), "TERMO_COMPROMISSO", "Termo de compromisso", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(58), "DECLARACAO_AUTENTICIDADE", "Declaração de autenticidade dos documentos", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(59), "DECLARACAO_DISPONIBILIDADE", "Declaração de disponibilidade", "DOCUMENTO_PROCESSUAL"),
        new(SeedId(60), "FORMULARIO_OPCAO_ENTREVISTA", "Formulário de escolha do formato de entrevista", "DOCUMENTO_PROCESSUAL"),

        // TITULACAO_EXPERIENCIA
        new(SeedId(61), "DECLARACAO_FUNCIONAL_SIG", "Declaração funcional emitida pelo SIG", "TITULACAO_EXPERIENCIA"),
        new(SeedId(62), "COMPROVACAO_EXPERIENCIA_PROFISSIONAL", "Comprovação de experiência profissional", "TITULACAO_EXPERIENCIA"),
        new(SeedId(63), "COMPROVACAO_EXPERIENCIA_ESCOLA_CAMPO", "Comprovação de experiência profissional em escola do campo", "TITULACAO_EXPERIENCIA"),
        new(SeedId(64), "COMPROVACAO_ATIVIDADE_POVOS_TRADICIONAIS", "Comprovação de atividade junto a povos tradicionais", "TITULACAO_EXPERIENCIA"),
        new(SeedId(65), "COMPROVACAO_VINCULO_DOCENTE_REDE_PUBLICA", "Comprovação de vínculo docente na rede pública", "TITULACAO_EXPERIENCIA"),
        new(SeedId(66), "COMPROVACAO_ATUACAO_APOIO_EDUCACAO_ESPECIAL", "Comprovação de atuação como profissional de apoio da educação especial", "TITULACAO_EXPERIENCIA"),
        new(SeedId(67), "COMPROVACAO_LATTES_PESQUISA", "Comprovação de pesquisa concluída no currículo Lattes", "TITULACAO_EXPERIENCIA"),
        new(SeedId(68), "COMPROVACAO_LATTES_EXTENSAO_ENSINO", "Comprovação de extensão e ensino no currículo Lattes", "TITULACAO_EXPERIENCIA"),

        // PRODUCAO_AVALIATIVA
        new(SeedId(69), "CARTA_INTENCAO", "Carta de intenção", "PRODUCAO_AVALIATIVA"),
        new(SeedId(70), "RELATO_HISTORIA_VIDA", "Relato de história de vida", "PRODUCAO_AVALIATIVA"),
    ];
}

/// <summary>
/// Definição de um tipo do seed (fonte única), na forma da entidade
/// <c>TipoDocumento</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio — formato do código,
/// tamanho do nome, forma da categoria — é garantida por teste, que revalida cada
/// item pela própria factory.
/// </summary>
public sealed record TipoDocumentoSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    string Categoria);
