namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDosFormularios
{
    internal static Result<PlanoDoFormulario> PlanejarDefinicao(
        ConfiguracaoDeclarada configuracao,
        FormularioDeclarado declarado,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado>? itensDaCriacao,
        ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(declarado);

        FormularioProcesso? existente = configuracao.Coleta.FormularioDe(declarado.Codigo);
        IReadOnlyList<FatoColetado>? itensIniciais = existente is null && itensDaCriacao is { Count: > 0 } ? itensDaCriacao : null;
        IReadOnlyList<FieldError> recusas = ConferirFormulario(configuracao, declarado, titulo, paginas, itensDaCriacao: itensIniciais);
        return recusas.Count > 0
            ? Result<PlanoDoFormulario>.ValidationFailure(recusas)
            : MontarPlano(configuracao, NovoFormulario(configuracao, declarado, titulo, paginas, consequenciaDaFaltaDeEnvio), existente, itensIniciais);
    }

    // A conferência anterior já recusou o que a fábrica recusaria.
    private static FormularioProcesso NovoFormulario(
        ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado, string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas, ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio)
    {
        FormularioProcesso novo = FormularioProcesso.Criar(declarado, configuracao.CronogramaLido(), titulo, paginas).Value!;
        novo.DeclararConsequenciaDaFaltaDeEnvio(consequenciaDaFaltaDeEnvio);
        return novo;
    }

    private static Result<PlanoDoFormulario> MontarPlano(
        ConfiguracaoDeclarada configuracao, FormularioProcesso novo, FormularioProcesso? existente, IReadOnlyList<FatoColetado>? itensIniciais)
    {
        Result itens = itensIniciais is null
            ? Result.Success()
            : RegrasDaColeta.ConferirFatosColetados(configuracao, novo, itensIniciais, []);
        return itens.IsFailure
            ? Result<PlanoDoFormulario>.ValidationFailure(itens.Errors)
            : Result<PlanoDoFormulario>.Success(new PlanoDoFormulario(novo, existente, MudancaDaDefinicao(configuracao, novo, existente, itensIniciais)));
    }

    private static MudancaDaConfiguracao MudancaDaDefinicao(
        ConfiguracaoDeclarada configuracao,
        FormularioProcesso novo,
        FormularioProcesso? existente,
        IReadOnlyList<FatoColetado>? itensIniciais)
    {
        if (existente is not null)
        {
            return new MudancaDaConfiguracao();
        }

        MudancaDaConfiguracao itens = itensIniciais is null ? new MudancaDaConfiguracao() : RegrasDaColeta.PlanejarTroca(configuracao, novo, itensIniciais, []);
        return itens with { Formularios = Troca.Acrescentando([novo]) };
    }

    /// <summary>
    /// A fase do formulário, se informada, está no cronograma; as páginas têm estrutura válida, acomodam
    /// os itens e só citam fatos que o formulário conhece antes delas.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFormulario(
        ConfiguracaoDeclarada configuracao,
        FormularioDeclarado declarado,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        bool paginasCompletas = true,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(declarado);
        ArgumentNullException.ThrowIfNull(paginas);

        List<FieldError> recusas = FormaDoCabecalho.ValidarTitulo(titulo);
        AcrescentarRecusaDaFase(recusas, configuracao, declarado.FaseId);

        if (paginasCompletas)
        {
            recusas.AddRange(RecusasDasPaginas(configuracao, declarado, paginas, itensDaCriacao));
        }

        return recusas;
    }

    // Provisório até a retirada da finalidade: quem ainda declara o formulário pela finalidade recebe o código dela.
    public static IReadOnlyList<FieldError> ConferirFormulario(
        ConfiguracaoDeclarada configuracao,
        FinalidadeFormulario? finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        bool paginasCompletas = true,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null) =>
        finalidade is { } declarada
            ? ConferirFormulario(configuracao, FormularioDeclarado.DaFinalidade(declarada, faseId), titulo, paginas, paginasCompletas, itensDaCriacao)
            : FormaDoCabecalho.Validar(finalidade, titulo);

    private static void AcrescentarRecusaDaFase(List<FieldError> recusas, ConfiguracaoDeclarada configuracao, Guid? faseId)
    {
        if (faseId is { } fase && fase != Guid.Empty && RecusaDaFaseForaDoCronograma(configuracao, fase) is { } recusa)
        {
            recusas.Add(new("faseId", recusa));
        }
    }

    private static List<FieldError> RecusasDasPaginas(
        ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado, IReadOnlyList<PaginaDoFormulario> paginas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        IReadOnlyList<PaginaEstrutura> estrutura = [.. paginas.Select(static e => e.Estrutura)];
        List<FieldError> recusas = [.. EstruturaFormulario.ValidarPaginas(configuracao.Estrutura(declarado), estrutura)];
        FormularioProcesso? existente = configuracao.Coleta.FormularioDe(declarado.Codigo);
        if (recusas.Count == 0)
        {
            recusas.AddRange(RecusasDosItens(configuracao, existente, estrutura, itensDaCriacao));
        }

        recusas.AddRange(RecusasDeExibicao(configuracao, declarado, existente, paginas, itensDaCriacao));
        return recusas;
    }

    // Os itens não estão no corpo do formulário; a recusa aponta as páginas e nomeia o item.
    private static IEnumerable<FieldError> RecusasDosItens(
        ConfiguracaoDeclarada configuracao, FormularioProcesso? existente, IReadOnlyList<PaginaEstrutura> estrutura, IReadOnlyList<FatoColetado>? itensDaCriacao) =>
        EstruturaFormulario.ValidarItens(
            estrutura,
            itensDaCriacao is null && existente is not null ? ItensDoFormulario(configuracao, existente) : CitacoesDosFormularios.ParaEstrutura(itensDaCriacao ?? [], []),
            secaoObrigatoria: false)
        .Select(static recusa => recusa with { Field = EstruturaFormulario.CaminhoDasPaginas });

    // A exibição de cada seção cita só o que o formulário conhece antes dela, com os itens atuais ou,
    // na criação, com os que o formulário recebe ao nascer.
    private static IEnumerable<FieldError> RecusasDeExibicao(
        ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado, FormularioProcesso? existente,
        IReadOnlyList<PaginaDoFormulario> paginas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        FatoColetado[] itens = [.. itensDaCriacao ?? (existente is null ? [] : configuracao.Coleta.ItensDo(existente))];
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(configuracao, declarado, itens);
        PaginaDoGrafo[] paginasDoGrafo = [.. paginas.Select(static e => e.ParaGrafo())];
        ItemDoGrafo[] itensDoGrafo = [.. itens.Select(static i => i.ParaGrafo())];
        GrupoDoGrafo[] gruposDoGrafo = [.. (existente is null ? [] : configuracao.Coleta.GruposDo(existente)).Select(static g => g.ParaGrafo())];
        for (int indice = 0; indice < paginas.Count; indice++)
        {
            if (RecusaDaExibicao(indice, paginasDoGrafo, itensDoGrafo, gruposDoGrafo, dependencias) is { } recusa)
            {
                yield return recusa;
            }
        }
    }

    private static FieldError? RecusaDaExibicao(
        int indice, PaginaDoGrafo[] paginas, ItemDoGrafo[] itens, GrupoDoGrafo[] grupos, DependenciasDoFormulario dependencias)
    {
        DomainError? recusa = GrafoDoFormulario.CitacaoInvalidaDaPagina(
            paginas[indice], GrafoDoFormulario.PosicaoDaPagina(paginas[indice], paginas, itens, grupos), dependencias);
        return recusa is null ? null : new FieldError($"{EstruturaFormulario.CaminhoDasPaginas}[{indice}].exibicao", recusa);
    }

    /// <summary>A fase do formulário, quando informada, é uma das fases do cronograma do processo.</summary>
    internal static DomainError? RecusaDaFaseForaDoCronograma(ConfiguracaoDeclarada configuracao, Guid faseId) =>
        configuracao.Cronograma.CronogramaFases.Any(f => f.Id == faseId)
            ? null
            : new DomainError(FormularioProcessoErrorCodes.FaseForaDoCronograma, "A fase do formulário não está no cronograma do processo.");

    internal static IReadOnlyList<ItemEstrutura> ItensDoFormulario(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        CitacoesDosFormularios.ParaEstrutura(configuracao.Coleta.ItensDo(formulario), configuracao.Coleta.GruposDo(formulario));

    internal static Result<MudancaDaConfiguracao> PlanejarRemocao(ConfiguracaoDeclarada configuracao, CodigoDoFormulario codigo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(codigo);

        if (configuracao.Coleta.FormularioDe(codigo) is not { } formulario)
        {
            return Result<MudancaDaConfiguracao>.Failure(FormularioNaoEncontrado(codigo));
        }

        IReadOnlyList<FieldError> comExigencia = ConferenciaDasExigenciasDoFormulario.ConferirRemocao([codigo], ExigenciasDoProcesso(configuracao));
        return comExigencia.Count > 0
            ? Result<MudancaDaConfiguracao>.ValidationFailure(comExigencia)
            : RemocaoSemOrfas(configuracao, formulario);
    }

    private static IEnumerable<ExigenciaDeDocumentoDoFormulario> ExigenciasDoProcesso(ConfiguracaoDeclarada configuracao) =>
        configuracao.Exigencias.DocumentosExigidos
            .Where(static documento => documento.FormularioCodigo is not null)
            .Select(static documento => new ExigenciaDeDocumentoDoFormulario(documento.FormularioCodigo!, documento.ExigidoNaFaseId));

    private static Result<MudancaDaConfiguracao> RemocaoSemOrfas(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        CitacoesDosFormularios.CitacaoQueFicariaOrfa(configuracao, formulario, []) is { } orfa
            ? Result<MudancaDaConfiguracao>.Failure(orfa)
            : Result<MudancaDaConfiguracao>.Success(MudancaDaRemocao(configuracao, formulario));

    private static MudancaDaConfiguracao MudancaDaRemocao(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) => new()
    {
        Formularios = Troca.Retirando([formulario]),
        Campos = Troca.Retirando(configuracao.Coleta.CamposDo(formulario)),
        GruposColetados = Troca.Retirando(configuracao.Coleta.GruposDo(formulario)),
        TermosExigidos = Troca.Retirando(configuracao.Coleta.TermosDo(formulario)),
    };

    internal static Result<MudancaDaConfiguracao> PlanejarTermos(
        ConfiguracaoDeclarada configuracao, CodigoDoFormulario codigo, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(codigo);

        if (configuracao.Coleta.FormularioDe(codigo) is not { } formulario)
        {
            return Result<MudancaDaConfiguracao>.Failure(FormularioNaoEncontrado(codigo));
        }

        List<FieldError> erros = RecusasDosTermos(configuracao, formulario, termos);
        return erros.Count > 0
            ? Result<MudancaDaConfiguracao>.ValidationFailure(erros)
            : Result<MudancaDaConfiguracao>.Success(MudancaDosTermos(configuracao, formulario, termos));
    }

    private static List<FieldError> RecusasDosTermos(
        ConfiguracaoDeclarada configuracao, FormularioProcesso formulario, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(configuracao, formulario.Declarado, configuracao.Coleta.ItensDo(formulario));
        erros.AddRange(CitacoesDosFormularios.RecusasDosTermos(termos, dependencias));

        return erros;
    }

    private static MudancaDaConfiguracao MudancaDosTermos(
        ConfiguracaoDeclarada configuracao, FormularioProcesso formulario, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        MudancaDaConfiguracao mudanca = new() { TermosExigidos = Troca.Substituindo(configuracao.Coleta.TermosDo(formulario), termos) };
        foreach (TermoExigidoFormulario termo in termos)
        {
            termo.VincularFormulario(formulario);
        }

        return mudanca;
    }

    /// <summary>O código do caminho não é de nenhum formulário do processo (remoção, itens e termos).</summary>
    public static DomainError FormularioNaoEncontrado(CodigoDoFormulario codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return new(
            FormularioProcessoErrorCodes.NaoEncontrado,
            $"O processo não tem formulário com o código {codigo.Valor}.");
    }

    /// <summary>O código informado na aplicação de modelo não é de nenhum formulário do processo.</summary>
    public static DomainError FormularioInexistente(CodigoDoFormulario codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return new(
            FormularioProcessoErrorCodes.FormularioInexistente,
            $"O processo não tem formulário com o código {codigo.Valor}; defina o formulário antes de aplicar o modelo.");
    }
}
