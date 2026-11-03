namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDosFormularios
{
    /// <summary>
    /// A aplicação do modelo substitui o formulário inteiro, e a retificação não retira o que o edital
    /// publicou: só em rascunho.
    /// </summary>
    public static DomainError AplicacaoDeModeloSoEmRascunho() => new(
        FormularioProcessoErrorCodes.AplicacaoDeModeloSoEmRascunho,
        "O modelo se aplica só em rascunho: a aplicação substitui o formulário inteiro, e a retificação não retira o que o edital publicou.");

    internal static Result<PlanoDoFormulario> PlanejarDefinicao(
        EstadoDaConfiguracao estado,
        FinalidadeFormulario finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<EtapaFormulario> etapas,
        IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        ArgumentNullException.ThrowIfNull(estado);

        FormularioProcesso? existente = estado.FormularioDe(finalidade);
        IReadOnlyList<FatoColetado>? itensIniciais = existente is null && itensDaCriacao is { Count: > 0 } ? itensDaCriacao : null;
        IReadOnlyList<FieldError> recusas = ConferirFormulario(estado, finalidade, faseId, titulo, etapas, itensDaCriacao: itensIniciais);
        if (recusas.Count > 0)
        {
            return Result<PlanoDoFormulario>.ValidationFailure(recusas);
        }

        // A conferência acima já recusou o que a fábrica recusaria.
        FormularioProcesso novo = FormularioProcesso.Criar(finalidade, faseId, titulo, etapas).Value!;
        if (existente is null
            && itensIniciais is not null
            && RegrasDaColeta.ConferirFatosColetados(estado, novo, finalidade, itensIniciais, []) is { IsFailure: true } recusaDosItens)
        {
            return Result<PlanoDoFormulario>.ValidationFailure(recusaDosItens.Errors);
        }

        return Result<PlanoDoFormulario>.Success(new PlanoDoFormulario(finalidade, novo, existente, itensIniciais));
    }

    /// <summary>
    /// A fase do formulário, se informada, está no cronograma e é a que a finalidade pede; as etapas
    /// têm estrutura válida, acomodam os itens e só citam fatos que o formulário conhece antes delas.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFormulario(
        EstadoDaConfiguracao estado,
        FinalidadeFormulario finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<EtapaFormulario> etapas,
        bool etapasCompletas = true,
        IReadOnlyList<FatoColetado>? itensDaCriacao = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(etapas);

        List<FieldError> recusas = FormaDoCabecalho.Validar(finalidade, titulo);
        if (faseId is { } fase && fase != Guid.Empty && RecusaDaFaseDoFormulario(estado, finalidade, fase) is { } recusaDaFase)
        {
            recusas.Add(new("faseId", recusaDaFase));
        }

        if (etapasCompletas && finalidade != FinalidadeFormulario.Nenhuma)
        {
            recusas.AddRange(RecusasDasEtapas(estado, finalidade, etapas, itensDaCriacao));
        }

        return recusas;
    }

    private static List<FieldError> RecusasDasEtapas(
        EstadoDaConfiguracao estado, FinalidadeFormulario finalidade, IReadOnlyList<EtapaFormulario> etapas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        IReadOnlyList<EtapaEstrutura> estrutura = [.. etapas.Select(static e => e.Estrutura)];
        List<FieldError> recusas = [.. EstruturaFormulario.ValidarEtapas(finalidade, estrutura)];
        if (recusas.Count == 0)
        {
            // Os itens não estão no corpo do formulário: a recusa aponta as etapas que os deixariam fora de
            // seção, e a mensagem nomeia o item. Estrutura recusada suspende só esta conferência.
            recusas.AddRange(EstruturaFormulario
                .ValidarItens(
                    estrutura,
                    itensDaCriacao is null ? ItensDaFinalidade(estado, finalidade) : CitacoesDosFormularios.ParaEstrutura(itensDaCriacao, []),
                    secaoObrigatoria: false)
                .Select(static recusa => recusa with { Field = "etapas" }));
        }

        recusas.AddRange(RecusasDeExibicao(estado, finalidade, etapas, itensDaCriacao));
        return recusas;
    }

    // A exibição de cada seção cita só o que o formulário conhece antes dela, com os itens atuais ou,
    // na criação, com os que o formulário recebe ao nascer.
    private static IEnumerable<FieldError> RecusasDeExibicao(
        EstadoDaConfiguracao estado, FinalidadeFormulario finalidade, IReadOnlyList<EtapaFormulario> etapas, IReadOnlyList<FatoColetado>? itensDaCriacao)
    {
        FatoColetado[] itens = [.. itensDaCriacao ?? [.. estado.Itens.Where(f => f.Finalidade == finalidade)]];
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(estado, finalidade, itens, CitacoesDosFormularios.FatosDaInscricao(estado.Itens));
        EtapaDoGrafo[] etapasDoGrafo = [.. etapas.Select(static e => e.ParaGrafo())];
        ItemDoGrafo[] itensDoGrafo = [.. itens.Select(static i => i.ParaGrafo())];
        GrupoDoGrafo[] gruposDoGrafo = [.. CitacoesDosFormularios.GruposDaFinalidade(estado, finalidade).Select(static g => g.ParaGrafo())];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            DomainError? recusa = GrafoDoFormulario.CitacaoInvalidaDaEtapa(
                    etapasDoGrafo[indice],
                    GrafoDoFormulario.PosicaoDaEtapa(etapasDoGrafo[indice], etapasDoGrafo, itensDoGrafo, gruposDoGrafo),
                    dependencias)
                ?? CitacoesDosFormularios.CitacaoNaoGarantida(
                    estado,
                    CitacoesDosFormularios.DoFormulario(finalidade, CitacoesDosFormularios.CitacoesDaSecao(etapas[indice])),
                    finalidade,
                    CitacoesDosFormularios.SemCatalogo);
            if (recusa is not null)
            {
                yield return new FieldError($"etapas[{indice}].exibicao", recusa);
            }
        }
    }

    internal static DomainError? RecusaDaFaseDoFormulario(EstadoDaConfiguracao estado, FinalidadeFormulario finalidade, Guid faseId)
    {
        if (estado.CronogramaFases.FirstOrDefault(f => f.Id == faseId) is not { } fase)
        {
            return new DomainError(FormularioProcessoErrorCodes.FaseForaDoCronograma, "A fase do formulário não está no cronograma do processo.");
        }

        return fase.Atende(finalidade)
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade,
                $"A fase '{fase.Codigo}' não é a fase em que se responde o formulário de {EstruturaFormulario.ParaToken(finalidade)}.");
    }

    internal static IReadOnlyList<ItemEstrutura> ItensDaFinalidade(EstadoDaConfiguracao estado, FinalidadeFormulario finalidade) =>
        CitacoesDosFormularios.ParaEstrutura(estado.Itens.Where(f => f.Finalidade == finalidade), CitacoesDosFormularios.GruposDaFinalidade(estado, finalidade));

    internal static DomainError RemocaoSoEmRascunho() => new(
        FormularioProcessoErrorCodes.RemocaoSoEmRascunho,
        "A retificação acrescenta formulário, mas não remove o que o edital publicou.");

    internal static void Remover(ColecoesDoProcesso colecoes, FormularioProcesso formulario)
    {
        ArgumentNullException.ThrowIfNull(colecoes);
        ArgumentNullException.ThrowIfNull(formulario);

        colecoes.Formularios.Remove(formulario);
        colecoes.Campos.RemoveAll(f => f.Finalidade == formulario.Finalidade);
        colecoes.GruposColetados.RemoveAll(g => g.Finalidade == formulario.Finalidade);
        colecoes.TermosExigidos.RemoveAll(t => t.Finalidade == formulario.Finalidade);
    }

    internal static Result<FormularioProcesso> ConferirRemocao(EstadoDaConfiguracao estado, FinalidadeFormulario finalidade)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (estado.FormularioDe(finalidade) is not { } formulario)
        {
            return Result<FormularioProcesso>.Failure(FormularioInexistente(finalidade));
        }

        return finalidade == FinalidadeFormulario.Inscricao && CitacoesDosFormularios.CitacaoQueFicariaOrfa(estado, []) is { } orfa
            ? Result<FormularioProcesso>.Failure(orfa)
            : Result<FormularioProcesso>.Success(formulario);
    }

    internal static Result<PlanoDosTermos> PlanejarTermos(
        EstadoDaConfiguracao estado, FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (estado.FormularioDe(finalidade) is null)
        {
            return Result<PlanoDosTermos>.Failure(FormularioInexistente(finalidade));
        }

        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(
            estado, finalidade, estado.Itens.Where(f => f.Finalidade == finalidade), CitacoesDosFormularios.FatosDaInscricao(estado.Itens));
        for (int indice = 0; indice < termos.Count; indice++)
        {
            DomainError? recusa = GrafoDoFormulario.CitacaoInvalidaDoTermo(termos[indice].ParaGrafo(), dependencias)
                ?? CitacoesDosFormularios.CitacaoNaoGarantida(
                    estado,
                    CitacoesDosFormularios.DoFormulario(finalidade, CitacoesDosFormularios.CitacoesDoTermo(termos[indice])),
                    finalidade,
                    CitacoesDosFormularios.SemCatalogo);
            if (recusa is not null)
            {
                erros.Add(new($"termos[{indice}]", recusa));
            }
        }

        return erros.Count > 0
            ? Result<PlanoDosTermos>.ValidationFailure(erros)
            : Result<PlanoDosTermos>.Success(new PlanoDosTermos(finalidade, termos));
    }

    public static DomainError FormularioInexistente(FinalidadeFormulario finalidade) => new(
        FormularioProcessoErrorCodes.FormularioInexistente,
        $"O processo não tem formulário de {EstruturaFormulario.ParaToken(finalidade)}; defina o formulário antes dos itens e termos.");
}

