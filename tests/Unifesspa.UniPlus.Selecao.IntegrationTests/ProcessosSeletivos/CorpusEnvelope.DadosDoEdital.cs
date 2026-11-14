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

internal static partial class CorpusEnvelope
{
    /// <summary>
    /// O período espelha a janela da fase que coleta inscrição (<c>INSCRICAO</c>), porque é dela
    /// que a publicação passou a projetá-lo (issue #1350).
    /// </summary>
    /// <remarks>
    /// Antes desta issue o corpus declarava o fim em 15/04 enquanto a fase fechava em 20/03 — 26
    /// dias de divergência, que era exatamente o estado que a issue tornou impossível. Manter a
    /// fixture naquele formato congelaria a prova de um certame que o sistema não produz mais.
    /// </remarks>
    internal static DadosEdital DadosRicos() => DadosEdital.Criar(
        numero: "042/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        periodoInscricaoFim: new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
        documentoEditalId: Documento,
        anexos: AnexosRicos).Value!;

    /// <summary>
    /// Os anexos do edital rico: um PDF e um DOCX, de formatos e tamanhos diferentes, para a fixture
    /// provar a ordem em que o edital os apresenta e o tipo de mídia de cada um.
    /// </summary>
    private static readonly AnexoDoEdital[] AnexosRicos =
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
    /// (COR_RACA, RENDA — ambos <c>SELECAO_UNICA</c>, UNI-REQ-0072): totalidade exigida pelo
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
        List<ValorDominioDeclaradoCongelado> corRaca =
        [
            new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
            new ValorDominioDeclaradoCongelado("PRETA", "Preta", 1, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação."),
            new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
        ];
        List<ValorDominioDeclaradoCongelado> renda =
        [
            new ValorDominioDeclaradoCongelado("ATE_1_SM", "Renda familiar per capita de até 1 salário mínimo.", 0),
            new ValorDominioDeclaradoCongelado("ACIMA_1_SM", "Renda familiar per capita acima de 1 salário mínimo.", 1),
        ];
        List<ValorDominioDeclaradoCongelado> ufs =
        [
            new ValorDominioDeclaradoCongelado("AC", "Acre", 0),
            new ValorDominioDeclaradoCongelado("PA", "Pará", 1),
        ];
        List<ValorDominioDeclaradoCongelado> parentesco =
        [
            new ValorDominioDeclaradoCongelado(CandidatoComoMembro.ProprioCandidato, "O próprio candidato.", 0),
            new ValorDominioDeclaradoCongelado("PAI_OU_MAE", "Pai ou mãe do candidato.", 1),
        ];

        if (permutarValores)
        {
            corRaca.Reverse();
            renda.Reverse();
            parentesco.Reverse();
            ufs.Reverse();
        }

        return CatalogoDoConjuntoBasico.ComValoresCongelados(new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["COR_RACA"] = corRaca,
            ["RENDA"] = renda,
            [CandidatoComoMembro.FatoParentesco] = parentesco,
            ["NATURALIDADE_UF"] = ufs,
            // As opções do processo vêm das edições declaradas, que o processo rico admite.
            [EdicaoDoEnem.Fato] = [new ValorDominioDeclaradoCongelado("2026", "2026", 0)],
        });
    }

    /// <summary>
    /// O grupo repetível do corpus rico (UNI-REQ-0146): exibido por um fato do candidato, com um
    /// campo exibido pelo campo anterior da mesma ocorrência — o decoder remonta o grupo, as regras
    /// dele e os campos, e o grafo conjunto liga os campos às regras. Sem máximo, o envelope grava
    /// o máximo nulo; o grupo inclui o candidato, identificado pelo parentesco.
    /// </summary>
    private static GrupoColetado ComposicaoFamiliar(bool permutar) => GrupoColetado.Criar(
        "COMPOSICAO_FAMILIAR", 2, FormularioDeTeste.Secao, "Composição familiar", 1, null,
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!,
        Obrigatoriedade.Nunca,
        Ordem([
            FatoColetado.Criar("TRABALHADOR_RURAL", 0, "Trabalha no campo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            // Classificação Identificador só para provar que o codec a carrega também em subitem de
            // grupo — não tem relação com o conteúdo real do fato (#1857).
            FatoColetado.Criar("MENOR_SOB_GUARDA", 1, "Menor sob guarda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "TRABALHADOR_RURAL", Operador.Igual, JsonSerializer.SerializeToElement(false)).Value!,
            ], classificacaoProtecao: "IDENTIFICADOR", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar(CandidatoComoMembro.FatoParentesco, 2, "Parentesco", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], permutar),
        incluiCandidato: true).Value!;

    /// <summary>
    /// Calendário congelado do corpus rico (UNI-REQ-0080). Traz <b>uma data de cada abrangência</b>
    /// de propósito: um corpus só com feriado nacional deixaria as três formas territoriais sem
    /// golden, e a recusa de combinação inválida no decoder passaria a ser provada apenas por
    /// mutação, nunca por artefato.
    /// </summary>
    /// <remarks>
    /// O id é fixo, como os demais ids do corpus: gerado a cada execução, a golden fixture rica
    /// não teria como existir. A data municipal é de Marabá, a localidade regente do corpus.
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

    /// <summary>Contexto do corpus: o calendário rico, com o fuso reconhecido.</summary>
    internal static ContextoDeContagemDePrazos ContextoRico() =>
        new(CalendarioRico(), FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
}
