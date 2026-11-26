namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas de aplicar um modelo de formulário, que substitui o formulário inteiro da finalidade.
/// Todas rodam antes de qualquer mutação, e o formulário novo é conferido por inteiro; nos outros
/// formulários só a citação que a cópia invalida é recusada.
/// </summary>
internal static class AplicacaoDeModeloDeFormulario
{
    internal static Result<PlanoDaCopiaDoModelo> Planejar(ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        FinalidadeFormulario finalidade = copia.Finalidade;
        FormularioProcesso? existente = configuracao.Coleta.FormularioDe(finalidade);
        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(
            finalidade, existente?.FaseId, copia.Titulo, copia.Paginas, copia.Origem);
        if (formulario.IsFailure)
        {
            return Result<PlanoDaCopiaDoModelo>.ValidationFailure(formulario.Errors);
        }

        // O modelo não traz a consequência da falta de envio: a declaração que o processo já tem se mantém.
        formulario.Value!.DeclararConsequenciaDaFaltaDeEnvio(existente?.ConsequenciaDaFaltaDeEnvio);
        return ConferirCopia(configuracao, copia, formulario.Value!, existente);
    }

    private static Result<PlanoDaCopiaDoModelo> ConferirCopia(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso? existente)
    {
        HashSet<string> daCopia = new(copia.Itens.Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        List<FieldError> erros = [.. ErrosDaCopia(configuracao, copia, novo, daCopia)];
        if (erros.Count > 0)
        {
            return Result<PlanoDaCopiaDoModelo>.ValidationFailure(erros);
        }

        // Substituir mantém a identidade do formulário existente; só sem ele o formulário novo é quem fica.
        VincularAoFormulario(copia, existente ?? novo);
        return ConferirEstadoFinal(configuracao, copia, novo, existente, daCopia);
    }

    private static void VincularAoFormulario(CopiaDeModeloDeFormulario copia, FormularioProcesso formulario)
    {
        foreach (FatoColetado item in copia.Itens)
        {
            item.VincularFormulario(formulario);
        }

        foreach (GrupoColetado grupo in copia.Grupos)
        {
            grupo.VincularFormulario(formulario);
        }

        foreach (TermoExigidoFormulario termo in copia.Termos)
        {
            termo.VincularFormulario(formulario);
        }
    }

    private static List<FieldError> ErrosDaCopia(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, HashSet<string> daCopia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        List<FieldError> erros = [.. FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(copia.Itens.Count, copia.Grupos))];
        erros.AddRange(EstruturaFormulario.ValidarItens(novo.Estrutura, CitacoesDosFormularios.ParaEstrutura(copia.Itens, copia.Grupos), secaoObrigatoria: false));
        erros.AddRange(ItensDeOutrosFormularios(configuracao, finalidade, daCopia));

        erros.AddRange(CodigosDeOutrosFormularios(configuracao, copia));
        erros.AddRange(copia.DerivacoesNovas
            .Where(static c => DerivacoesDaOferta.Deriva(c.CodigoFato))
            .Select(static c => new FieldError("derivacoes", DerivacoesDaOferta.RecusaDeConfiguracao(c.CodigoFato))));

        erros.AddRange(DerivacoesRepetidas(configuracao, copia));
        return erros;
    }

