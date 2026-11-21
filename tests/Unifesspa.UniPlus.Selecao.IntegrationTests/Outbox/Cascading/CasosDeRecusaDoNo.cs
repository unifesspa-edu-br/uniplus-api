namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

internal sealed record CasoDeRecusaDoNo(object[] Nos, string Codigo, string Campo, string Mensagem);

internal static class CasosDeRecusaDoNo
{
    private const string Prefixo = "uniplus.selecao.no_exigencia.";
    private const string Item = "No item 1 da lista: ";

    internal static IReadOnlyList<CasoDeRecusaDoNo> Todos() => [.. Estrutura(), .. Quantidades(), .. ConsequenciasEBases(), .. DatasDaDistincao(), .. OcorrenciasDaDistincao(), Repeticao()];

    private static IReadOnlyList<CasoDeRecusaDoNo> Estrutura() =>
    [
        Caso(new { tipo = "XOR" }, "tipo_invalido", "tipo",
            "O tipo informado não é reconhecido. Escolha \"FOLHA\" para um documento, \"E\" para exigir todos os itens ou \"OU\" para exigir uma quantidade deles."),
        Caso(FolhaSemDocumento(), "documento_obrigatorio_em_folha", "documento",
            "Este item é um documento (\"FOLHA\"), mas não informa qual documento é exigido. Escolha o documento no catálogo."),
        Caso(Grupo("E", documento: Documento()), "documento_proibido_em_grupo", "documento",
            "Este grupo reúne exigências e não pode informar um documento próprio. Retire o documento do grupo e inclua-o como item separado."),
        Caso(Folha(filhos: [Folha()]), "filhos_proibidos_em_folha", "filhos",
            "Este item é um documento (\"FOLHA\") e não pode reunir outros itens. Retire os itens reunidos ou escolha um grupo."),
        Caso(GrupoVazio(), "grupo_vazio", "filhos", "Este grupo não contém itens. Acrescente ao menos um item ao grupo."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> Quantidades() =>
    [
        Caso(Grupo("E", quantidade: 1), "quantidade_minima_proibida_em_grupo_e", "quantidadeMinima",
            "Este grupo exige todos os itens (\"E\"), mas informa uma quantidade mínima. Retire a quantidade mínima."),
        Caso(Grupo("OU", quantidade: 0), "quantidade_minima_fora_dos_limites", "quantidadeMinima",
            "A quantidade mínima deste grupo (\"OU\") está fora dos limites. Informe um valor entre 1 e o número de itens do grupo."),
        Caso(Folha(quantidade: 0), "quantidade_minima_de_folha_invalida", "quantidadeMinima",
            "A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> ConsequenciasEBases() =>
    [
        Caso(Grupo("E", consequencia: "ELIMINA"), "consequencia_proibida_em_grupo_e", "consequencia",
            "Este grupo exige todos os itens (\"E\"), mas informa uma consequência própria. Retire a consequência do grupo."),
        Caso(Grupo("E", bases: [BaseLegal()]), "base_legal_proibida_em_grupo_e", "basesLegais",
            "Este grupo exige todos os itens (\"E\"), mas informa base legal própria. Retire a base legal do grupo."),
        Caso(Grupo("OU", consequencia: "INVALIDA"), "consequencia_invalida", "consequencia",
            "A consequência deste grupo não é reconhecida. Escolha \"ELIMINA\" para retirar a pessoa do processo, \"RECLASSIFICA_AC\" para a ampla concorrência, \"REMOVE_VANTAGEM\" para retirar a vantagem ou \"PENDENCIA_REENVIO\" para permitir reenvio."),
        Caso(Grupo("OU", bases: [BaseLegal()]), "base_legal_sem_consequencia", "basesLegais",
            "Este grupo tem base legal, mas não tem consequência. Informe a consequência ou retire a base legal."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> DatasDaDistincao() =>
    [
        Caso(Folha(chave: "INVALIDA"), "chave_distincao_invalida", "chaveDistincao",
            "O critério para distinguir as apresentações não é reconhecido. Escolha \"COMPETENCIA_MENSAL\" para mês, \"EXERCICIO_ANUAL\" para ano ou \"OCORRENCIA\" para ocorrência."),
        Caso(Folha(chave: "OCORRENCIA", data: new DateOnly(2025, 1, 1)), "data_referencia_indevida_para_chave", "dataReferencia",
            "A data de referência não se aplica ao critério escolhido. Retire a data ou escolha um critério por mês ou ano."),
        Caso(Folha(chave: "COMPETENCIA_MENSAL"), "data_referencia_obrigatoria_para_chave_calendario", "dataReferencia",
            "Este documento é distinguido por mês ou ano e precisa de uma data de referência. Informe a data."),
        Caso(Folha(chave: "COMPETENCIA_MENSAL", data: new DateOnly(2025, 1, 1), ocorrencias: ["A"]),
            "ocorrencias_esperadas_indevidas_para_chave", "ocorrenciasEsperadas",
            "A lista de ocorrências esperadas não se aplica ao critério escolhido. Retire a lista ou escolha \"OCORRENCIA\" para distinguir por ocorrência."),
        Caso(Folha(quantidade: 2, chave: "EXERCICIO_ANUAL", data: DateOnly.MinValue),
            "quantidade_minima_excede_janela_representavel", "quantidadeMinima",
            "A quantidade mínima alcança datas anteriores ao início do calendário. Reduza a quantidade ou escolha outra data de referência."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> OcorrenciasDaDistincao() =>
    [
        Caso(Folha(chave: "OCORRENCIA", ocorrencias: []), "ocorrencias_esperadas_vazia", "ocorrenciasEsperadas",
            "A lista de ocorrências esperadas está vazia. Informe ao menos uma ocorrência ou deixe a lista ausente."),
        Caso(Folha(chave: "OCORRENCIA", ocorrencias: [""]), "ocorrencias_esperadas_com_id_vazio", "ocorrenciasEsperadas",
            "Uma ocorrência esperada não tem identificador. Informe um identificador para cada ocorrência."),
        Caso(Folha(quantidade: 2, chave: "OCORRENCIA", ocorrencias: ["A", "A"]), "ocorrencias_esperadas_com_ids_duplicados", "ocorrenciasEsperadas",
            "Há ocorrências esperadas com o mesmo identificador. Informe identificadores distintos."),
        Caso(Folha(quantidade: 2, chave: "OCORRENCIA", ocorrencias: ["A"]), "ocorrencias_esperadas_quantidade_minima_divergente", "quantidadeMinima",
            "A quantidade mínima não coincide com o número de ocorrências esperadas. Ajuste a quantidade ou a lista."),
    ];

    private static CasoDeRecusaDoNo Repeticao() =>
        Caso(Folha(repete: "GRUPO_INEXISTENTE"), "tipo_entidade_invalido", "repetePorEntidade",
            "O grupo de repetição informado não existe nos formulários do processo. Escolha um grupo existente ou retire a repetição.");

    internal static object GrupoCom(object[] filhos) => new { tipo = "E", filhos };
    internal static object FolhaSemDocumento() => new { tipo = "FOLHA" };
    internal static object GrupoVazio() => new { tipo = "OU", filhos = Array.Empty<object>() };

    private static CasoDeRecusaDoNo Caso(object no, string codigo, string campo, string mensagem) =>
        new([no], Prefixo + codigo, $"nos[0].{campo}", Item + mensagem);

    private static object Grupo(string tipo, object? documento = null, int? quantidade = null, string? consequencia = null, object[]? bases = null) =>
        new { tipo, documento, quantidadeMinima = quantidade, consequencia, basesLegais = bases, filhos = new[] { Folha() } };

    private static object Folha(int? quantidade = null, object[]? filhos = null, string? chave = null,
        DateOnly? data = null, string[]? ocorrencias = null, string? repete = null) =>
        new
        {
            tipo = "FOLHA",
            documento = Documento(),
            quantidadeMinima = quantidade,
            filhos,
            chaveDistincao = chave,
            dataReferencia = data,
            ocorrenciasEsperadas = ocorrencias,
            repetePorEntidade = repete
        };

    private static object Documento() => new
    {
        exigidoNaFaseId = Guid.CreateVersion7(),
        tipoDocumentoId = Guid.CreateVersion7(),
        aplicabilidade = "GERAL",
        obrigatorio = true,
        condicoes = Array.Empty<object>(),
        basesLegais = Array.Empty<object>(),
        formatosPermitidos = "QUALQUER",
    };

    private static object BaseLegal() => new { referencia = "Lei de exemplo", abrangencia = "FEDERAL", status = "RESOLVIDO" };
}
