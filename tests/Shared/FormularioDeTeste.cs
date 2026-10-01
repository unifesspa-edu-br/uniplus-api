namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O formulário mínimo de uma finalidade para os testes que não tratam da estrutura dele: uma seção
/// de dados e a revisão e aceite, na fase do cronograma que a finalidade pede, quando o processo a
/// tem. Os itens sem seção vão para a seção de dados.
/// </summary>
internal static class FormularioDeTeste
{
    public const string Secao = "DADOS";

    public static IReadOnlyList<EtapaFormulario> Etapas() =>
    [
        EtapaFormulario.Criar(Secao, 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
        EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
    ];

    /// <summary>Define os itens da finalidade, criando antes o formulário mínimo quando o processo não o tem.</summary>
    public static Result DefinirItens(
        this ProcessoSeletivo processo,
        IReadOnlyList<FatoColetado> fatos,
        PrecondicaoIfMatch? precondicao = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        PrecondicaoIfMatch vigente = GarantirFormulario(processo, finalidade, precondicao ?? PrecondicaoIfMatch.Ausente);
        return processo.DefinirFatosColetados(finalidade, [.. fatos.Select(NaSecao)], vigente, grupos);
    }

    /// <summary>
    /// Define os itens de inscrição num processo que ainda não tem fase de inscrição: a fase entra no
    /// cronograma, com janela, para o formulário ter a fase que a publicação exige.
    /// </summary>
    public static Result DefinirItensComFaseDeInscricao(this ProcessoSeletivo processo, IReadOnlyList<FatoColetado> fatos)
    {
        if (FaseDe(processo, FinalidadeFormulario.Inscricao) is null)
        {
            FaseCronograma inscricao = FaseDeInscricao(processo.CronogramaFases.Select(static f => f.Ordem).DefaultIfEmpty(0).Max() + 1);
            Result cronograma = processo.DefinirCronogramaFases([inscricao, .. processo.CronogramaFases], [], PrecondicaoIfMatch.Ausente);
            if (cronograma.IsFailure)
            {
                return cronograma;
            }
        }

        return processo.DefinirItens(fatos);
    }

    /// <summary>Define o título do formulário da finalidade, mantendo a fase e as etapas mínimas.</summary>
    public static Result DefinirTitulo(
        this ProcessoSeletivo processo,
        string? titulo,
        PrecondicaoIfMatch? precondicao = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        PrecondicaoIfMatch vigente = GarantirFormulario(processo, finalidade, precondicao ?? PrecondicaoIfMatch.Ausente);
        return processo.DefinirFormulario(finalidade, processo.FormularioDe(finalidade)!.FaseId, titulo, Etapas(), vigente);
    }

    /// <summary>Define os termos da finalidade, criando antes o formulário mínimo quando o processo não o tem.</summary>
    public static Result DefinirTermos(
        this ProcessoSeletivo processo,
        IReadOnlyList<TermoExigidoFormulario> termos,
        PrecondicaoIfMatch? precondicao = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        PrecondicaoIfMatch vigente = GarantirFormulario(processo, finalidade, precondicao ?? PrecondicaoIfMatch.Ausente);
        return processo.DefinirTermosDoFormulario(finalidade, termos, vigente);
    }

    /// <summary>
    /// Troca o cronograma do processo: o formulário de inscrição sai antes, porque é respondido
    /// numa fase que a troca remove, e volta depois, na fase de inscrição do cronograma novo.
    /// </summary>
    public static Result TrocarCronograma(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases, PrecondicaoIfMatch precondicao)
    {
        bool tinhaFormulario = processo.FormularioDe(FinalidadeFormulario.Inscricao) is not null;
        if (tinhaFormulario)
        {
            processo.RemoverFormulario(FinalidadeFormulario.Inscricao, precondicao);
        }

        Result trocado = processo.DefinirCronogramaFases(fases, [], precondicao);
        if (tinhaFormulario)
        {
            GarantirFormulario(processo, FinalidadeFormulario.Inscricao, precondicao);
        }

        return trocado;
    }

    /// <summary>
    /// Processo com inscrição própria publica o formulário de inscrição (UNI-REQ-0144): cria o
    /// formulário mínimo nesse caso, e só nele.
    /// </summary>
    public static void GarantirFormularioDeInscricaoPropria(ProcessoSeletivo processo)
    {
        if (processo.OrigemCandidatos == Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemCandidatos.InscricaoPropria)
        {
            GarantirFormulario(processo, FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente);
        }
    }

    /// <summary>
    /// Cria o formulário mínimo da finalidade quando o processo não o tem. Devolve a precondição que
    /// continua válida: sob sessão editorial, definir o formulário avança a revisão e troca o ETag.
    /// </summary>
    public static PrecondicaoIfMatch GarantirFormulario(
        ProcessoSeletivo processo, FinalidadeFormulario finalidade, PrecondicaoIfMatch precondicao)
    {
        if (processo.FormularioDe(finalidade) is not null)
        {
            return precondicao;
        }

        Guid? fase = FaseDe(processo, finalidade);
        Result criado = processo.DefinirFormulario(finalidade, fase, null, Etapas(), precondicao);
        if (criado.IsFailure)
        {
            throw new InvalidOperationException($"O formulário de teste não pôde ser criado: {criado.Error!.Message}");
        }

        return processo.ETagDaSessaoEditorial is { } etag ? PrecondicaoIfMatch.DeTags([etag]) : precondicao;
    }

    private static FaseCronograma FaseDeInscricao(int ordem) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), "INSCRICAO", "CEPS", Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemDataFase.Propria,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("INSCRICAO", Unifesspa.UniPlus.Selecao.Domain.Enums.PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static Guid? FaseDe(ProcessoSeletivo processo, FinalidadeFormulario finalidade) =>
        processo.CronogramaFases.FirstOrDefault(f => finalidade switch
        {
            FinalidadeFormulario.Inscricao => f.ColetaInscricao,
            FinalidadeFormulario.IsencaoTaxa => f.ColetaSolicitacaoIsencao,
            FinalidadeFormulario.Habilitacao => f.Codigo == FormularioProcesso.CodigoFaseHabilitacao,
            _ => false,
        })?.Id;

    private static FatoColetado NaSecao(FatoColetado fato) =>
        fato.EtapaCodigo is not null
            ? fato
            : FatoColetado.Criar(
                fato.FatoCodigo, fato.Ordem, fato.Rotulo, fato.TipoRenderizacao, fato.Obrigatoriedade,
                [.. fato.Precondicoes.Select(static c => CondicaoPrecondicaoFato.Criar(c.Clausula, c.Fato, c.Operador, c.Valor).Value!)],
                fato.OrigemValores, Secao, formato: fato.Formato, ajuda: fato.Ajuda, pedirConfirmacao: fato.PedirConfirmacao,
                restricoes: fato.Restricoes).Value!;
}
