namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O formulário mínimo de uma finalidade para os testes que não tratam da estrutura dele: uma seção
/// de dados e a revisão e aceite, na fase do cronograma que a finalidade pede, quando o processo a
/// tem. Os itens sem seção vão para a seção de dados. O de inscrição tem antes a seção reservada
/// com o conjunto básico do candidato, como a escrita o monta: os itens do teste sobem acima dela
/// quando colidem, e o básico que o teste declara vai para a seção, na ordem dele, sem se repetir.
/// </summary>
internal static class FormularioDeTeste
{
    public const string Secao = "DADOS";

    /// <summary>A ordem a partir da qual o teste declara os itens do formulário de inscrição.</summary>
    public static int PrimeiraOrdemDeInscricao => ConjuntoBasicoDaInscricao.Itens.Count;

    public static IReadOnlyList<EtapaFormulario> Etapas(FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        int deslocamento = finalidade == FinalidadeFormulario.Inscricao ? 1 : 0;
        EtapaFormulario[] doTeste =
        [
            EtapaFormulario.Criar(Secao, deslocamento, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
            EtapaFormulario.Criar("REVISAO", deslocamento + 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
        ];
        if (deslocamento == 0)
        {
            return doTeste;
        }

        EtapaFormularioInput secao = ConjuntoBasicoDaInscricao.Secao;
        return [EtapaFormulario.Criar(secao.Codigo, secao.Ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, secao.Titulo, secao.Descricao, secao.Aviso).Value!, .. doTeste];
    }

    /// <summary>Os itens do conjunto básico, como a escrita os monta, sem os que o teste já declara.</summary>
    public static IReadOnlyList<FatoColetado> DadosBasicos(IEnumerable<string>? exceto = null)
    {
        HashSet<string> declarados = new(exceto ?? [], StringComparer.Ordinal);
        return [.. ConjuntoBasicoDaInscricao.Itens.Where(i => !declarados.Contains(i.FatoCodigo)).Select(Basico)];
    }

    /// <summary>Os itens que não são do conjunto básico, que o formulário de inscrição sempre coleta.</summary>
    public static IEnumerable<FatoColetado> ForaDoConjuntoBasico(this IEnumerable<FatoColetado> itens) =>
        itens.Where(static f => !ConjuntoBasicoDaInscricao.Fatos.Contains(f.FatoCodigo));

    /// <summary>Define os itens da finalidade, criando antes o formulário mínimo quando o processo não o tem.</summary>
    public static Result DefinirItens(
        this ProcessoSeletivo processo,
        IReadOnlyList<FatoColetado> fatos,
        PrecondicaoIfMatch? precondicao = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        PrecondicaoIfMatch vigente = GarantirFormulario(processo, finalidade, precondicao ?? PrecondicaoIfMatch.Ausente);
        if (finalidade != FinalidadeFormulario.Inscricao)
        {
            return processo.DefinirFatosColetados(finalidade, [.. fatos.Select(f => NaSecao(f, 0))], vigente, grupos);
        }

        int deslocamento = fatos.Any(static f => !EhBasico(f) && f.Ordem < PrimeiraOrdemDeInscricao)
            || (grupos ?? []).Any(static g => g.Ordem < PrimeiraOrdemDeInscricao)
            ? PrimeiraOrdemDeInscricao
            : 0;
        return processo.DefinirFatosColetados(
            finalidade,
            [.. fatos.Select(f => EhBasico(f) ? NaSecaoReservada(f) : NaSecao(f, deslocamento)), .. DadosBasicos(fatos.Select(static f => f.FatoCodigo))],
            vigente,
            grupos is null ? null : [.. grupos.Select(g => Deslocar(g, deslocamento))]);
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
        return processo.DefinirFormulario(finalidade, processo.FormularioDe(finalidade)!.FaseId, titulo, Etapas(finalidade), vigente);
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
        Result criado = processo.DefinirFormulario(
            finalidade, fase, null, Etapas(finalidade), precondicao,
            finalidade == FinalidadeFormulario.Inscricao ? DadosBasicos() : null);
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

    private static bool EhBasico(FatoColetado fato) => ConjuntoBasicoDaInscricao.Fatos.Contains(fato.FatoCodigo);

    /// <summary>O dado básico que o teste declara vai para a seção reservada, na ordem do conjunto básico.</summary>
    private static FatoColetado NaSecaoReservada(FatoColetado fato) =>
        string.Equals(fato.EtapaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal)
            ? fato
            : FatoColetado.Criar(
                fato.FatoCodigo, ConjuntoBasicoDaInscricao.Itens.Single(i => i.FatoCodigo == fato.FatoCodigo).Ordem, fato.Rotulo,
                fato.TipoRenderizacao, fato.Obrigatoriedade,
                [.. fato.Precondicoes.Select(static c => CondicaoPrecondicaoFato.Criar(c.Clausula, c.Fato, c.Operador, c.Valor).Value!)],
                fato.OrigemValores, ConjuntoBasicoDaInscricao.CodigoDaSecao, formato: fato.Formato, ajuda: fato.Ajuda,
                pedirConfirmacao: fato.PedirConfirmacao, restricoes: fato.Restricoes).Value!;

    private static FatoColetado NaSecao(FatoColetado fato, int deslocamento) =>
        fato.EtapaCodigo is not null && deslocamento == 0
            ? fato
            : FatoColetado.Criar(
                fato.FatoCodigo, fato.Ordem + deslocamento, fato.Rotulo, fato.TipoRenderizacao, fato.Obrigatoriedade,
                [.. fato.Precondicoes.Select(static c => CondicaoPrecondicaoFato.Criar(c.Clausula, c.Fato, c.Operador, c.Valor).Value!)],
                fato.OrigemValores, fato.EtapaCodigo ?? Secao, formato: fato.Formato, ajuda: fato.Ajuda, pedirConfirmacao: fato.PedirConfirmacao,
                restricoes: fato.Restricoes).Value!;

    private static GrupoColetado Deslocar(GrupoColetado grupo, int deslocamento) =>
        deslocamento == 0
            ? grupo
            : GrupoColetado.Criar(
                grupo.Codigo, grupo.Ordem + deslocamento, grupo.EtapaCodigo, grupo.Rotulo, grupo.Minimo, grupo.Maximo, grupo.Exibicao,
                grupo.Obrigatoriedade, [.. grupo.Subitens], grupo.Finalidade, grupo.IncluiCandidato).Value!;

    /// <summary>O formato que o catálogo dá a cada texto do conjunto básico.</summary>
    private static readonly Dictionary<string, string> FormatoDosTextos = new(StringComparer.Ordinal)
    {
        ["NOME"] = "NOME_PESSOA",
        ["NOME_SOCIAL"] = "NOME_PESSOA",
        ["NOME_MAE"] = "NOME_PESSOA",
        ["NOME_PAI"] = "NOME_PESSOA",
        ["CPF"] = "CPF",
        ["EMAIL"] = "EMAIL",
        ["TELEFONE"] = "TELEFONE",
        ["RG_NUMERO"] = "LIVRE",
        ["RG_ORGAO_EMISSOR"] = "LIVRE",
        ["DOCUMENTO_ESTRANGEIRO_NUMERO"] = "LIVRE",
    };

    /// <summary>O item básico na forma do domínio, a partir da entrada do conjunto básico.</summary>
    private static FatoColetado Basico(FatoColetadoInput entrada)
    {
        Obrigatoriedade obrigatoriedade = EntradaDeRegras.Obrigatoriedade(
            entrada.Obrigatoriedade, EntradaDeRegras.Predicado(entrada.PredicadoObrigatoriedade).Value)!;
        CondicaoPrecondicaoFato[] precondicoes = [.. (entrada.Precondicao ?? []).SelectMany((clausula, indice) => clausula.Select(c =>
            CondicaoPrecondicaoFato.Criar(indice, c.Fato, OperadorCodigo.FromCodigo(c.Operador), c.Valor).Value!))];
        return FatoColetado.Criar(
            entrada.FatoCodigo, entrada.Ordem, entrada.Rotulo, TipoRenderizacaoCodigo.FromCodigo(entrada.TipoRenderizacao), obrigatoriedade, precondicoes,
            entrada.FatoCodigo.EndsWith("_UF", StringComparison.Ordinal) ? Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemValoresColeta.UnidadesFederativas : default,
            entrada.EtapaCodigo, formato: FormatoDosTextos.GetValueOrDefault(entrada.FatoCodigo),
            restricoes: [.. (entrada.Restricoes ?? []).Select(static r => EntradaDeRegras.Restricao(r).Value!)]).Value!;
    }
}
