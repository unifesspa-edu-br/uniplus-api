namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a aplicação do modelo grava: o formulário novo no lugar do existente e a cópia dos itens, grupos, termos e derivações.</summary>
internal sealed record PlanoDoModelo(
    CopiaDeModeloDeFormulario Copia,
    FormularioProcesso Novo,
    FormularioProcesso? Existente,
    IReadOnlySet<string> FatosDaCopia)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        FinalidadeFormulario finalidade = Copia.Finalidade;
        if (Existente is not null)
        {
            Existente.Substituir(Novo);
            Existente.RegistrarOrigem(Copia.ModeloId, Copia.ModeloCodigo);
        }
        else
        {
            ((IParteDoProcesso)Novo).VincularProcesso(processoId);
            colecoes.Formularios.Add(Novo);
        }

        colecoes.Campos.RemoveAll(f => f.Finalidade == finalidade || (f.GrupoColetadoId is null && FatosDaCopia.Contains(f.FatoCodigo)));
        PartesDoProcesso.Substituir(colecoes.Campos, static _ => false, Copia.Itens, processoId);
        new PlanoDaColeta(finalidade, [], Copia.Grupos).AplicarGrupos(colecoes, processoId);
        PartesDoProcesso.Substituir(colecoes.TermosExigidos, t => t.Finalidade == finalidade, Copia.Termos, processoId);
        PartesDoProcesso.Substituir(colecoes.RegrasDerivacao, static _ => false, Copia.DerivacoesNovas, processoId);
    }
}

/// <summary>
/// As recusas de aplicar um modelo de formulário, que substitui o formulário inteiro da finalidade.
/// Todas rodam antes de qualquer mutação, e o formulário novo é conferido por inteiro; nos outros
/// formulários só a citação que a cópia invalida é recusada.
/// </summary>
internal static class AplicacaoDeModeloDeFormulario
{
    internal static Result<PlanoDoModelo> Planejar(EstadoDaConfiguracao estado, CopiaDeModeloDeFormulario copia)
    {
        ArgumentNullException.ThrowIfNull(estado);

        FinalidadeFormulario finalidade = copia.Finalidade;
        FormularioProcesso? existente = estado.FormularioDe(finalidade);
        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(
            finalidade, existente?.FaseId, copia.Titulo, copia.Etapas, copia.ModeloId, copia.ModeloCodigo);
        if (formulario.IsFailure)
        {
            return Result<PlanoDoModelo>.ValidationFailure(formulario.Errors);
        }

        FormularioProcesso novo = formulario.Value!;
        HashSet<string> daCopia = new(copia.Itens.Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        List<FieldError> erros = [.. ErrosDaCopia(estado, copia, novo, daCopia)];
        if (erros.Count > 0)
        {
            return Result<PlanoDoModelo>.ValidationFailure(erros);
        }

        VincularFinalidadeAosItens(copia);
        Result<PlanoDoModelo> recusaDoEstadoFinal = ConferirEstadoFinal(estado, copia, novo, existente, daCopia);
        return recusaDoEstadoFinal;
    }

    private static void VincularFinalidadeAosItens(CopiaDeModeloDeFormulario copia)
    {
        foreach (FatoColetado item in copia.Itens)
        {
            item.VincularFinalidade(copia.Finalidade);
        }

        foreach (GrupoColetado grupo in copia.Grupos)
        {
            grupo.VincularFinalidade(copia.Finalidade);
        }

        foreach (TermoExigidoFormulario termo in copia.Termos)
        {
            termo.VincularFinalidade(copia.Finalidade);
        }
    }

    private static List<FieldError> ErrosDaCopia(
        EstadoDaConfiguracao estado, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, HashSet<string> daCopia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        List<FieldError> erros = [.. FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(copia.Itens.Count, copia.Grupos))];
        erros.AddRange(EstruturaFormulario.ValidarItens(novo.Estrutura, CitacoesDosFormularios.ParaEstrutura(copia.Itens, copia.Grupos), secaoObrigatoria: false));
        if (finalidade != FinalidadeFormulario.Inscricao)
        {
            erros.AddRange(estado.Itens
                .Where(f => f.Finalidade != finalidade && daCopia.Contains(f.FatoCodigo))
                .OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal)
                .Select(f => new FieldError("itens", new DomainError(
                    FatoColetadoErrorCodes.FatoDuplicado,
                    $"O fato '{f.FatoCodigo}' já é coletado pelo formulário de {EstruturaFormulario.ParaToken(f.Finalidade)}."))));
        }

        erros.AddRange(CodigosDeOutrosFormularios(estado, copia));
        erros.AddRange(copia.DerivacoesNovas
            .Where(static c => DerivacoesDaOferta.Deriva(c.CodigoFato))
            .Select(static c => new FieldError("derivacoes", DerivacoesDaOferta.RecusaDeConfiguracao(c.CodigoFato))));

