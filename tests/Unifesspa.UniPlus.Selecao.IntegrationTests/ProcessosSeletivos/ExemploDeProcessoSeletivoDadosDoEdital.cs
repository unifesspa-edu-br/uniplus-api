namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoVagas;

internal static class ExemploDeProcessoSeletivoDadosDoEdital
{
    /// <summary>
    /// O período espelha a janela da fase que coleta inscrição (<c>INSCRICAO</c>), porque é dela
    /// que a publicação o projeta.
    /// </summary>
    /// <remarks>
    /// Declarar o fim em data diferente da do fim da fase (por exemplo, 15/04 contra 20/03) é um
    /// estado que o sistema não produz. A fixture nesse formato congelaria a prova de um certame
    /// impossível.
    /// </remarks>
    internal static DadosEdital DadosRicos() => DadosEdital.Criar(
        numero: "042/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        periodoInscricaoFim: FimDasInscricoes,
        documentoEditalId: Documento,
        anexos: AnexosRicos).Value!;

    /// <summary>
    /// Os anexos do edital rico: um PDF e um DOCX, de formatos e tamanhos diferentes, para a fixture
    /// provar a ordem em que o edital os apresenta e o tipo de mídia de cada um.
    /// </summary>
    internal static readonly AnexoDoEdital[] AnexosRicos =
    [
        new(
            new Guid("0192a000-0000-7000-8000-0000000000e1"),
            new Guid("0192a000-0000-7000-8000-0000000000f1"),
            "Cronograma detalhado.pdf",
            FormatoDeDocumento.Pdf,
            48_213,
            new string('b', 64)),
        new(
            new Guid("0192a000-0000-7000-8000-0000000000e2"),
            new Guid("0192a000-0000-7000-8000-0000000000f2"),
            "Quadro de vagas.docx",
            FormatoDeDocumento.Docx,
            19_877,
            new string('c', 64)),
    ];

