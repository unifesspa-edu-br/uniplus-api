namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O papel que o formulário cumpre no teste: o nome que o teste dá ao formulário, e não uma classificação do domínio. O
/// processo guarda só o código e a obrigatoriedade, que o papel fornece.
/// </summary>
public enum PapelDoFormularioDeTeste
{
    [CodigoDeContrato("INSCRICAO", "Inscrição")]
    Inscricao = 1,

    [CodigoDeContrato("ISENCAO_TAXA", "Isenção da taxa")]
    IsencaoTaxa = 2,

    [CodigoDeContrato("HABILITACAO", "Habilitação")]
    Habilitacao = 3,
}

/// <summary>
/// O formulário mínimo de um papel para os testes que não tratam da estrutura dele: uma seção
/// de dados e a revisão e aceite, na fase do cronograma que o papel pede, quando o processo a
/// tem. Os itens sem seção vão para a seção de dados. O de inscrição tem antes a seção reservada
/// com o conjunto básico do candidato, como a escrita o monta: os itens do teste sobem acima dela
/// quando colidem, e o básico que o teste declara vai para a seção, na ordem dele, sem se repetir.
/// </summary>
internal static class FormularioDeTeste
{
    public const string Secao = "DADOS";

    /// <summary>O código do formulário de inscrição, para o teste que cita termo ou aceite por formulário.</summary>
    public const string CodigoDaInscricao = "INSCRICAO";

    /// <summary>O termo do formulário de inscrição, na forma que as respostas admitidas o chaveiam.</summary>
    public static TermoNoFormulario TermoDaInscricao(string codigo) => new(CodigoDaInscricao, codigo);

    /// <summary>O código do formulário que cumpre o papel dado: o papel é o nome do teste, o código é o que o processo guarda.</summary>
    public static CodigoDoFormulario CodigoDoPapel(PapelDoFormularioDeTeste papel) => CodigoDoFormulario.Criar(papel.Codigo()).Value!;