/// <summary>A definição do formulário de uma finalidade: troca o existente ou cria o novo, com os itens iniciais.</summary>
internal sealed record PlanoDoFormulario(
    FinalidadeFormulario Finalidade,
    FormularioProcesso Novo,
    FormularioProcesso? Existente,
    IReadOnlyList<FatoColetado>? ItensIniciais)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        if (Existente is not null)
        {
            Existente.Substituir(Novo);
            return;
        }

        ((IParteDoProcesso)Novo).VincularProcesso(processoId);
        colecoes.Formularios.Add(Novo);
        if (ItensIniciais is not null)
        {
            new PlanoDaColeta(Finalidade, ItensIniciais, []).Aplicar(colecoes, processoId);
        }
    }
}

/// <summary>Os termos que substituem os da finalidade, já vinculados a ela.</summary>
internal sealed record PlanoDosTermos(FinalidadeFormulario Finalidade, IReadOnlyList<TermoExigidoFormulario> Termos)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        colecoes.TermosExigidos.RemoveAll(t => t.Finalidade == Finalidade);
        foreach (TermoExigidoFormulario termo in Termos)
        {
            ((IParteDoProcesso)termo).VincularProcesso(processoId);
            termo.VincularFinalidade(Finalidade);
            colecoes.TermosExigidos.Add(termo);
        }
    }
}