        // A cópia não repete o que o processo grava. O egresso que a oferta supre pela regra do sistema
        // cede lugar ao que a cópia traz, como cede ao que o operador configura.
        HashSet<string> derivados = new(estado.RegrasDerivacao.Select(static c => c.CodigoFato), StringComparer.Ordinal);
        erros.AddRange(copia.DerivacoesNovas
            .Where(c => !derivados.Add(c.CodigoFato))
            .Select(static c => new FieldError("derivacoes", new DomainError(
                ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                $"O fato '{c.CodigoFato}' tem mais de uma configuração de derivação neste processo."))));
        return erros;
    }

    // A cópia substitui os grupos do próprio formulário; os dos outros continuam, e nem item, nem grupo,
    // nem campo de grupo, nem derivação da cópia repete o código de um deles ou de um campo deles. O
    // grupo e o campo de grupo da cópia também não repetem fato de outro formulário: diferente do item,
    // não são trazidos de outra finalidade.
    private static IEnumerable<FieldError> CodigosDeOutrosFormularios(EstadoDaConfiguracao estado, CopiaDeModeloDeFormulario copia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        HashSet<string> dosGruposDeOutros = new(
            estado.GruposColetados.Where(g => g.Finalidade != finalidade).SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)),
            StringComparer.Ordinal);
        foreach (string codigo in copia.Itens.Select(static i => i.FatoCodigo).Concat(copia.DerivacoesNovas.Select(static c => c.CodigoFato))
            .Where(dosGruposDeOutros.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            yield return new FieldError("itens", CodigoDeOutroFormulario(codigo));
        }

        HashSet<string> deOutros = new(
            dosGruposDeOutros.Concat(estado.Itens.Where(f => f.Finalidade != finalidade).Select(static f => f.FatoCodigo)), StringComparer.Ordinal);
        for (int indice = 0; indice < copia.Grupos.Count; indice++)
        {
            GrupoColetado grupo = copia.Grupos[indice];
            if (grupo.Subitens.Select(static s => s.FatoCodigo).Prepend(grupo.Codigo).FirstOrDefault(deOutros.Contains) is { } repetido)
            {
                yield return new FieldError($"grupos[{indice}]", CodigoDeOutroFormulario(repetido));
            }
        }
    }

    private static DomainError CodigoDeOutroFormulario(string codigo) => new(
        FatoColetadoErrorCodes.FatoDuplicado, $"O código '{codigo}' já é usado por um fato ou grupo de outro formulário do processo.");

    // O estado final: os outros formulários sem o que a cópia trouxe para cá, e a cópia.
    private static Result<PlanoDoModelo> ConferirEstadoFinal(
        EstadoDaConfiguracao estado, CopiaDeModeloDeFormulario copia, FormularioProcesso novo, FormularioProcesso? existente, HashSet<string> daCopia)
    {
        FinalidadeFormulario finalidade = copia.Finalidade;
        FatoColetado[] itensFinais = [.. estado.Itens.Where(f => f.Finalidade != finalidade && !daCopia.Contains(f.FatoCodigo)), .. copia.Itens];
        IReadOnlyList<ConfiguracaoDerivacaoFato> derivacoesFinais = DerivacoesDaOferta.Efetivas([.. estado.RegrasDerivacao, .. copia.DerivacoesNovas], estado.DistribuicaoVagas);
        TermoExigidoFormulario[] termosFinais = [.. estado.TermosExigidos.Where(t => t.Finalidade != finalidade), .. copia.Termos];
        GrupoColetado[] gruposFinais = [.. estado.GruposColetados.Where(g => g.Finalidade != finalidade), .. copia.Grupos];
        Dictionary<string, IReadOnlyCollection<string>> dependenciasDasDerivacoes = CitacoesDosFormularios.MapaDeDerivacoes(derivacoesFinais);
        HashSet<string> daInscricao = finalidade == FinalidadeFormulario.Inscricao
            ? new(StringComparer.Ordinal)
            : CitacoesDosFormularios.FatosDaInscricao(itensFinais);

        if (CitacoesDosFormularios.ValidarGrafoDeFatos(novo.Etapas, copia.Itens, copia.Grupos, daInscricao, dependenciasDasDerivacoes) is { } grafo)
        {
            return Result<PlanoDoModelo>.ValidationFailure([new("itens", grafo)]);
        }

        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. copia.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(finalidade, copia.Itens, CitacoesDosFormularios.FatosDaInscricao(itensFinais), dependenciasDasDerivacoes);
        for (int indice = 0; indice < copia.Termos.Count; indice++)
        {
            if (GrafoDoFormulario.CitacaoInvalidaDoTermo(copia.Termos[indice].ParaGrafo(), dependencias) is { } recusa)
            {
                erros.Add(new($"termos[{indice}]", recusa));
            }
        }

        if (erros.Count > 0)
        {
            return Result<PlanoDoModelo>.ValidationFailure(erros);
        }

        // O formulário novo é conferido por inteiro; nos outros, só a citação que a cópia invalida é
        // recusada, e a que já era inválida fica para a publicação. A cópia fora da inscrição não muda
        // o que os outros formulários conhecem.
        IEnumerable<DomainError> orfasDosOutros = finalidade == FinalidadeFormulario.Inscricao
            ? CitacoesDosFormularios.CitacoesQueFicariamOrfas(estado, daCopia, outra => [.. itensFinais.Where(f => f.Finalidade == outra)], dependenciasDasDerivacoes)
            : [];
        FieldError[] orfas = [.. orfasDosOutros
            .Concat(CitacoesDosFormularios.CitacaoInvalidaNosFormularios([novo], itensFinais, termosFinais, derivacoesFinais, gruposFinais) is { } doNovo ? [doNovo] : [])
            .Select(static recusa => new FieldError(string.Empty, recusa))];
        return orfas.Length > 0
            ? Result<PlanoDoModelo>.ValidationFailure(orfas)
            : Result<PlanoDoModelo>.Success(new PlanoDoModelo(copia, novo, existente, daCopia));
    }
}
