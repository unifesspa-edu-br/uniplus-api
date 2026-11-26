namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
        FinalidadeFormulario finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        IReadOnlyList<FatoColetado>? itensDaCriacao,
        ConsequenciaDoDescumprimento? consequenciaDaFaltaDeEnvio)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        FormularioProcesso? existente = configuracao.Coleta.FormularioDe(finalidade);
        IReadOnlyList<FatoColetado>? itensIniciais = existente is null && itensDaCriacao is { Count: > 0 } ? itensDaCriacao : null;
        IReadOnlyList<FieldError> recusas = ConferirFormulario(configuracao, finalidade, faseId, titulo, paginas, itensDaCriacao: itensIniciais);
        if (recusas.Count > 0)
        {
            return Result<PlanoDoFormulario>.ValidationFailure(recusas);
        }

        // A conferência anterior já recusou o que a fábrica recusaria.
        FormularioProcesso novo = FormularioProcesso.Criar(finalidade, faseId, titulo, paginas).Value!;
        novo.DeclararConsequenciaDaFaltaDeEnvio(consequenciaDaFaltaDeEnvio);
        return MontarPlano(configuracao, novo, existente, itensIniciais);
    }

    private static Result<PlanoDoFormulario> MontarPlano(
        ConfiguracaoDeclarada configuracao, FormularioProcesso novo, FormularioProcesso? existente, IReadOnlyList<FatoColetado>? itensIniciais)
    {
        FinalidadeFormulario finalidade = novo.Finalidade;
        Result itens = itensIniciais is null
            ? Result.Success()
            : RegrasDaColeta.ConferirFatosColetados(configuracao, novo, finalidade, itensIniciais, []);
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
    /// A fase do formulário, se informada, está no cronograma e é a que a finalidade pede; as páginas
    /// têm estrutura válida, acomodam os itens e só citam fatos que o formulário conhece antes delas.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFormulario(
        ConfiguracaoDeclarada configuracao,
        FinalidadeFormulario? finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        bool paginasCompletas = true,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(paginas);

        List<FieldError> recusas = FormaDoCabecalho.Validar(finalidade, titulo);
        AcrescentarRecusaDaFase(recusas, configuracao, finalidade, faseId);

        if (paginasCompletas && finalidade is { } formulario)
        {
            recusas.AddRange(RecusasDasPaginas(configuracao, formulario, paginas, itensDaCriacao));
        }

        return recusas;
    }

    private static void AcrescentarRecusaDaFase(
        List<FieldError> recusas, ConfiguracaoDeclarada configuracao, FinalidadeFormulario? finalidade, Guid? faseId)
    {
        if (finalidade is { } declarada && faseId is { } fase && fase != Guid.Empty && RecusaDaFaseDoFormulario(configuracao, declarada, fase) is { } recusa)
        {
            recusas.Add(new("faseId", recusa));
        }
    }

    private static List<FieldError> RecusasDasPaginas(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, IReadOnlyList<PaginaDoFormulario> paginas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        IReadOnlyList<PaginaEstrutura> estrutura = [.. paginas.Select(static e => e.Estrutura)];
        List<FieldError> recusas = [.. EstruturaFormulario.ValidarPaginas(CorrespondenciaDaFinalidade.DeclaracaoDa(finalidade), estrutura)];
        if (recusas.Count == 0)
        {
            recusas.AddRange(RecusasDosItens(configuracao, finalidade, estrutura, itensDaCriacao));
        }

        recusas.AddRange(RecusasDeExibicao(configuracao, finalidade, paginas, itensDaCriacao));
        return recusas;
    }

    // Os itens não estão no corpo do formulário; a recusa aponta as páginas e nomeia o item.
    private static IEnumerable<FieldError> RecusasDosItens(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, IReadOnlyList<PaginaEstrutura> estrutura, IReadOnlyList<FatoColetado>? itensDaCriacao) =>
        EstruturaFormulario.ValidarItens(
            estrutura,
            itensDaCriacao is null ? ItensDaFinalidade(configuracao, finalidade) : CitacoesDosFormularios.ParaEstrutura(itensDaCriacao, []),
            secaoObrigatoria: false)
        .Select(static recusa => recusa with { Field = EstruturaFormulario.CaminhoDasPaginas });

    // A exibição de cada seção cita só o que o formulário conhece antes dela, com os itens atuais ou,
    // na criação, com os que o formulário recebe ao nascer.
    private static IEnumerable<FieldError> RecusasDeExibicao(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, IReadOnlyList<PaginaDoFormulario> paginas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        FatoColetado[] itens = [.. itensDaCriacao ?? [.. configuracao.Coleta.Itens.Where(f => f.Finalidade == finalidade)]];
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(configuracao, finalidade, itens, CitacoesDosFormularios.FatosDaInscricao(configuracao.Coleta.Itens));
        PaginaDoGrafo[] paginasDoGrafo = [.. paginas.Select(static e => e.ParaGrafo())];
        ItemDoGrafo[] itensDoGrafo = [.. itens.Select(static i => i.ParaGrafo())];
        GrupoDoGrafo[] gruposDoGrafo = [.. CitacoesDosFormularios.GruposDaFinalidade(configuracao, finalidade).Select(static g => g.ParaGrafo())];
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

    internal static DomainError? RecusaDaFaseDoFormulario(ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, Guid faseId)
    {
        if (configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == faseId) is not { } fase)
        {
            return new DomainError(FormularioProcessoErrorCodes.FaseForaDoCronograma, "A fase do formulário não está no cronograma do processo.");
        }

        return fase.Atende(finalidade)
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade,
                $"A fase '{fase.Codigo}' não é a fase em que se responde o formulário de {finalidade.Codigo()}.");
    }

    internal static IReadOnlyList<ItemEstrutura> ItensDaFinalidade(ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade) =>
        CitacoesDosFormularios.ParaEstrutura(configuracao.Coleta.Itens.Where(f => f.Finalidade == finalidade), CitacoesDosFormularios.GruposDaFinalidade(configuracao, finalidade));

    internal static Result<MudancaDaConfiguracao> PlanejarRemocao(ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (configuracao.Coleta.FormularioDe(finalidade) is not { } formulario)
        {
            return Result<MudancaDaConfiguracao>.Failure(FormularioInexistente(finalidade));
        }

        return finalidade == FinalidadeFormulario.Inscricao && CitacoesDosFormularios.CitacaoQueFicariaOrfa(configuracao, []) is { } orfa
            ? Result<MudancaDaConfiguracao>.Failure(orfa)
            : Result<MudancaDaConfiguracao>.Success(MudancaDaRemocao(configuracao, formulario));
    }

    private static MudancaDaConfiguracao MudancaDaRemocao(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) => new()
    {
        Formularios = Troca.Retirando([formulario]),
        Campos = Troca.Retirando(configuracao.Coleta.Campos.Where(f => f.Finalidade == formulario.Finalidade)),
        GruposColetados = Troca.Retirando(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade == formulario.Finalidade)),
        TermosExigidos = Troca.Retirando(configuracao.Coleta.TermosExigidos.Where(t => t.Finalidade == formulario.Finalidade)),
    };

    internal static Result<MudancaDaConfiguracao> PlanejarTermos(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (configuracao.Coleta.FormularioDe(finalidade) is not { } formulario)
        {
            return Result<MudancaDaConfiguracao>.Failure(FormularioInexistente(finalidade));
        }

        List<FieldError> erros = RecusasDosTermos(configuracao, finalidade, termos);
        return erros.Count > 0
            ? Result<MudancaDaConfiguracao>.ValidationFailure(erros)
            : Result<MudancaDaConfiguracao>.Success(MudancaDosTermos(configuracao, formulario, termos));
    }

    private static List<FieldError> RecusasDosTermos(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(
            configuracao, finalidade, configuracao.Coleta.Itens.Where(f => f.Finalidade == finalidade), CitacoesDosFormularios.FatosDaInscricao(configuracao.Coleta.Itens));
        erros.AddRange(CitacoesDosFormularios.RecusasDosTermos(termos, dependencias));

        return erros;
    }

    private static MudancaDaConfiguracao MudancaDosTermos(
        ConfiguracaoDeclarada configuracao, FormularioProcesso formulario, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        MudancaDaConfiguracao mudanca = new() { TermosExigidos = Troca.Substituindo(configuracao.Coleta.TermosExigidos.Where(t => t.Finalidade == formulario.Finalidade), termos) };
        foreach (TermoExigidoFormulario termo in termos)
        {
            termo.VincularFormulario(formulario);
        }

        return mudanca;
    }

    public static DomainError FormularioInexistente(FinalidadeFormulario finalidade) => new(
        FormularioProcessoErrorCodes.FormularioInexistente,
        $"O processo não tem formulário de {finalidade.Codigo()}; defina o formulário antes dos itens e termos.");
}
