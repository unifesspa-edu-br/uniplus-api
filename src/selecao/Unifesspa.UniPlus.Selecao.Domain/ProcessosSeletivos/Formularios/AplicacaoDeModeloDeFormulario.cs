namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas de aplicar um modelo de formulário, que substitui o conteúdo do formulário do código, que o processo já
/// precisa ter: ele mantém a identidade, a fase, a obrigatoriedade e a consequência da falta de envio. Todas rodam antes
/// de qualquer mutação, e o formulário novo é conferido por inteiro; nos outros formulários só a citação que a cópia
/// invalida é recusada.
/// </summary>
internal static class AplicacaoDeModeloDeFormulario
{
    internal static Result<PlanoDaCopiaDoModelo> Planejar(ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(copia);

        return configuracao.Coleta.FormularioDe(copia.Formulario) is { } existente
            ? PlanejarSobre(configuracao, copia, existente)
            : Result<PlanoDaCopiaDoModelo>.ValidationFailure([new FieldError("formularioCodigo", RegrasDosFormularios.FormularioInexistente(copia.Formulario))]);
    }

    private static Result<PlanoDaCopiaDoModelo> PlanejarSobre(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso existente)
    {
        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(
            existente.Declarado, configuracao.CronogramaLido(), copia.Titulo, copia.Paginas, copia.Origem);
        if (formulario.IsFailure)
        {
            return Result<PlanoDaCopiaDoModelo>.ValidationFailure(formulario.Errors);
        }

        // O modelo não traz a consequência da falta de envio: a declaração que o processo já tem se mantém.
        formulario.Value!.DeclararConsequenciaDaFaltaDeEnvio(existente.ConsequenciaDaFaltaDeEnvio);
        return ConferirCopia(configuracao, copia, formulario.Value!, existente);
    }

    private static Result<PlanoDaCopiaDoModelo> ConferirCopia(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso existente)
    {
        HashSet<string> daCopia = new(copia.Itens.Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        List<FieldError> erros = [.. ErrosDaCopia(configuracao, copia, novo, existente, daCopia)];
        if (erros.Count > 0)
        {
            return Result<PlanoDaCopiaDoModelo>.ValidationFailure(erros);
        }

        // Substituir mantém a identidade do formulário existente: a cópia se liga a ele.
        VincularAoFormulario(copia, existente);
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
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso existente, HashSet<string> daCopia)
    {
        List<FieldError> erros = [.. FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(copia.Itens.Count, copia.Grupos))];
        erros.AddRange(EstruturaFormulario.ValidarItens(novo.Estrutura, CitacoesDosFormularios.ParaEstrutura(copia.Itens, copia.Grupos), secaoObrigatoria: false));
        erros.AddRange(ItensDeOutrosFormularios(configuracao, existente, daCopia));

        erros.AddRange(CodigosDeOutrosFormularios(configuracao, copia, existente));
        erros.AddRange(copia.DerivacoesNovas
            .Where(static c => DerivacoesDaOferta.Deriva(c.CodigoFato))
            .Select(static c => new FieldError("derivacoes", DerivacoesDaOferta.RecusaDeConfiguracao())));

        erros.AddRange(DerivacoesRepetidas(configuracao, copia));
        return erros;
    }

    // O formulário da inscrição traz para si o item que um formulário fora da inscrição coletava, e a citação desse
    // formulário continua válida. O item que outro formulário da inscrição coleta não se traz: o formulário anterior
    // ficaria com citações inválidas, que a conferência da publicação não alcança nos formulários da inscrição.
    private static IEnumerable<FieldError> ItensDeOutrosFormularios(
        ConfiguracaoDeclarada configuracao, FormularioProcesso existente, HashSet<string> daCopia)
    {
        bool destinoDaInscricao = configuracao.EhDaInscricao(existente.Declarado);
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        return configuracao.Coleta.Itens
            .Where(f => f.FormularioId != existente.Id && daCopia.Contains(f.FatoCodigo)
                && (!destinoDaInscricao || (f.FormularioId is { } dono && daInscricao.Contains(dono))))
            .OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal)
            .Select(f => new FieldError("itens", new DomainError(FatoColetadoErrorCodes.FatoDuplicado,
                $"O fato '{f.FatoCodigo}' já é coletado pelo formulário de {configuracao.Coleta.CodigoDoDono(f)}.")));
    }

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
    // não são trazidos de outro formulário.
    private static IEnumerable<FieldError> CodigosDeOutrosFormularios(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso existente)
    {
        HashSet<string> dosGruposDeOutros = new(
            configuracao.Coleta.GruposColetados.Where(g => g.FormularioId != existente.Id).SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)),
            StringComparer.Ordinal);
        foreach (string codigo in copia.Itens.Select(static i => i.FatoCodigo).Concat(copia.DerivacoesNovas.Select(static c => c.CodigoFato))
            .Where(dosGruposDeOutros.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            yield return new FieldError("itens", UnicidadeEntreFormularios.CodigoDeOutroFormulario(codigo));
        }

        foreach (FieldError recusa in CodigosDosGrupos(configuracao, copia, existente, dosGruposDeOutros))
            yield return recusa;
    }

    private static IEnumerable<FieldError> CodigosDosGrupos(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso existente, HashSet<string> dosGruposDeOutros)
    {
        HashSet<string> deOutros = new(dosGruposDeOutros.Concat(configuracao.Coleta.Itens
            .Where(f => f.FormularioId != existente.Id).Select(static f => f.FatoCodigo)), StringComparer.Ordinal);
        for (int indice = 0; indice < copia.Grupos.Count; indice++)
        {
            GrupoColetado grupo = copia.Grupos[indice];
            if (grupo.Subitens.Select(static s => s.FatoCodigo).Prepend(grupo.Codigo).FirstOrDefault(deOutros.Contains) is { } repetido)
                yield return new FieldError($"grupos[{indice}]", UnicidadeEntreFormularios.CodigoDeOutroFormulario(repetido));
        }
    }

    private static Result<PlanoDaCopiaDoModelo> ConferirEstadoFinal(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso existente, HashSet<string> daCopia)
    {
        EstadoFinalDaCopia estado = new(configuracao, copia, existente, daCopia);
        IReadOnlyList<FieldError> recusas = estado.Conferir(novo);
        return recusas.Count > 0
            ? Result<PlanoDaCopiaDoModelo>.ValidationFailure(recusas)
            : Result<PlanoDaCopiaDoModelo>.Success(new PlanoDaCopiaDoModelo(copia, novo, existente, MudancaDaCopia(configuracao, copia, existente, daCopia)));
    }

    private static MudancaDaConfiguracao MudancaDaCopia(
        ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso existente, HashSet<string> daCopia) => new()
        {
            Campos = Troca.Substituindo(
                configuracao.Coleta.Campos.Where(f => f.FormularioId == existente.Id || (f.GrupoColetadoId is null && daCopia.Contains(f.FatoCodigo))),
                [.. copia.Itens, .. copia.Grupos.SelectMany(static g => g.Subitens)]),
            GruposColetados = Troca.Substituindo(configuracao.Coleta.GruposDo(existente), copia.Grupos),
            TermosExigidos = Troca.Substituindo(configuracao.Coleta.TermosDo(existente), copia.Termos),
            RegrasDerivacao = Troca.Acrescentando(copia.DerivacoesNovas),
        };
}
