namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using System.Text.Json;

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

        // O derivado não coincide com grupo repetível nem com campo de grupo: o código do grupo é único
        // no processo, e o campo é fato de membro, que existe por ocorrência.
        HashSet<string> dosGrupos = new(
            configuracao.Coleta.GruposColetados.SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)), StringComparer.Ordinal);
        HashSet<string> codigos = new(StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            DomainError? recusa = DerivacoesDaOferta.Deriva(config.CodigoFato)
                ? DerivacoesDaOferta.RecusaDeConfiguracao(config.CodigoFato)
                : !codigos.Add(config.CodigoFato)
                    ? new DomainError(
                        ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                        $"O fato '{config.CodigoFato}' tem mais de uma configuração de derivação neste processo.")
                    : dosGrupos.Contains(config.CodigoFato)
                        ? new DomainError(
                            ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                            $"O código '{config.CodigoFato}' já é de um grupo repetível ou de um campo de grupo do processo.")
                        : null;
            if (recusa is not null)
            {
                return Result.Failure(recusa);
            }
        }

        return Result.Success();
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

        List<RecusaDaCopiaDoItem> recusas = [];
        foreach (FatoColetado item in configuracao.Coleta.Campos)
        {
            if (fatosJaColetados.Contains(item.FatoCodigo) || !catalogo.TryGetValue(item.FatoCodigo, out CopiaDoItemNoCatalogo? copia))
            {
                continue;
            }

            recusas.AddRange(item.ConferirCopiaDoCatalogo(copia).Errors.Select(erro => new RecusaDaCopiaDoItem(item, erro)));
        }

        return recusas;
    }

    /// <summary>
    /// Confere os itens e os grupos que o formulário passa a coletar, sem mutar o processo: o teto,
    /// o grafo, as citações, o código e o produtor únicos e as seções.
    /// </summary>
    internal static Result ConferirFatosColetados(
        ConfiguracaoDeclarada configuracao,
        FormularioProcesso formulario, FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> fatosColetados, IReadOnlyList<GrupoColetado> grupos)
    {
        if (FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(fatosColetados.Count, grupos)) is [var excesso, ..])
        {
            return Result.ValidationFailure([excesso]);
        }

        if (Impedimento.ConferirFinalidade(finalidade, fatosColetados.Select(static f => f.Impedimento)) is { Count: > 0 } foraDaInscricao)
        {
            return Result.ValidationFailure(foraDaInscricao);
        }

        // A inscrição só cita os próprios campos; as outras finalidades citam também os dela.
        HashSet<string> daInscricao = finalidade == FinalidadeFormulario.Inscricao
            ? new(StringComparer.Ordinal)
            : CitacoesDosFormularios.FatosDaInscricao(configuracao.Coleta.Itens);
        if (CitacoesDosFormularios.ValidarGrafoDeFatos(formulario.Etapas, fatosColetados, grupos, daInscricao, CitacoesDosFormularios.DependenciasDasDerivacoes(configuracao)) is { } erro)
        {
            return Result.Failure(erro);
        }

        if (finalidade == FinalidadeFormulario.Inscricao && CitacoesDosFormularios.CitacaoQueFicariaOrfa(configuracao, fatosColetados) is { } orfa)
        {
            return Result.Failure(orfa);
        }

        // O código do grupo é único no processo e não coincide com o de fato coletado: um grupo novo
        // não repete grupo nem fato de outro formulário, e um fato novo não repete grupo de outro.
        HashSet<string> deOutrosFormularios = new(
            configuracao.Coleta.Campos.Where(f => f.Finalidade != finalidade).Select(static f => f.FatoCodigo)
                .Concat(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != finalidade).Select(static g => g.Codigo)),
            StringComparer.Ordinal);
        HashSet<string> gruposDeOutros = new(
            configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != finalidade).Select(static g => g.Codigo), StringComparer.Ordinal);
        if ((grupos.Select(static g => g.Codigo).FirstOrDefault(deOutrosFormularios.Contains)
                ?? fatosColetados.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo).FirstOrDefault(gruposDeOutros.Contains))
            is { } codigoRepetido)
        {
            return Result.Failure(new DomainError(
                FatoColetadoErrorCodes.FatoDuplicado,
                $"O código '{codigoRepetido}' já é usado por um fato ou grupo de outro formulário do processo."));
        }

        // Produtor único (UNI-REQ-0144): cada fato é coletado por um só formulário do processo,
        // contando os campos dos grupos.
        HashSet<string> novos = new(fatosColetados.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        if (configuracao.Coleta.Campos.FirstOrDefault(f => f.Finalidade != finalidade && novos.Contains(f.FatoCodigo)) is { } jaProduzido)
        {
            string outro = EstruturaFormulario.ParaToken(jaProduzido.Finalidade);
            return Result.Failure(new DomainError(
                FatoColetadoErrorCodes.FatoDuplicado,
                finalidade == FinalidadeFormulario.Inscricao && ConjuntoBasicoDaInscricao.Fatos.Contains(jaProduzido.FatoCodigo)
                    ? $"O fato '{jaProduzido.FatoCodigo}' é dos dados básicos do candidato, que o formulário de inscrição coleta; tire-o do formulário de {outro} antes."
                    : $"O fato '{jaProduzido.FatoCodigo}' já é coletado pelo formulário de {outro}."));
        }

        IReadOnlyList<FieldError> itensForaDasSecoes = EstruturaFormulario.ValidarItens(
            formulario.Estrutura, CitacoesDosFormularios.ParaEstrutura(fatosColetados, grupos), secaoObrigatoria: false);
        return itensForaDasSecoes.Count > 0 ? Result.ValidationFailure(itensForaDasSecoes) : Result.Success();
    }
}
