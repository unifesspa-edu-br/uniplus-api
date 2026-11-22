namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

internal sealed record CasoDeRecusaDoNo(object[] Nos, string Codigo, string Campo, string Mensagem);

internal static class CasosDeRecusaDoNo
{
    private const string Prefixo = "uniplus.selecao.regra_de_comprovacao.";
    private const string Item = "No item 1 da lista: ";

    internal static IReadOnlyList<CasoDeRecusaDoNo> Todos() => [.. Estrutura(), .. Quantidades(), .. ConsequenciasEBases(), .. DatasDaDistincao(), .. OcorrenciasDaDistincao(), Repeticao()];

    private static IReadOnlyList<CasoDeRecusaDoNo> Estrutura() =>
    [
        Caso(new { forma = "XOR" }, "forma_invalida", "forma",
            "A forma de comprovação não é reconhecida. Escolha \"DOCUMENTO\", \"TODOS_OS_ITENS\" ou \"QUANTIDADE_MINIMA_DE_ITENS\"."),
        Caso(FolhaSemDocumento(), "documento_obrigatorio", "documento",
            "A regra de documento não informa qual documento é exigido. Escolha o documento no catálogo."),
        Caso(Grupo("TODOS_OS_ITENS", documento: Documento()), "documento_proibido_em_grupo", "documento",
            "Este grupo reúne exigências e não pode informar um documento próprio. Retire o documento do grupo e inclua-o como item separado."),
        Caso(Folha(itens: [Folha()]), "itens_proibidos_em_regra_de_documento", "itens",
            "A regra de documento não pode reunir outros itens. Retire os itens reunidos ou escolha um grupo."),
        Caso(GrupoVazio(), "grupo_sem_itens", "itens", "Este grupo não contém itens. Acrescente ao menos um item ao grupo."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> Quantidades() =>
    [
        Caso(Grupo("TODOS_OS_ITENS", quantidade: 1), "quantidade_minima_proibida_quando_exige_todos", "cardinalidade.quantidadeMinima",
            "Este grupo exige todos os itens, mas informa uma quantidade mínima. Retire a quantidade mínima."),
        Caso(Grupo("QUANTIDADE_MINIMA_DE_ITENS", quantidade: 0), "quantidade_minima_de_itens_fora_dos_limites", "cardinalidade.quantidadeMinima",
            "A quantidade mínima deste grupo está fora dos limites. Informe um valor entre 1 e o número de itens do grupo."),
        Caso(Folha(quantidade: 0), "quantidade_minima_de_apresentacoes_invalida", "cardinalidade.quantidadeMinima",
            "A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> ConsequenciasEBases() =>
    [
        Caso(Grupo("TODOS_OS_ITENS", consequencia: "ELIMINA"), "consequencia_proibida_quando_exige_todos", "indeferimento.consequencia",
            "Este grupo exige todos os itens, mas informa uma consequência própria. Retire a consequência do grupo."),
        Caso(Grupo("TODOS_OS_ITENS", bases: [BaseLegal()]), "base_legal_proibida_quando_exige_todos", "indeferimento.basesLegais",
            "Este grupo exige todos os itens, mas informa base legal própria. Retire a base legal do grupo."),
        Caso(Grupo("QUANTIDADE_MINIMA_DE_ITENS", consequencia: "INVALIDA"), "consequencia_invalida", "indeferimento.consequencia",
            "A consequência deste grupo não é reconhecida. Escolha \"ELIMINA\" para retirar a pessoa do processo, \"RECLASSIFICA_AC\" para a ampla concorrência, \"REMOVE_VANTAGEM\" para retirar a vantagem ou \"PENDENCIA_REENVIO\" para permitir reenvio."),
        Caso(Grupo("QUANTIDADE_MINIMA_DE_ITENS", bases: [BaseLegal()]), "base_legal_sem_consequencia", "indeferimento.basesLegais",
            "Este grupo tem base legal, mas não tem consequência. Informe a consequência ou retire a base legal."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> DatasDaDistincao() =>
    [
        Caso(Folha(chave: "INVALIDA"), "criterio_de_distincao_invalido", "cardinalidade.distincao.chave",
            "O critério para distinguir as apresentações não é reconhecido. Escolha \"COMPETENCIA_MENSAL\" para mês, \"EXERCICIO_ANUAL\" para ano ou \"OCORRENCIA\" para ocorrência."),
        Caso(Folha(chave: "OCORRENCIA", data: new DateOnly(2025, 1, 1)), "data_referencia_indevida_para_criterio", "cardinalidade.distincao.dataReferencia",
            "A data de referência não se aplica ao critério escolhido. Retire a data ou escolha um critério por mês ou ano."),
        Caso(Folha(chave: "COMPETENCIA_MENSAL"), "data_referencia_obrigatoria_para_calendario", "cardinalidade.distincao.dataReferencia",
            "Este documento é distinguido por mês ou ano e precisa de uma data de referência. Informe a data."),
        Caso(Folha(chave: "COMPETENCIA_MENSAL", data: new DateOnly(2025, 1, 1), ocorrencias: ["A"]),
            "ocorrencias_esperadas_indevidas_para_criterio", "cardinalidade.distincao.ocorrenciasEsperadas",
            "A lista de ocorrências esperadas não se aplica ao critério escolhido. Retire a lista ou escolha \"OCORRENCIA\" para distinguir por ocorrência."),
        Caso(Folha(quantidade: 2, chave: "EXERCICIO_ANUAL", data: DateOnly.MinValue),
            "quantidade_minima_excede_calendario", "cardinalidade.quantidadeMinima",
            "A quantidade mínima alcança datas anteriores ao início do calendário. Reduza a quantidade ou escolha outra data de referência."),
    ];

    private static IReadOnlyList<CasoDeRecusaDoNo> OcorrenciasDaDistincao() =>
    [
        Caso(Folha(chave: "OCORRENCIA", ocorrencias: []), "ocorrencias_esperadas_vazias", "cardinalidade.distincao.ocorrenciasEsperadas",
            "A lista de ocorrências esperadas está vazia. Informe ao menos uma ocorrência ou deixe a lista ausente."),
        Caso(Folha(chave: "OCORRENCIA", ocorrencias: [""]), "ocorrencia_sem_identificador", "cardinalidade.distincao.ocorrenciasEsperadas",
            "Uma ocorrência esperada não tem identificador. Informe um identificador para cada ocorrência."),
        Caso(Folha(quantidade: 2, chave: "OCORRENCIA", ocorrencias: ["A", "A"]), "ocorrencia_com_identificador_repetido", "cardinalidade.distincao.ocorrenciasEsperadas",
            "Há ocorrências esperadas com o mesmo identificador. Informe identificadores distintos."),
        Caso(Folha(quantidade: 2, chave: "OCORRENCIA", ocorrencias: ["A"]), "quantidade_minima_diferente_das_ocorrencias", "cardinalidade.quantidadeMinima",
            "A quantidade mínima não coincide com o número de ocorrências esperadas. Ajuste a quantidade ou a lista."),
    ];

    private static CasoDeRecusaDoNo Repeticao() =>
        Caso(Folha(repete: "GRUPO_INEXISTENTE"), "grupo_para_repeticao_nao_declarado", "grupoDoFormularioParaRepeticao",
            "O grupo de repetição informado não existe nos formulários do processo. Escolha um grupo existente ou retire a repetição.");

    internal static object GrupoCom(object[] filhos) => new { forma = "TODOS_OS_ITENS", itens = filhos };
    internal static object FolhaSemDocumento() => new { forma = "DOCUMENTO" };
    internal static object GrupoVazio() => new { forma = "QUANTIDADE_MINIMA_DE_ITENS", itens = Array.Empty<object>() };

    private static CasoDeRecusaDoNo Caso(object no, string codigo, string campo, string mensagem) =>
        new([no], Prefixo + codigo, $"regrasDeComprovacao[0].{campo}", Item + mensagem);

    private static object Grupo(string tipo, object? documento = null, int? quantidade = null, string? consequencia = null, object[]? bases = null) =>
        new { forma = tipo, documento, cardinalidade = new { quantidadeMinima = quantidade }, indeferimento = new { consequencia, basesLegais = bases }, itens = new[] { Folha() } };

    private static object Folha(int? quantidade = null, object[]? itens = null, string? chave = null,
        DateOnly? data = null, string[]? ocorrencias = null, string? repete = null) =>
        new
        {
            forma = "DOCUMENTO",
            documento = Documento(),
            cardinalidade = new
            {
                quantidadeMinima = quantidade,
                distincao = chave is null && data is null && ocorrencias is null ? null : new { chave, dataReferencia = data, ocorrenciasEsperadas = ocorrencias },
            },
            itens,
            grupoDoFormularioParaRepeticao = repete
        };

    private static object Documento() => new
    {
        tipoDocumento = new { id = Guid.CreateVersion7() },
        momento = new { faseId = Guid.CreateVersion7() },
        aplicabilidade = new { tipo = "GERAL", condicoes = Array.Empty<object>() },
        indeferimento = new { obrigatorio = true, basesLegais = Array.Empty<object>() },
        requisitos = new { formatosPermitidos = "QUALQUER" },
    };

    private static object BaseLegal() => new { referencia = "Lei de exemplo", abrangencia = "FEDERAL", situacao = "RESOLVIDO" };
}
