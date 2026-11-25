namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDaColeta
{
    internal static Result<MudancaDaConfiguracao> Planejar(
        ConfiguracaoDeclarada configuracao,
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado>? grupos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // Sem grupos informados, os do formulário ficam como estão.
        IReadOnlyList<GrupoColetado> gruposFinais = grupos ?? [.. CitacoesDosFormularios.GruposDaFinalidade(configuracao, finalidade)];
        if (configuracao.Coleta.FormularioDe(finalidade) is not { } formulario)
        {
            return Result<MudancaDaConfiguracao>.Failure(RegrasDosFormularios.FormularioInexistente(finalidade));
        }

        Result conferencia = ConferirFatosColetados(configuracao, formulario, finalidade, fatosColetados, gruposFinais);
        return conferencia.IsFailure
            ? Result<MudancaDaConfiguracao>.ValidationFailure(conferencia.Errors)
            : Result<MudancaDaConfiguracao>.Success(PlanejarTroca(configuracao, finalidade, fatosColetados, gruposFinais));
    }

    /// <summary>
    /// Sai tudo o que a finalidade coletava, itens e campos dos grupos, e entram os itens e os grupos
    /// novos, já vinculados a ela.
    /// </summary>
    internal static MudancaDaConfiguracao PlanejarTroca(
        ConfiguracaoDeclarada configuracao,
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado> grupos)
    {
        MudancaDaConfiguracao mudanca = new()
        {
            Campos = Troca.Substituindo(
                configuracao.Coleta.Campos.Where(f => f.Finalidade == finalidade),
                [.. fatosColetados, .. grupos.SelectMany(static g => g.Subitens)]),
            GruposColetados = Troca.Substituindo(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade == finalidade), grupos),
        };
        VincularFinalidade(finalidade, fatosColetados, grupos);
        return mudanca;
    }

    private static void VincularFinalidade(
        FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> fatosColetados, IReadOnlyList<GrupoColetado> grupos)
    {
        foreach (FatoColetado fato in fatosColetados)
        {
            fato.VincularFinalidade(finalidade);
        }

        foreach (GrupoColetado grupo in grupos)
        {
            grupo.VincularFinalidade(finalidade);
        }
    }

    internal static Result ConferirDerivacoes(ConfiguracaoDeclarada configuracao, IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        HashSet<string> dosGrupos = new(
            configuracao.Coleta.GruposColetados.SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)), StringComparer.Ordinal);
        return ConferirDerivacoesContraOsGrupos(regrasDerivacao, dosGrupos);
    }

    private static Result ConferirDerivacoesContraOsGrupos(
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao, HashSet<string> dosGrupos)
    {
        HashSet<string> codigos = new(StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato derivacao in regrasDerivacao)
        {
            DomainError? recusa = RecusaDaDerivacao(derivacao, codigos, dosGrupos);
            if (recusa is not null)
            {
                return Result.Failure(recusa);
            }
        }

        return Result.Success();
    }

    private static DomainError? RecusaDaDerivacao(
        ConfiguracaoDerivacaoFato configuracao, HashSet<string> codigos, HashSet<string> dosGrupos)
    {
        return DerivacoesDaOferta.Deriva(configuracao.CodigoFato)
            ? DerivacoesDaOferta.RecusaDeConfiguracao(configuracao.CodigoFato)
            : !codigos.Add(configuracao.CodigoFato)
                ? new DomainError(
                    ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                    $"O fato '{configuracao.CodigoFato}' tem mais de uma configuração de derivação neste processo.")
                : dosGrupos.Contains(configuracao.CodigoFato)
                    ? new DomainError(
                        ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                        $"O código '{configuracao.CodigoFato}' já é de um grupo repetível ou de um campo de grupo do processo.")
                    : null;
    }

    internal static Result<PlanoDaRecopiaDoCatalogo> PlanejarRecopia(
        ConfiguracaoDeclarada configuracao, IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        IReadOnlyList<RecusaDaCopiaDoItem> recusas = ConferirCopiasDoCatalogo(configuracao, catalogo, fatosJaColetados);
        if (recusas.Count > 0)
        {
            return Result<PlanoDaRecopiaDoCatalogo>.ValidationFailure([.. recusas.Select(static r => r.Erro)]);
        }

        return Result<PlanoDaRecopiaDoCatalogo>.Success(new PlanoDaRecopiaDoCatalogo(
            [.. configuracao.Coleta.Campos
                .Where(item => catalogo.ContainsKey(item.FatoCodigo))
                .Select(item => (item, catalogo[item.FatoCodigo], fatosJaColetados.Contains(item.FatoCodigo)))]));
    }

    /// <summary>
    /// Confere, sem mudar o processo, a cópia que cada item e cada campo de grupo tirou do catálogo
    /// — formato e classificação de proteção — e devolve a recusa de cada um com o item de origem,
    /// para quem chama dizer onde ele está. O fato que alguma versão publicada já coletou não é
    /// conferido: a retificação não troca os eixos dele. Acumula todas as recusas (ADR-0125).
    /// </summary>
    /// <param name="catalogo">A cópia que o catálogo de hoje tem de cada fato, por código.</param>
    /// <param name="fatosJaColetados">Os fatos que alguma versão publicada coletou, por código ordinal.</param>
    public static IReadOnlyList<RecusaDaCopiaDoItem> ConferirCopiasDoCatalogo(
        ConfiguracaoDeclarada configuracao,
        IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(fatosJaColetados);

        return [.. RecusasDasCopias(configuracao.Coleta.Campos, catalogo, fatosJaColetados)];
    }

    private static IEnumerable<RecusaDaCopiaDoItem> RecusasDasCopias(
        IEnumerable<FatoColetado> itens, IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        foreach (FatoColetado item in itens)
        {
            if (fatosJaColetados.Contains(item.FatoCodigo) || !catalogo.TryGetValue(item.FatoCodigo, out CopiaDoItemNoCatalogo? copia))
                continue;
            foreach (FieldError erro in item.ConferirCopiaDoCatalogo(copia).Errors)
                yield return new RecusaDaCopiaDoItem(item, erro);
        }
    }

    /// <summary>
    /// Confere os itens e os grupos que o formulário passa a coletar, sem mutar o processo: o teto,
    /// o grafo, as citações, o código e o produtor únicos e as seções.
    /// </summary>
    internal static Result ConferirFatosColetados(
        ConfiguracaoDeclarada configuracao,
        FormularioProcesso formulario, FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> fatosColetados, IReadOnlyList<GrupoColetado> grupos)
    {
        Result forma = ConferirForma(finalidade, fatosColetados, grupos);
        if (forma.IsFailure) return forma;
        Result grafo = ConferirGrafo(configuracao, formulario, finalidade, fatosColetados, grupos);
        if (grafo.IsFailure) return grafo;
        if (finalidade == FinalidadeFormulario.Inscricao && CitacoesDosFormularios.CitacaoQueFicariaOrfa(configuracao, fatosColetados) is { } orfa)
            return Result.Failure(orfa);
        UnicidadeEntreFormularios unicidade = new(configuracao, finalidade, fatosColetados, grupos);
        Result codigos = unicidade.ConferirCodigos();
        if (codigos.IsFailure) return codigos;
        Result produtores = unicidade.ConferirProdutor();
        if (produtores.IsFailure) return produtores;
        IReadOnlyList<FieldError> foraDasSecoes = EstruturaFormulario.ValidarItens(
            formulario.Estrutura, CitacoesDosFormularios.ParaEstrutura(fatosColetados, grupos), secaoObrigatoria: false);
        return foraDasSecoes.Count > 0 ? Result.ValidationFailure(foraDasSecoes) : Result.Success();
    }

    private static Result ConferirForma(FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
    {
        if (FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(fatos.Count, grupos)) is [var excesso, ..])
            return Result.ValidationFailure([excesso]);
        if (Impedimento.ConferirFinalidade(finalidade, fatos.Select(static f => f.Impedimento)) is { Count: > 0 } foraDaInscricao)
            return Result.ValidationFailure(foraDaInscricao);
        return Result.Success();
    }

    private static Result ConferirGrafo(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario,
        FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
    {
        HashSet<string> daInscricao = finalidade == FinalidadeFormulario.Inscricao
            ? new(StringComparer.Ordinal) : CitacoesDosFormularios.FatosDaInscricao(configuracao.Coleta.Itens);
        return CitacoesDosFormularios.ValidarGrafoDeFatos(formulario.Paginas, fatos, grupos, daInscricao,
            CitacoesDosFormularios.DependenciasDasDerivacoes(configuracao)) is { } erro ? Result.Failure(erro) : Result.Success();
    }
}