    /// <summary>
    /// Os valores selecionáveis dos dois fatos coletados de <see cref="ProcessoRico"/>
    /// (COR_RACA, RENDA — ambos <c>ESCOLHA_UNICA</c>, UNI-REQ-0072): totalidade exigida pelo
    /// encoder (D1-ter) desde que qualquer processo colete um fato de seleção. Ordens não-zero
    /// e fora de ordem de código de propósito — a ordenação canônica (Ordem, depois Codigo) é
    /// responsabilidade do ENCODER, não de quem monta este dicionário.
    /// </summary>
    /// <param name="permutarValores">
    /// Inverte a ordem de inserção de cada lista sem mudar o conteúdo — a prova de que os
    /// bytes finais dependem só do <c>Ordem</c>/<c>Codigo</c> de cada valor, nunca da ordem em
    /// que a lista chegou ao encoder.
    /// </param>
    internal static IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveisRicos(
        bool permutarValores = false)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = NaOrdemDeInsercao(ValoresDeCorRaca(), permutarValores),
            ["RENDA"] = NaOrdemDeInsercao(ValoresDeRenda(), permutarValores),
            [CandidatoComoMembro.FatoParentesco] = NaOrdemDeInsercao(ValoresDeParentesco(), permutarValores),
            ["NATURALIDADE_UF"] = NaOrdemDeInsercao(ValoresDeUf(), permutarValores),
            // As opções do processo vêm das edições declaradas, que o processo rico admite.
            [EdicaoDoEnem.Fato] = EdicoesDoEnemDoExemplo(),
        };

        return CatalogoDoConjuntoBasico.ComValoresCongelados(valores);
    }

    private static List<ValorDominioDeclaradoCongelado> EdicoesDoEnemDoExemplo() =>
    [
        new("2024", "2024", 0),
        new("2025", "2025", 1),
        new("2026", "2026", 2),
    ];

    private static List<ValorDominioDeclaradoCongelado> NaOrdemDeInsercao(List<ValorDominioDeclaradoCongelado> valores, bool permutar)
    {
        if (permutar)
        {
            valores.Reverse();
        }

        return valores;
    }

    private static List<ValorDominioDeclaradoCongelado> ValoresDeCorRaca() =>
    [
        new("BRANCA", "Autodeclaração de cor/raça branca.", 0),
        new("PRETA", "Preta", 1, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação."),
        new("PARDA", "Autodeclaração de cor/raça parda.", 2),
    ];

    private static List<ValorDominioDeclaradoCongelado> ValoresDeRenda() =>
    [
        new("ATE_1_SM", "Renda familiar per capita de até 1 salário mínimo.", 0),
        new("ACIMA_1_SM", "Renda familiar per capita acima de 1 salário mínimo.", 1),
    ];

    private static List<ValorDominioDeclaradoCongelado> ValoresDeParentesco() =>
    [
        new(CandidatoComoMembro.ProprioCandidato, "O próprio candidato.", 0),
        new("PAI_OU_MAE", "Pai ou mãe do candidato.", 1),
    ];

    private static List<ValorDominioDeclaradoCongelado> ValoresDeUf() =>
    [
        new("AC", "Acre", 0),
        new("PA", "Pará", 1),
    ];

    /// <summary>
    /// O grupo repetível do exemplo rico (UNI-REQ-0146): exibido por um fato do candidato, com um
    /// campo exibido pelo campo anterior da mesma ocorrência — o decoder remonta o grupo, as regras
    /// dele e os campos, e o grafo conjunto liga os campos às regras. Sem máximo, o envelope grava
    /// o máximo nulo; o grupo inclui o candidato, identificado pelo parentesco.
    /// </summary>
    internal static GrupoColetado ComposicaoFamiliar(bool permutar) => GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 2)
        .ComOcorrencias(1, null)
        .IncluindoOCandidato()
        .ExibidoQuando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!)
        .ComCampos(Ordem([TrabalhadorRural(), MenorSobGuarda(), Parentesco()], permutar))
        .Construir();

    private static FatoColetado TrabalhadorRural() => FatoColetadoDeTeste.Coletando("TRABALHADOR_RURAL", 0)
        .ComRotulo("Trabalha no campo")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .Construir();

    /// <summary>
    /// A classificação Identificador existe só para provar que o codec a carrega também em subitem de grupo: não tem
    /// relação com o conteúdo real do fato.
    /// </summary>
    private static FatoColetado MenorSobGuarda() => FatoColetadoDeTeste.Coletando("MENOR_SOB_GUARDA", 1)
        .ComRotulo("Menor sob guarda")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ExibidoQuando(ExibicaoDeTeste.Quando("TRABALHADOR_RURAL", Operador.Igual, false))
        .ComClassificacao("IDENTIFICADOR")
        .Construir();

    private static FatoColetado Parentesco() => FatoColetadoDeTeste.Coletando(CandidatoComoMembro.FatoParentesco, 2)
        .ComRotulo("Parentesco")
        .DoTipo(TipoDeCampo.EscolhaUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .Construir();

    /// <summary>
    /// Calendário congelado do exemplo rico (UNI-REQ-0080). Traz <b>uma data de cada abrangência</b>
    /// de propósito: um exemplo só com feriado nacional deixaria as três formas territoriais sem
    /// golden, e a recusa de combinação inválida no decoder passaria a ser provada apenas por
    /// mutação, nunca por artefato.
    /// </summary>
    /// <remarks>
    /// O id é fixo, como os demais ids do exemplo: gerado a cada execução, a golden fixture rica
    /// não teria como existir. A data municipal é de Marabá, a localidade regente do exemplo.
    /// </remarks>
    internal static CalendarioDiasUteisCongelado CalendarioRico() =>
        CalendarioDiasUteisCongelado.Criar(
            Guid.Parse("01930000-0000-7000-8000-0000000000ca"),
            "2026",
            [
                DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!,
                DiaNaoUtilCongelado.Criar(new DateOnly(2026, 4, 5), "MUNICIPAL", "1504208", "Marabá", "PA").Value!,
                DiaNaoUtilCongelado.Criar(new DateOnly(2026, 8, 15), "ESTADUAL", null, null, "PA").Value!,
                DiaNaoUtilCongelado.Criar(new DateOnly(2026, 10, 28), "INSTITUCIONAL", null, null, null).Value!,
            ]).Value!;

    /// <summary>Contexto do exemplo: o calendário rico, com o fuso reconhecido.</summary>
    internal static ContextoDeContagemDePrazos ContextoRico() =>
        new(CalendarioRico(), FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
}
