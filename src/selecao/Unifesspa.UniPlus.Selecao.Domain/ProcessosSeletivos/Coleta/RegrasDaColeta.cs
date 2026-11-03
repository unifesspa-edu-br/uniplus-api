namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDaColeta
{
    internal static Result<PlanoDaColeta> Planejar(
        EstadoDaConfiguracao estado,
        FinalidadeFormulario finalidade,
        IReadOnlyList<FatoColetado> fatosColetados,
        IReadOnlyList<GrupoColetado>? grupos)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // Sem grupos informados, os do formulário ficam como estão.
        IReadOnlyList<GrupoColetado> gruposFinais = grupos ?? [.. CitacoesDosFormularios.GruposDaFinalidade(estado, finalidade)];
        if (estado.FormularioDe(finalidade) is not { } formulario)
        {
            return Result<PlanoDaColeta>.Failure(RegrasDosFormularios.FormularioInexistente(finalidade));
        }

        Result conferencia = ConferirFatosColetados(estado, formulario, finalidade, fatosColetados, gruposFinais);
        return conferencia.IsFailure
            ? Result<PlanoDaColeta>.ValidationFailure(conferencia.Errors)
            : Result<PlanoDaColeta>.Success(new PlanoDaColeta(finalidade, fatosColetados, gruposFinais));
    }

    internal static Result ConferirDerivacoes(EstadoDaConfiguracao estado, IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // O derivado não coincide com grupo repetível nem com campo de grupo: o código do grupo é único
        // no processo, e o campo é fato de membro, que existe por ocorrência.
        HashSet<string> dosGrupos = new(
            estado.GruposColetados.SelectMany(static g => g.Subitens.Select(static s => s.FatoCodigo).Append(g.Codigo)), StringComparer.Ordinal);
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

    internal static void LimparParaRestauracao(ColecoesDoProcesso colecoes)
    {
        ArgumentNullException.ThrowIfNull(colecoes);

        colecoes.Campos.Clear();
        colecoes.GruposColetados.Clear();
        colecoes.RegrasDerivacao.Clear();
        colecoes.TermosExigidos.Clear();
        colecoes.Formularios.Clear();
    }

    internal static Result<PlanoDaRecopiaDoCatalogo> PlanejarRecopia(
        EstadoDaConfiguracao estado, IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        IReadOnlyList<RecusaDaCopiaDoItem> recusas = ConferirCopiasDoCatalogo(estado, catalogo, fatosJaColetados);
        if (recusas.Count > 0)
        {
            return Result<PlanoDaRecopiaDoCatalogo>.ValidationFailure([.. recusas.Select(static r => r.Erro)]);
        }

        return Result<PlanoDaRecopiaDoCatalogo>.Success(new PlanoDaRecopiaDoCatalogo(
            [.. estado.Campos
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
        EstadoDaConfiguracao estado,
        IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> catalogo, IReadOnlySet<string> fatosJaColetados)
    {
        ArgumentNullException.ThrowIfNull(estado);

        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(fatosJaColetados);

        List<RecusaDaCopiaDoItem> recusas = [];
        foreach (FatoColetado item in estado.Campos)
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
        EstadoDaConfiguracao estado,
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

        // Sob retificação, o fato que a inscrição já coletava continua coletado por ela, no mesmo
        // papel: quem se inscreveu numa versão anterior o informou, e as outras finalidades e as
        // versões seguintes contam com ele (UNI-REQ-0144).
        if (finalidade == FinalidadeFormulario.Inscricao
            && estado.Rascunho?.FatosDasVersoesPublicadas.PrimeiroDaInscricaoQueDeixaDeSerColetado(fatosColetados, grupos) is { } retirado)
        {
            return Result.Failure(new DomainError(
                "RascunhoRetificacao.FatoJaColetadoRemovido",
                $"O fato '{retirado}' já é coletado pelo formulário de inscrição do edital publicado: a retificação não o retira do formulário nem o passa de item para campo de grupo, ou de um grupo para outro."));
        }

        // O candidato pode ter respondido o fato em qualquer versão publicada, e a classificação e a
        // elegibilidade leem as respostas juntas: o item regravado não troca o eixo que a versão
        // publicada congelou (UNI-REQ-0144).
        if (estado.Rascunho?.FatosDasVersoesPublicadas.PrimeiroItemComEixoTrocado(fatosColetados, grupos) is { } troca)
        {
            return Result.Failure(troca.ParaErro());
        }

        // A inscrição só cita os próprios campos; as outras finalidades citam também os dela.
        HashSet<string> daInscricao = finalidade == FinalidadeFormulario.Inscricao
            ? new(StringComparer.Ordinal)
            : CitacoesDosFormularios.FatosDaInscricao(estado.Itens);
        if (CitacoesDosFormularios.ValidarGrafoDeFatos(formulario.Etapas, fatosColetados, grupos, daInscricao, CitacoesDosFormularios.DependenciasDasDerivacoes(estado)) is { } erro)
        {
            return Result.Failure(erro);
        }

        if (CitacoesDosFormularios.CitacaoNaoGarantida(
                estado,
                CitacoesDosFormularios.DoFormulario(finalidade, CitacoesDosFormularios.CitacoesDosCampos(fatosColetados, grupos).Select(static c => (c.Dono, c.Citado))),
                finalidade,
                CitacoesDosFormularios.SemCatalogo) is { } naoGarantida)
        {
            return Result.Failure(naoGarantida);
        }

        if (finalidade == FinalidadeFormulario.Inscricao && CitacoesDosFormularios.CitacaoQueFicariaOrfa(estado, fatosColetados) is { } orfa)
        {
            return Result.Failure(orfa);
        }

        // O código do grupo é único no processo e não coincide com o de fato coletado: um grupo novo
        // não repete grupo nem fato de outro formulário, e um fato novo não repete grupo de outro.
        HashSet<string> deOutrosFormularios = new(
            estado.Campos.Where(f => f.Finalidade != finalidade).Select(static f => f.FatoCodigo)
                .Concat(estado.GruposColetados.Where(g => g.Finalidade != finalidade).Select(static g => g.Codigo)),
            StringComparer.Ordinal);
        HashSet<string> gruposDeOutros = new(
            estado.GruposColetados.Where(g => g.Finalidade != finalidade).Select(static g => g.Codigo), StringComparer.Ordinal);
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
        if (estado.Campos.FirstOrDefault(f => f.Finalidade != finalidade && novos.Contains(f.FatoCodigo)) is { } jaProduzido)
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

/// <summary>O que gravar quando a coleta de uma finalidade é trocada: os itens e os grupos com os campos deles.</summary>
internal sealed record PlanoDaColeta(
    FinalidadeFormulario Finalidade,
    IReadOnlyList<FatoColetado> Fatos,
    IReadOnlyList<GrupoColetado> Grupos)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        // Sai tudo o que o formulário coletava, itens e campos dos grupos, e entra o novo.
        colecoes.Campos.RemoveAll(f => f.Finalidade == Finalidade);
        foreach (FatoColetado fato in Fatos)
        {
            ((IParteDoProcesso)fato).VincularProcesso(processoId);
            fato.VincularFinalidade(Finalidade);
            colecoes.Campos.Add(fato);
        }

        AplicarGrupos(colecoes, processoId);
    }

    internal void AplicarGrupos(ColecoesDoProcesso colecoes, Guid processoId)
    {
        colecoes.GruposColetados.RemoveAll(g => g.Finalidade == Finalidade);
        foreach (GrupoColetado grupo in Grupos)
        {
            ((IParteDoProcesso)grupo).VincularProcesso(processoId);
            grupo.VincularFinalidade(Finalidade);
            colecoes.GruposColetados.Add(grupo);
            colecoes.Campos.AddRange(grupo.Subitens);
        }
    }
}

/// <summary>A cópia do catálogo que cada item recebe: só a apresentação, para o fato que uma versão publicada já coletou, ou a cópia inteira.</summary>
internal sealed record PlanoDaRecopiaDoCatalogo(IReadOnlyList<(FatoColetado Item, CopiaDoItemNoCatalogo Copia, bool SoApresentacao)> Itens)
{
    internal void Aplicar()
    {
        foreach ((FatoColetado item, CopiaDoItemNoCatalogo copia, bool soApresentacao) in Itens)
        {
            if (soApresentacao)
            {
                item.RecopiarApresentacaoDoCatalogo(copia);
            }
            else
            {
                item.RecopiarDoCatalogo(copia);
            }
        }
    }
}