    /// <summary>O código do formulário de inscrição, para a exigência que o teste não liga a formulário nenhum em especial.</summary>
    public static CodigoDoFormulario FormularioDaInscricao { get; } = CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao);

    /// <summary>O formulário que cumpre o papel dado, na fase dada: o pedido de isenção é facultativo, os outros são obrigatórios.</summary>
    public static FormularioDeclarado DeclaradoNoPapel(PapelDoFormularioDeTeste papel, Guid? faseId) =>
        new(CodigoDoPapel(papel), faseId, papel != PapelDoFormularioDeTeste.IsencaoTaxa);

    /// <summary>O formulário do processo que cumpre o papel dado, quando o processo o tem.</summary>
    public static FormularioProcesso? FormularioDe(this ProcessoSeletivo processo, PapelDoFormularioDeTeste papel)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return processo.FormularioDe(CodigoDoPapel(papel));
    }

    /// <summary>Define o formulário que cumpre o papel dado, na fase dada.</summary>
    public static Result DefinirFormulario(
        this ProcessoSeletivo processo,
        PapelDoFormularioDeTeste papel,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null,
        ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio = null)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return processo.DefinirFormulario(DeclaradoNoPapel(papel, faseId), titulo, paginas, itensDaCriacao, consequenciaDaFaltaDeEnvio);
    }

    /// <summary>Remove o formulário que cumpre o papel dado.</summary>
    public static Result RemoverFormulario(this ProcessoSeletivo processo, PapelDoFormularioDeTeste papel)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return processo.RemoverFormulario(CodigoDoPapel(papel));
    }

    /// <summary>Substitui os termos do formulário que cumpre o papel dado.</summary>
    public static Result DefinirTermosDoFormulario(
        this ProcessoSeletivo processo, PapelDoFormularioDeTeste papel, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return processo.DefinirTermosDoFormulario(CodigoDoPapel(papel), termos);
    }

    /// <summary>Substitui os itens do formulário que cumpre o papel dado.</summary>
    public static Result DefinirFatosColetados(
        this ProcessoSeletivo processo,
        PapelDoFormularioDeTeste papel,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return processo.DefinirFatosColetados(CodigoDoPapel(papel), fatosColetados, grupos);
    }

    /// <summary>A ordem a partir da qual o teste declara os itens do formulário de inscrição.</summary>
    public static int PrimeiraOrdemDeInscricao => ConjuntoBasicoDaInscricao.Itens.Count;

    /// <summary>
    /// A avaliação de quem responde só o que aparece, para o envio cujo assunto não é o descarte do
    /// que não se aplica nem a modalidade: nenhum fato não aplicável, nada a descartar, e uma versão
    /// que não deriva a modalidade.
    /// </summary>
    public static ResultadoDaAvaliacaoDaInscricao AvaliacaoSemOcultos { get; } =
        AvaliacaoDoEnvio(new(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), [], [], [], []));

    /// <summary>A avaliação do envio com o formulário dado, numa versão que não deriva a modalidade.</summary>
    public static ResultadoDaAvaliacaoDaInscricao AvaliacaoDoEnvio(AvaliacaoFormulario formulario) =>
        new(formulario, ModalidadesDeConcorrencia.SemDerivacao, new DocumentosDaInscricao([], new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []));

    public static IReadOnlyList<PaginaDoFormulario> Paginas(PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao)
    {
        int deslocamento = papel == PapelDoFormularioDeTeste.Inscricao ? 1 : 0;
        PaginaDoFormulario[] doTeste =
        [
            PaginaDoFormulario.Criar(new PaginaEstrutura(Secao, deslocamento, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
            PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", deslocamento + 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
        ];
        if (deslocamento == 0)
        {
            return doTeste;
        }

        PaginaDoFormularioInput secao = ConjuntoBasicoDaInscricao.Secao;
        return [PaginaDoFormulario.Criar(new PaginaEstrutura(secao.Codigo, secao.Ordem, TipoPaginaFormulario.Secao, null), secao.Apresentacao!.Titulo!, secao.Apresentacao.Descricao, secao.Apresentacao.Aviso).Value!, .. doTeste];
    }

    /// <summary>Os itens do conjunto básico, como a escrita os monta, sem os que o teste já declara.</summary>
    /// <summary>
    /// Um fato booleano que todo formulário de inscrição de teste coleta, por ser do conjunto básico:
    /// serve de fato de elegibilidade do acréscimo onde o acréscimo não é o assunto.
    /// </summary>
    public const string FatoBooleanoDaInscricao = "DESEJA_NOME_SOCIAL";

    public static IReadOnlyList<FatoColetado> DadosBasicos(IEnumerable<string>? exceto = null)
    {
        HashSet<string> declarados = new(exceto ?? [], StringComparer.Ordinal);
        return [.. ConjuntoBasicoDaInscricao.Itens.Where(i => !declarados.Contains(i.FatoCodigo)).Select(Basico)];
    }

    /// <summary>
    /// Os dados básicos como um formulário gravado antes do sexo civil, da identidade de gênero e da
    /// orientação sexual os tinha: sem os três, e na numeração contínua de então.
    /// </summary>
    public static IReadOnlyList<FatoColetado> DadosBasicosDeAntesDosDadosDeGenero() =>
    [
        .. DadosBasicos(["SEXO_CIVIL", "IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"]).Select(static (fato, ordem) => FatoColetadoDeTeste.Como(fato)
            .NaOrdem(ordem)
            .NaSecao(ConjuntoBasicoDaInscricao.CodigoDaSecao)
            .Construir()),
    ];

    /// <summary>Os itens que não são do conjunto básico, que o formulário de inscrição sempre coleta.</summary>
    public static IEnumerable<FatoColetado> ForaDoConjuntoBasico(this IEnumerable<FatoColetado> itens) =>
        itens.Where(static f => !ConjuntoBasicoDaInscricao.Fatos.Contains(f.FatoCodigo));

    /// <summary>Define os itens da papel, criando antes o formulário mínimo quando o processo não o tem.</summary>
    public static Result DefinirItens(
        this ProcessoSeletivo processo,
        IReadOnlyList<FatoColetado> fatos,
        PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao,
        IReadOnlyList<GrupoColetado>? grupos = null)
    {
        GarantirFormulario(processo, papel);
        if (papel != PapelDoFormularioDeTeste.Inscricao)
        {
            return processo.DefinirFatosColetados(CodigoDoPapel(papel), [.. fatos.Select(f => NaSecao(f, 0))], grupos);
        }

        int deslocamento = fatos.Any(static f => !EhBasico(f) && f.Ordem < PrimeiraOrdemDeInscricao)
            || (grupos ?? []).Any(static g => g.Ordem < PrimeiraOrdemDeInscricao)
            ? PrimeiraOrdemDeInscricao
            : 0;
        return processo.DefinirFatosColetados(
            CodigoDoPapel(papel),
            [.. fatos.Select(f => EhBasico(f) ? NaSecaoReservada(f) : NaSecao(f, deslocamento)), .. DadosBasicos(fatos.Select(static f => f.FatoCodigo))],
            grupos is null ? null : [.. grupos.Select(g => Deslocar(g, deslocamento))]);
    }

    /// <summary>
    /// Define os itens de inscrição num processo que ainda não tem fase de inscrição: a fase entra no
    /// cronograma, com janela, para o formulário ter a fase que a publicação exige.
    /// </summary>
    public static Result DefinirItensComFaseDeInscricao(this ProcessoSeletivo processo, IReadOnlyList<FatoColetado> fatos)
    {
        if (FaseDe(processo, PapelDoFormularioDeTeste.Inscricao) is null)
        {
            FaseCronograma inscricao = FaseDeInscricao(processo.CronogramaFases.Select(static f => f.Ordem).DefaultIfEmpty(0).Max() + 1);
            Result cronograma = processo.DefinirCronogramaFases([inscricao, .. processo.CronogramaFases], []);
            if (cronograma.IsFailure)
            {
                return cronograma;
            }
        }

        return processo.DefinirItens(fatos);
    }

    /// <summary>Define o título do formulário da papel, mantendo a fase e as páginas mínimas.</summary>
    public static Result DefinirTitulo(
        this ProcessoSeletivo processo,
        string? titulo,
        PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao)
    {
        GarantirFormulario(processo, papel);
        return processo.DefinirFormulario(
            DeclaradoNoPapel(papel, processo.FormularioDe(papel)!.FaseId), titulo, Paginas(papel));
    }

    /// <summary>Define os termos da papel, criando antes o formulário mínimo quando o processo não o tem.</summary>
    public static Result DefinirTermos(
        this ProcessoSeletivo processo,
        IReadOnlyList<TermoExigidoFormulario> termos,
        PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao)
    {
        GarantirFormulario(processo, papel);
        return processo.DefinirTermosDoFormulario(CodigoDoPapel(papel), termos);
    }

    /// <summary>
    /// Troca o cronograma do processo: o formulário de inscrição sai antes, porque é respondido
    /// numa fase que a troca remove, e volta depois, na fase de inscrição do cronograma novo.
    /// </summary>
    public static Result TrocarCronograma(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases)
    {
        bool tinhaFormulario = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao) is not null;
        if (tinhaFormulario)
        {
            processo.RemoverFormulario(CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao));
        }

        Result trocado = DefinirCronogramaRepondoEtapas(processo, fases);
        if (tinhaFormulario)
        {
            GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        }

        return trocado;
    }

    private static Result DefinirCronogramaRepondoEtapas(
        ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases)
    {
        List<EtapaProcesso> etapasAntes = [.. processo.Etapas];
        Result trocado = processo.DefinirCronogramaFases(fases, []);
        if (trocado.IsSuccess)
        {
            ReporEtapasDeFaseQueContinua(processo, etapasAntes, fases);
        }

        return trocado;
    }

    // A poda do cronograma leva junto a etapa da fase que sai, e a troca de teste recria a fase com a mesma
    // identidade de negócio (o código) e outra origem: a etapa volta, presa à fase de mesmo código.
    private static void ReporEtapasDeFaseQueContinua(
        ProcessoSeletivo processo, List<EtapaProcesso> etapasAntes, IReadOnlyList<FaseCronograma> fases)
    {
        HashSet<Guid> sobreviventes = [.. processo.Etapas.Select(static e => e.Id)];
        List<EtapaProcesso> podadas = [.. etapasAntes.Where(e => !sobreviventes.Contains(e.Id)
            && fases.Any(f => string.Equals(f.Codigo, e.FaseCodigo, StringComparison.Ordinal)))];
        if (podadas.Count > 0)
        {
            Result reposta = processo.DefinirEtapas([.. processo.Etapas, .. podadas]);
            if (reposta.IsFailure)
            {
                throw new InvalidOperationException($"A etapa podada não voltou ao cronograma de teste: {reposta.Error?.Message}");
            }
        }
    }

    /// <summary>
    /// O campo da edição do ENEM, que a inscrição pede a todo candidato quando a classificação do
    /// processo é baseada em ENEM; as opções são as edições declaradas pelo processo.
    /// </summary>
    public static FatoColetado CampoDaEdicaoDoEnem(int ordem) =>
        FatoColetadoDeTeste.Coletando(EdicaoDoEnem.Fato, ordem)
            .ComRotulo("Qual edição do ENEM você vai usar?")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComOrigem(Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemValoresColeta.OpcoesDoProcesso)
            .Construir();

    /// <summary>
    /// Processo com inscrição própria publica o formulário de inscrição (UNI-REQ-0144): cria o
    /// formulário mínimo nesse caso, e só nele.
    /// </summary>
    public static void GarantirFormularioDeInscricaoPropria(ProcessoSeletivo processo)
    {
        if (processo.OrigemCandidatos == Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemCandidatos.InscricaoPropria)
        {
            GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        }
    }

    /// <summary>
    /// Cria, na fase de cada exigência, o formulário mínimo que ela declara e o processo ainda não tem: toda exigência
    /// pertence a um formulário. A recusa de criar o formulário (fase fora do cronograma, por exemplo) fica para o
    /// teste, que a observa na gravação das exigências.
    /// </summary>
    public static void GarantirOsFormulariosDasExigencias(ProcessoSeletivo processo, IEnumerable<DocumentoExigido> exigencias)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(exigencias);

        foreach (DocumentoExigido exigencia in exigencias.Where(e => processo.FormularioDe(e.FormularioCodigo) is null))
        {
            PapelDoFormularioDeTeste papel = CatalogoDe<PapelDoFormularioDeTeste>.Obter(exigencia.FormularioCodigo.Valor);
            processo.DefinirFormulario(
                papel, exigencia.ExigidoNaFaseId, null, Paginas(papel), papel == PapelDoFormularioDeTeste.Inscricao ? DadosBasicos() : null);
        }
    }

    /// <summary>Grava as regras de comprovação depois de criar os formulários que as exigências declaram e o processo ainda não tem.</summary>
    public static Result DefinirDocumentosExigidosNosFormulariosMinimos(this ProcessoSeletivo processo, IReadOnlyList<RegraDeComprovacao> regras)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(regras);

        GarantirOsFormulariosDasExigencias(
            processo, regras.SelectMany(static r => r.EnumerarRegraEItens()).Select(static r => r.DocumentoExigido).OfType<DocumentoExigido>());
        return processo.DefinirDocumentosExigidos(regras);
    }

    /// <summary>Cria o formulário mínimo da papel quando o processo não o tem.</summary>
    public static void GarantirFormulario(ProcessoSeletivo processo, PapelDoFormularioDeTeste papel)
    {
        if (processo.FormularioDe(papel) is not null)
        {
            return;
        }

        Guid? fase = FaseDe(processo, papel);
        Result criado = processo.DefinirFormulario(
            DeclaradoNoPapel(papel, fase), null, Paginas(papel),
            papel == PapelDoFormularioDeTeste.Inscricao ? DadosBasicos() : null);
        if (criado.IsFailure)
        {
            throw new InvalidOperationException($"O formulário de teste não pôde ser criado: {criado.Error!.Message}");
        }
    }

    private static FaseCronograma FaseDeInscricao(int ordem) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            ordem, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", Unifesspa.UniPlus.Selecao.Domain.Enums.PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static Guid? FaseDe(ProcessoSeletivo processo, PapelDoFormularioDeTeste papel) =>
        processo.CronogramaFases.FirstOrDefault(f => papel switch
        {
            PapelDoFormularioDeTeste.Inscricao => f.ColetaInscricao,
            PapelDoFormularioDeTeste.IsencaoTaxa => f.ColetaSolicitacaoIsencao,
            PapelDoFormularioDeTeste.Habilitacao => f.Codigo == FormularioProcesso.CodigoFaseHabilitacao,
            _ => false,
        })?.Id;

    private static bool EhBasico(FatoColetado fato) => ConjuntoBasicoDaInscricao.Fatos.Contains(fato.FatoCodigo);

    /// <summary>O dado básico que o teste declara vai para a seção reservada, na ordem do conjunto básico.</summary>
    private static FatoColetado NaSecaoReservada(FatoColetado fato) =>
        string.Equals(fato.PaginaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal)
            ? fato
            : FatoColetadoDeTeste.Como(fato)
                .NaOrdem(ConjuntoBasicoDaInscricao.Itens.Single(i => i.FatoCodigo == fato.FatoCodigo).Ordem)
                .NaSecao(ConjuntoBasicoDaInscricao.CodigoDaSecao)
                .Construir();

    private static FatoColetado NaSecao(FatoColetado fato, int deslocamento) =>
        fato.PaginaCodigo is not null && deslocamento == 0
            ? fato
            : FatoColetadoDeTeste.Como(fato)
                .NaOrdem(fato.Ordem + deslocamento)
                .NaSecao(fato.PaginaCodigo ?? Secao)
                .Construir();

    private static GrupoColetado Deslocar(GrupoColetado grupo, int deslocamento) =>
        deslocamento == 0
            ? grupo
            : GrupoColetadoDeTeste.Como(grupo).NaOrdem(grupo.Ordem + deslocamento).Construir();

    /// <summary>O formato que o catálogo dá a cada texto do conjunto básico.</summary>
    private static readonly Dictionary<string, string> FormatoDosTextos = new(StringComparer.Ordinal)
    {
        ["NOME"] = "LIVRE",
        ["NOME_SOCIAL"] = "NOME_PESSOA",
        ["NOME_MAE"] = "NOME_PESSOA",
        ["NOME_PAI"] = "NOME_PESSOA",
        ["CPF"] = "CPF",
        ["EMAIL"] = "EMAIL",
        ["TELEFONE"] = "TELEFONE",
        ["RG_NUMERO"] = "LIVRE",
        ["RG_ORGAO_EMISSOR_OUTRO"] = "LIVRE",
        ["PASSAPORTE_NUMERO"] = "LIVRE",
        ["RNM_NUMERO"] = "LIVRE",
        ["NOME_COMUNIDADE"] = "LIVRE",
    };

    /// <summary>A classificação de proteção de dados que o catálogo dá a cada fato do conjunto básico.</summary>
    private static readonly Dictionary<string, string> ClassificacaoDosFatos = new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = "PUBLICO",
        ["CPF"] = "IDENTIFICADOR",
        ["RG_NUMERO"] = "IDENTIFICADOR",
        ["PASSAPORTE_NUMERO"] = "IDENTIFICADOR",
        ["RNM_NUMERO"] = "IDENTIFICADOR",
        ["IDENTIDADE_GENERO"] = "SENSIVEL",
        ["ORIENTACAO_SEXUAL"] = "SENSIVEL",
    };

    /// <summary>O item básico na forma do domínio, a partir da entrada do conjunto básico.</summary>
    private static FatoColetado Basico(FatoColetadoInput entrada) =>
        FatoColetadoDeTeste.Coletando(entrada.FatoCodigo, entrada.Ordem)
            .ComRotulo(entrada.Apresentacao!.Rotulo!)
            .DoTipo(CatalogoDe<TipoDeCampo>.Obter(entrada.Apresentacao.TipoDeCampo!))
            .ComObrigatoriedade(ObrigatoriedadeDaEntrada(entrada))
            .ExibidoQuando(ExibicaoDaEntrada(entrada))
            .ComCopia(CopiaDoCatalogoDoBasico(entrada))
            .NaSecao(entrada.PaginaCodigo)
            .ComAjuda(entrada.Apresentacao.Ajuda)
            .ComRestricoes([.. (entrada.Regras!.Restricoes ?? []).Select(static r => EntradaDeRegras.Restricao(r).Value!)])
            .Construir();

    private static Obrigatoriedade ObrigatoriedadeDaEntrada(FatoColetadoInput entrada) =>
        EntradaDeRegras.Obrigatoriedade(
            entrada.Regras!.Obrigatoriedade!.Tipo, EntradaDeRegras.Predicado(entrada.Regras.Obrigatoriedade.Predicado).Value)!;

    private static CopiaDoItemNoCatalogo CopiaDoCatalogoDoBasico(FatoColetadoInput entrada) =>
        ProtecaoDoFatoDeTeste.Copia(
            ClassificacaoDosFatos.GetValueOrDefault(entrada.FatoCodigo, "PESSOAL"),
            entrada.FatoCodigo.EndsWith("_UF", StringComparison.Ordinal) ? Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemValoresColeta.UnidadesFederativas : default,
            FormatoDosTextos.GetValueOrDefault(entrada.FatoCodigo));

    private static PredicadoDnf? ExibicaoDaEntrada(FatoColetadoInput entrada) =>
        entrada.Regras!.Exibicao is not { Count: > 0 } clausulas
            ? null
            : PredicadoDnf.CriarDeCondicoesAgrupadas(
                [.. clausulas.SelectMany((clausula, indice) => clausula.Select(c =>
                    (indice, CondicaoDnf.Criar(c.Fato, CatalogoDe<Operador>.Obter(c.Operador!), c.Valor).Value!)))]).Value;
}