    private static IEnumerable<FieldError> ItensDeOutrosFormularios(
        ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade, HashSet<string> daCopia) =>
        finalidade == FinalidadeFormulario.Inscricao ? [] : configuracao.Coleta.Itens
            .Where(f => f.Finalidade != finalidade && daCopia.Contains(f.FatoCodigo))
            .OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal)
            .Select(f => new FieldError("itens", new DomainError(FatoColetadoErrorCodes.FatoDuplicado,
                $"O fato '{f.FatoCodigo}' já é coletado pelo formulário de {f.Finalidade!.Value.Codigo()}.")));

    private static IEnumerable<FieldError> DerivacoesRepetidas(ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia)
    {
        HashSet<string> derivados = new(configuracao.Coleta.RegrasDerivacao.Select(static c => c.CodigoFato), StringComparer.Ordinal);
        return copia.DerivacoesNovas.Where(c => !derivados.Add(c.CodigoFato))
            .Select(static c => new FieldError("derivacoes", new DomainError(ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                $"O fato '{c.CodigoFato}' tem mais de uma configuração de derivação neste processo.")));
    }

    // A cópia substitui os grupos do próprio formulário; os dos outros continuam, e nem item, nem grupo,
    // nem campo de grupo, nem derivação da cópia repete o código de um deles ou de um campo deles. O
    // grupo e o campo de grupo da cópia também não repetem fato de outro formulário: diferente do item,
    // não são trazidos de outra finalidade.
    private static IEnumerable<FieldError> CodigosDeOutrosFormularios(ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        HashSet<string> dosGruposDeOutros = new(
            configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != finalidade).SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)),
            StringComparer.Ordinal);
        foreach (string codigo in copia.Itens.Select(static i => i.FatoCodigo).Concat(copia.DerivacoesNovas.Select(static c => c.CodigoFato))
            .Where(dosGruposDeOutros.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            yield return new FieldError("itens", UnicidadeEntreFormularios.CodigoDeOutroFormulario(codigo));
        }

        foreach (FieldError recusa in CodigosDosGrupos(configuracao, copia, dosGruposDeOutros))
            yield return recusa;
    }

    private static IEnumerable<FieldError> CodigosDosGrupos(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, HashSet<string> dosGruposDeOutros)
    {
        HashSet<string> deOutros = new(dosGruposDeOutros.Concat(configuracao.Coleta.Itens
            .Where(f => f.Finalidade != copia.Finalidade).Select(static f => f.FatoCodigo)), StringComparer.Ordinal);
        for (int indice = 0; indice < copia.Grupos.Count; indice++)
        {
            GrupoColetado grupo = copia.Grupos[indice];
            if (grupo.Subitens.Select(static s => s.FatoCodigo).Prepend(grupo.Codigo).FirstOrDefault(deOutros.Contains) is { } repetido)
                yield return new FieldError($"grupos[{indice}]", UnicidadeEntreFormularios.CodigoDeOutroFormulario(repetido));
        }
    }

    private static Result<PlanoDaCopiaDoModelo> ConferirEstadoFinal(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso? existente, HashSet<string> daCopia)
    {
        EstadoFinalDaCopia estado = new(configuracao, copia, daCopia);
        IReadOnlyList<FieldError> recusas = estado.Conferir(novo);
        return recusas.Count > 0
            ? Result<PlanoDaCopiaDoModelo>.ValidationFailure(recusas)
            : Result<PlanoDaCopiaDoModelo>.Success(new PlanoDaCopiaDoModelo(copia, novo, existente, MudancaDaCopia(configuracao, copia, novo, existente, daCopia)));
    }

    private static MudancaDaConfiguracao MudancaDaCopia(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso? existente, HashSet<string> daCopia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        return new MudancaDaConfiguracao
        {
            Formularios = existente is null ? Troca.Acrescentando([novo]) : default,
            Campos = Troca.Substituindo(
                configuracao.Coleta.Campos.Where(f => f.Finalidade == finalidade || (f.GrupoColetadoId is null && daCopia.Contains(f.FatoCodigo))),
                [.. copia.Itens, .. copia.Grupos.SelectMany(static g => g.Subitens)]),
            GruposColetados = Troca.Substituindo(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade == finalidade), copia.Grupos),
            TermosExigidos = Troca.Substituindo(configuracao.Coleta.TermosExigidos.Where(t => t.Finalidade == finalidade), copia.Termos),
            RegrasDerivacao = Troca.Acrescentando(copia.DerivacoesNovas),
        };
    }
}
